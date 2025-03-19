
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using Peeper.Logic.Search;
using Peeper.Logic.Transposition;

namespace Peeper.Logic.Threads
{
    /// <summary>
    /// Keeps track of a number of SearchThreads and provides methods to start and wait for them to finish.
    /// 
    /// <para></para>
    /// Some of the thread logic in this class is based on Stockfish's Thread class
    /// (StartThreads, WaitForSearchFinished, and the general concepts in StartSearch), the sources of which are here:
    /// <br></br>
    /// https://github.com/official-stockfish/Stockfish/blob/master/src/thread.cpp
    /// <br></br>
    /// https://github.com/official-stockfish/Stockfish/blob/master/src/thread.h
    /// 
    /// </summary>
    public unsafe class SearchThreadPool
    {
        /// <summary>
        /// Global ThreadPool.
        /// </summary>
        public static SearchThreadPool GlobalSearchPool;

        public int ThreadCount = SearchOptions.Threads;

        public SearchInformation SharedInfo;
        public SearchThread[] Threads;
        
        public TranspositionTable TTable;

        public Barrier Blocker;

        static SearchThreadPool()
        {
            GlobalSearchPool = new SearchThreadPool(SearchOptions.Threads);
        }

        public SearchThreadPool(int threadCount)
        {
            Blocker = new Barrier(1);
            TTable = new TranspositionTable(Hash);
            Resize(threadCount);
        }


        public SearchThread MainThread => Threads[0];


        public void Resize(int newThreadCount)
        {
            if (Threads != null)
            {
                MainThread.WaitForThreadFinished();

                for (int i = 0; i < ThreadCount; i++)
                {
                    Threads[i]?.Dispose();
                }
            }

            this.ThreadCount = newThreadCount;
            Threads = new SearchThread[ThreadCount];

            for (int i = 0; i < ThreadCount; i++)
            {
                Threads[i] = new SearchThread(i);
                Threads[i].AssocPool = this;
                Threads[i].TT = TTable;
            }

            MainThread.WaitForThreadFinished();
        }


        public void StartSearch(Position rootPosition, ref SearchInformation rootInfo)
        {
            StartSearch(rootPosition, ref rootInfo, new ThreadSetup(rootPosition.GetSFen()));
        }

        public void StartSearch(Position rootPosition, ref SearchInformation rootInfo, ThreadSetup setup)
        {
            MainThread.WaitForThreadFinished();

            StartAllThreads();
            SharedInfo = rootInfo;
            SharedInfo.SearchActive = true;

            MoveList list = new();
            int size = rootPosition.GenerateLegal(ref list);

            var rootFEN = setup.StartFEN;
            if (rootFEN == InitialFEN && setup.SetupMoves.Count == 0)
            {
                rootFEN = rootPosition.GetSFen();
            }

            for (int i = 0; i < ThreadCount; i++)
            {
                var td = Threads[i];

                td.Nodes = 0;

                td.CompletedDepth = 0;
                td.RootDepth = 0;
                td.SelDepth = 0;
                td.NMPPly = 0;

                td.RootMoves = new List<RootMove>(size);
                for (int j = 0; j < size; j++)
                {
                    td.RootMoves.Add(new RootMove(list[j].Move));
                }

                if (setup.UCISearchMoves.Count != 0)
                {
                    td.RootMoves = td.RootMoves.Where(x => setup.UCISearchMoves.Contains(x.Move)).ToList();
                }

                td.RootPosition.LoadFromSFen(rootFEN);

                foreach (var move in setup.SetupMoves)
                {
                    td.RootPosition.MakeMove(move);
                }
            }

            TimeManager.StartTimer();
            MainThread.WakeUp();
        }


        public SearchThread GetBestThread()
        {
            SearchThread bestThread = MainThread;
            for (int i = 1; i < ThreadCount; i++)
            {
                int thisScore = Threads[i].RootMoves[0].Score - bestThread.RootMoves[0].Score;
                if (thisScore > 0 && (Threads[i].CompletedDepth >= bestThread.CompletedDepth))
                {
                    bestThread = Threads[i];
                }
            }

            return bestThread;
        }


        public void AwakenHelperThreads()
        {
            //  Skip Threads[0] because it will do this to itself after this method returns.
            for (int i = 1; i < ThreadCount; i++)
            {
                Threads[i].WakeUp();
            }
        }


        public void StopAllThreads()
        {
            for (int i = 1; i < ThreadCount; i++)
                Threads[i].SetStop(true);

            MainThread.SetStop(true);
        }

        public void StartAllThreads()
        {
            for (int i = 1; i < ThreadCount; i++)
                Threads[i].SetStop(false);

            MainThread.SetStop(false);
        }


        public void WaitForSearchFinished()
        {
            //  Skip Threads[0] (the MainThread) since this method is only ever called when the MainThread is done.
            for (int i = 1; i < ThreadCount; i++)
            {
                Threads[i].WaitForThreadFinished();
            }
        }


        public void BlockCallerUntilFinished()
        {
            //  This can happen if any thread other than the main thread calls this method.
            Assert(Blocker.ParticipantCount == 1,
                $"BlockCallerUntilFinished was called, but the barrier had {Blocker.ParticipantCount} participants (should have been 1)!");

            if (!SharedInfo.SearchActive)
            {
                //  Don't block if we aren't searching.
                return;
            }

            if (Blocker.ParticipantCount != 1)
            {
                //  This should never happen, but just in case we can signal once to try and unblock Blocker.
                Blocker.SignalAndWait(1);
                return;
            }

            //  The MainSearchThread is always a participant, and the calling thread is a temporary participant.
            //  The MainSearchThread will only signal if there are 2 participants, so add the calling thread.
            Blocker.AddParticipant();

            //  The MainSearchThread will signal Blocker once it has finished, so wait here until it does so.
            Blocker.SignalAndWait();

            //  We are done waiting, so remove the calling thread as a participant (now Blocker.ParticipantCount == 1)
            Blocker.RemoveParticipant();

        }


        public void Clear()
        {
            for (int i = 0; i < ThreadCount; i++)
            {
                Threads[i].History.Clear();
            }
        }


        public ulong GetNodeCount()
        {
            ulong total = 0;
            for (int i = 0; i < ThreadCount; i++)
            {
                total += Threads[i].Nodes;
            }
            return total;
        }


    }
}
