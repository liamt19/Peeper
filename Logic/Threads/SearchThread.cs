﻿
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.Runtime.InteropServices;
using Peeper.Logic.Search;
using Peeper.Logic.Search.History;
using Peeper.Logic.Transposition;

namespace Peeper.Logic.Threads
{

    /// <summary>
    /// Represents a thread that performs searches. 
    /// 
    /// <para></para>
    /// Much of the actual thread logic in this class is based on Stockfish's Thread class
    /// (namely PrepareToSearch, WaitForThreadFinished, MainThreadSearch, and IdleLoop), the source of which is here:
    /// <br></br>
    /// https://github.com/official-stockfish/Stockfish/blob/master/src/thread.cpp
    /// <para></para>
    /// 
    /// The main differences are in using dumbed-down, explicit versions of condition_variable::wait()
    /// and having to deal with spurious wakeups because of that.
    /// 
    /// </summary>
    public unsafe class SearchThread : IDisposable
    {
        private bool _Disposed = false;

        public ulong Nodes;

        public int ThreadIdx;
        public int PVIndex;
        public int RootDepth;
        public int SelDepth;
        public int CompletedDepth;
        public int NMPPly;

        public bool Searching;
        public bool Quit;
        public readonly bool IsMain = false;

        public readonly Position RootPosition;

        public List<RootMove> RootMoves = new List<RootMove>(64);

        public HistoryTable History;

        public ulong[][] NodeTable;

        public SearchThreadPool AssocPool;
        public TranspositionTable TT;

        private Thread _SysThread;

        private readonly object _Mutex;

        private readonly ConditionVariable _SearchCond;

        private Barrier _InitBarrier = new Barrier(2);

        public Move CurrentMove => RootMoves[PVIndex].Move;

        public SearchThread(int idx)
        {
            ThreadIdx = idx;
            if (ThreadIdx == 0)
            {
                IsMain = true;
            }

            _Mutex = "Mut" + ThreadIdx;
            _SearchCond = new ConditionVariable();
            Searching = true;

            //  Each thread its own position object, which lasts the entire lifetime of the thread.
            RootPosition = new Position(InitialFEN, true, this);

            _SysThread = new Thread(ThreadInit);

            //  Start the new thread, which will enter into ThreadInit --> IdleLoop
            _SysThread.Start();

            //  Wait here until the new thread signals that it is ready.
            _InitBarrier.SignalAndWait();

            WaitForThreadFinished();

            //  This isn't necessary but doesn't hurt either.
            _InitBarrier.RemoveParticipant();
        }


        public string? FriendlyName => _SysThread.Name;
        public ulong HardNodeLimit => AssocPool.SharedInfo.HardNodeLimit;


        /// <summary>
        /// Initializes this thread's Accumulators and history heuristic data.
        /// </summary>
        public void ThreadInit()
        {
            Quit = false;

            History = new HistoryTable();

            NodeTable = new ulong[DropSourceSquare + 1][];
            for (int sq = 0; sq < DropSourceSquare + 1; sq++)
            {
                NodeTable[sq] = new ulong[SquareNB];
            }

            _SysThread.Name = "SearchThread " + ThreadIdx + ", ID " + Environment.CurrentManagedThreadId;
            if (IsMain)
            {
                _SysThread.Name = "(MAIN)Thread " + ThreadIdx + ", ID " + Environment.CurrentManagedThreadId;
            }

            IdleLoop();
        }



        /// <summary>
        /// Sets this thread's <see cref="Searching"/> variable to true, which will cause the thread in the IdleLoop to
        /// call the search function once it wakes up.
        /// </summary>
        public void PrepareToSearch()
        {
            Monitor.Enter(_Mutex);
            Searching = true;
            Monitor.Exit(_Mutex);

            _SearchCond.Pulse();
        }


        /// <summary>
        /// Blocks the calling thread until this SearchThread has exited its search call 
        /// and has returned to the beginning of its IdleLoop.
        /// </summary>
        public void WaitForThreadFinished()
        {
            if (_Mutex == null)
            {
                //  Asserting that _Mutex has been initialized properly
                throw new Exception("Thread " + Thread.CurrentThread.Name + " tried accessing the Mutex of " + this.ToString() + ", but Mutex was null!");
            }

            Monitor.Enter(_Mutex);

            while (Searching)
            {
                _SearchCond.Wait(_Mutex);

                if (Searching)
                {
                    ///  Spurious wakeups are possible here if <see cref="SearchThreadPool.StartSearch"/> is called
                    ///  again before this thread has returned to IdleLoop.
                    _SearchCond.Pulse();
                    Thread.Yield();
                }
            }

            Monitor.Exit(_Mutex);
        }


        /// <summary>
        /// The main loop that threads will be in while they are not currently searching.
        /// Threads enter here after they have been initialized and do not leave until their thread is terminated.
        /// </summary>
        public void IdleLoop()
        {
            //  Let the main thread know that this thread is initialized and ready to go.
            _InitBarrier.SignalAndWait();

            while (true)
            {
                Monitor.Enter(_Mutex);
                Searching = false;
                _SearchCond.Pulse();

                while (!Searching)
                {
                    //  Wait here until we are notified of a change in Searching's state.
                    _SearchCond.Wait(_Mutex);
                    if (!Searching)
                    {
                        //  This was a spurious wakeup since Searching's state has not changed.

                        //  Another thread was waiting on this signal but the OS gave it to this thread instead.
                        //  We can pulse the condition again, yield, and hope that the OS gives it to the thread that actually needs it
                        _SearchCond.Pulse();
                        Thread.Yield();
                    }

                }

                if (Quit)
                    return;

                Monitor.Exit(_Mutex);

                if (IsMain)
                {
                    MainThreadSearch();
                }
                else
                {
                    Search();
                }
            }
        }



        /// <summary>
        /// Called by the MainThread after it is woken up by a call to <see cref="SearchThreadPool.StartSearch"/>.
        /// The MainThread will wake up the other threads and notify them to begin searching, and then start searching itself. 
        /// Once the MainThread finishes searching, it will wait until all other threads have finished as well, and will then 
        /// send the search results as output to the UCI.
        /// </summary>
        public void MainThreadSearch()
        {
            TT.TTUpdate();  //  Age the TT

            AssocPool.StartThreads();   //  Start other threads (if any)
            this.Search();              //  Make this thread begin searching

            while (!AssocPool.StopThreads && AssocPool.SharedInfo.IsInfinite) { }

            //  When the main thread is done, prevent the other threads from searching any deeper
            AssocPool.StopThreads = true;

            //  Wait for the other threads to return
            AssocPool.WaitForSearchFinished();

            //  Search is finished, now give the UCI output.
            AssocPool.SharedInfo.OnSearchFinish?.Invoke(ref AssocPool.SharedInfo);
            TimeManager.ResetTimer();

            AssocPool.SharedInfo.SearchActive = false;

            //  If the main program thread called BlockCallerUntilFinished,
            //  then the Blocker's ParticipantCount will be 2 instead of 1.
            if (AssocPool.Blocker.ParticipantCount == 2)
            {
                //  Signal that we are here, but only wait for 1 ms if the main thread isn't already waiting
                AssocPool.Blocker.SignalAndWait(1);
            }
        }

        /// <summary>
        /// Main deepening loop for threads. This is essentially the same as the old "StartSearching" method that was used.
        /// </summary>
        public void Search()
        {
            SearchStack* _ss = stackalloc SearchStack[MaxPly];
            SearchStack* ss = _ss + 10;
            for (int i = -10; i < MaxSearchStackPly; i++)
            {
                (ss + i)->Clear();
                (ss + i)->Ply = (short)i;
                (ss + i)->PV = AlignedAllocZeroed<Move>(MaxPly);
                (ss + i)->ContinuationHistory = History.Continuations[0][0][0, 0, 0];
            }

            for (int sq = 0; sq < SquareNB; sq++)
            {
                Array.Clear(NodeTable[sq]);
            }

            //  Create a copy of the AssocPool's root SearchInformation instance.
            SearchInformation info = AssocPool.SharedInfo;

            info.Position = RootPosition;

            int multiPV = Math.Min(SearchOptions.MultiPV, RootMoves.Count);

            Span<int> searchScores = stackalloc int[MaxPly];
            int scoreIdx = 0;

            RootMove lastBestRootMove = new RootMove(Move.Null);
            int stability = 0;

            int maxDepth = IsMain ? MaxDepth : MaxPly;
            while (++RootDepth < maxDepth)
            {
                if (IsMain && RootDepth > info.DepthLimit)
                    break;

                if (AssocPool.StopThreads)
                    break;

                foreach (RootMove rm in RootMoves)
                {
                    rm.PreviousScore = rm.Score;
                }

                int usedDepth = RootDepth;

                for (PVIndex = 0; PVIndex < multiPV; PVIndex++)
                {
                    if (AssocPool.StopThreads)
                        break;

                    int alpha = AlphaStart;
                    int beta = BetaStart;
                    int window = ScoreInfinite;
                    int score = RootMoves[PVIndex].AverageScore;
                    SelDepth = 0;

#if NO
                    if (usedDepth >= 5)
                    {
                        window = AspWindow;
                        alpha = Math.Max(AlphaStart, score - window);
                        beta = Math.Min(BetaStart, score + window);
                    }
#endif

                    while (true)
                    {
                        score = Logic.Search.Searches.Negamax<RootNode>(info.Position, ss, alpha, beta, Math.Max(1, usedDepth));

                        StableSort(RootMoves, PVIndex);

                        if (AssocPool.StopThreads)
                            break;

#if NO
                        if (score <= alpha)
                        {
                            beta = (alpha + beta) / 2;
                            alpha = Math.Max(alpha - window, AlphaStart);
                            usedDepth = RootDepth;
                        }
                        else if (score >= beta)
                        {
                            beta = Math.Min(beta + window, BetaStart);
                            usedDepth = Math.Max(usedDepth - 1, RootDepth - 5);
                        }
                        else
                            break;

                        window += window / 2;
#else
                        break;
#endif
                    }

                    StableSort(RootMoves, 0, PVIndex + 1);

                    if (IsMain && (AssocPool.StopThreads || PVIndex == multiPV - 1))
                    {
                        info.OnDepthFinish?.Invoke(ref info);
                    }
                }

                if (!IsMain)
                    continue;

                if (AssocPool.StopThreads)
                {
                    //  If we received a stop command or hit the hard time limit, our RootMoves may not have been filled in properly.
                    //  In that case, we replace the current bestmove with the last depth's bestmove
                    //  so that the move we send is based on an entire depth being searched instead of only a portion of it.
                    RootMoves[0] = lastBestRootMove;

                    for (int i = -10; i < MaxSearchStackPly; i++)
                    {
                        NativeMemory.AlignedFree((ss + i)->PV);
                    }

                    return;
                }


                if (lastBestRootMove.Move == RootMoves[0].Move)
                {
                    stability++;
                }
                else
                {
                    stability = 0;
                }

                lastBestRootMove.Move = RootMoves[0].Move;
                lastBestRootMove.Score = RootMoves[0].Score;
                lastBestRootMove.Depth = RootMoves[0].Depth;

                for (int i = 0; i < MaxPly; i++)
                {
                    lastBestRootMove.PV[i] = RootMoves[0].PV[i];
                    if (lastBestRootMove.PV[i] == Move.Null)
                    {
                        break;
                    }
                }

                searchScores[scoreIdx++] = RootMoves[0].Score;

                if (SoftTimeUp(stability, searchScores[..scoreIdx]))
                {
                    break;
                }

                if (Nodes >= info.SoftNodeLimit)
                {
                    break;
                }

                if (!AssocPool.StopThreads)
                {
                    CompletedDepth = RootDepth;
                }
            }

            if (IsMain && RootDepth >= MaxDepth && info.HasNodeLimit && !AssocPool.StopThreads)
            {
                //  If this was a "go nodes x" command, it is possible for the main thread to hit the
                //  maximum depth before hitting the requested node count (causing an infinite wait).

                //  If this is the case, and we haven't been told to stop searching, then we need to stop now.
                AssocPool.StopThreads = true;
            }

            for (int i = -10; i < MaxSearchStackPly; i++)
            {
                NativeMemory.AlignedFree((ss + i)->PV);
            }
        }

        private static ReadOnlySpan<double> StabilityCoefficients => [2.2, 1.6, 1.4, 1.1, 1, 0.95, 0.9];
        private static int StabilityMax = StabilityCoefficients.Length - 1;

        private bool SoftTimeUp(int stability, Span<int> searchScores)
        {
            if (!TimeManager.HasSoftTime)
                return false;

            double multFactor = 1.0;
#if NO
            if (RootDepth > 7)
            {
                double nodeTM = ((1.5 - NodeTable[RootMoves[0].Move.From][RootMoves[0].Move.To] / (double)Nodes) * 1.75);
                double bmStability = StabilityCoefficients[Math.Min(stability, StabilityMax)];

                double scoreStability = searchScores[searchScores.Length - 1 - 3] 
                                      - searchScores[searchScores.Length - 1 - 0];

                scoreStability = Math.Max(0.85, Math.Min(1.15, 0.034 * scoreStability));

                multFactor = nodeTM * bmStability * scoreStability;
            }
#endif

            if (TimeManager.GetSearchTime() >= TimeManager.SoftTimeLimit * multFactor)
            {
                return true;
            }

            return false;
        }

        public bool NodeLimitReached()
        {
            if (SearchOptions.Threads == 1)
            {
                return Nodes >= HardNodeLimit;
            }
            else if (Nodes % 1024 == 0)
            {
                return AssocPool.GetNodeCount() >= HardNodeLimit;
            }

            return false;
        }

        public void SetStop() => AssocPool.StopThreads = true;
        public bool ShouldStop() => AssocPool.StopThreads;


        /// <summary>
        /// Frees up the memory that was allocated to this SearchThread.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                Assert(Searching == false, "The thread {ToString()} had its Dispose({disposing}) method called Searching was {Searching}!");

                //  Set quit to True, and pulse the condition to allow the thread in IdleLoop to exit.
                Quit = true;

                PrepareToSearch();
            }


            //  And free up the memory we allocated for this thread.
            History.Dispose();

            //  Destroy the underlying system thread
            _SysThread.Join();

            _Disposed = true;
        }

        /// <summary>
        /// Calls the class destructor, which will free up the memory that was allocated to this SearchThread.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);

            //  We handled the finalization ourselves, so tell the GC not to worry about it.
            GC.SuppressFinalize(this);
        }

        ~SearchThread()
        {
            Dispose(false);
        }


        public override string ToString()
        {
            return "[" + (_SysThread != null ? _SysThread.Name : "NULL?") + " (caller ID " + Environment.CurrentManagedThreadId + ")]";
        }
    }
}
