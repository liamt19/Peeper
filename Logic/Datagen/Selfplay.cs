
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

//#define DBG_PRINT

using System.Runtime.InteropServices;

using static Peeper.Logic.Datagen.DatagenParameters;

using Peeper.Logic.NN;
using Peeper.Logic.Threads;
using Peeper.Logic.Search;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Data;
using Peeper.Logic.Transposition;
using System.Text;


namespace Peeper.Logic.Datagen
{
    public static unsafe class Selfplay
    {
        private static int Seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> ThreadRNG = new(() => new Random(Interlocked.Increment(ref Seed)));

        public static void RunGames(ulong gamesToRun, int threadID, ulong softNodeLimit = SoftNodeLimit, ulong depthLimit = DepthLimit)
        {
            SearchOptions.Hash = HashSize;
            TimeManager.RemoveHardLimit();

            TranspositionTable tt = new(HashSize);
            SearchThread thread = new(0) { TT = tt, IsDatagen = true };
            Position pos = thread.RootPosition;
            ref Bitboard bb = ref pos.bb;

#if DBG_PRINT
            using var debugStream = File.Open($"dbg_{threadID}.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var debugStreamWriter = new StreamWriter(debugStream);
#endif

            void Dbg(string s)
            {
#if DBG_PRINT
                debugStreamWriter.WriteLine($"{threadID}\t{s}");
                debugStreamWriter.Flush();
#endif
            }

            string fName = $"{softNodeLimit / 1000}k_{depthLimit}d_{threadID}.bin";
            using var ostr = File.Open(fName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using var outWriter = new BinaryWriter(ostr);
            
            Stoatpack pack = new();

            ulong totalPositions = 0;
            ulong totalDepths = 0;

            var info = SearchInformation.DatagenStandard(pos, softNodeLimit, (int)depthLimit);
            var prelimInfo = SearchInformation.DatagenPrelim(pos, softNodeLimit, (int)depthLimit);

            for (ulong gameNum = 0; gameNum < gamesToRun; gameNum++)
            {
            
            ThisIsGoingToBeAnnoyingToFix:

                GetStartPos(thread, ref pack, ref prelimInfo);

                GameResult result = GameResult.None;
                int winPlies = 0, drawPlies = 0, lossPlies = 0;

                while (result == GameResult.None)
                {
                    int legalMoves = SetupThread(pos, thread);
                    if (legalMoves == 0)
                    {
                        result = pos.ToMove == Black ? GameResult.WhiteWin : GameResult.BlackWin;
                        break;
                    }

                    thread.Search(ref info);

                    Move move = thread.RootMoves[0].Move;
                    int score = thread.RootMoves[0].Score;
                    score *= (pos.ToMove == Black ? 1 : -1);

#if DBG_PRINT
                    debugStreamWriter.WriteLine($"{pos.GetSFen()}\t{move} {score}\t");
#endif
                    totalDepths += (ulong)thread.RootDepth;

                    if (IsDecisive(score))
                    {
                        result = score > 0 ? GameResult.BlackWin : GameResult.WhiteWin;
                        break;
                    }

                    pos.MakeMove(move);
                    var sennichite = pos.CheckSennichite(false, BoardState.StateStackSize);
                    if (sennichite == Sennichite.Draw)
                    {
                        result = GameResult.Draw;
                        break;
                    }
                    
                    if (sennichite == Sennichite.Win)
                    {
                        StringBuilder sb = new();
                        sb.AppendLine($"{move} caused an illegal perpetual!");
                        sb.AppendLine($"sfen: {pos.GetSFen()}");
                        sb.AppendLine($"captured: {pos.State->CapturedPiece}");
                        sb.AppendLine($"Prior keys: {string.Join(", ", pos.GetPriorStates().Select(x => x.Hash))}");
                        //FailFast(sb.ToString());
                        goto ThisIsGoingToBeAnnoyingToFix;
                    }


                    if (score >= AdjudicateScore)
                    {
                        winPlies++;
                        lossPlies = 0;
                        drawPlies = 0;
                    }
                    else if (score <= -AdjudicateScore)
                    {
                        winPlies = 0;
                        lossPlies++;
                        drawPlies = 0;
                    }
                    else
                    {
                        winPlies = 0;
                        lossPlies = 0;
                        drawPlies = 0;
                    }

                    if (winPlies >= AdjudicatePlies)
                    {
                        result = GameResult.BlackWin;
                    }
                    else if (lossPlies >= AdjudicatePlies)
                    {
                        result = GameResult.WhiteWin;
                    }
                    else if (drawPlies >= 10)
                    {
                        result = GameResult.Draw;
                    }

                    pack.Push(move, (short)score);

                    if (pack.IsAtMoveLimit())
                        result = GameResult.Draw;
                }

#if DBG_PRINT
                debugStreamWriter.WriteLine($"done, {result}");
#endif

                totalPositions += (uint)pack.MoveIndex;

                ProgressBroker.ReportProgress(threadID, gameNum, totalPositions, totalDepths);
                pack.AddResultsAndWrite(result, outWriter);
            }

        }


        private static void GetStartPos(SearchThread thread, ref Stoatpack pack, ref SearchInformation prelim)
        {
            Position pos = thread.RootPosition;

            Random rand = ThreadRNG.Value;
            MoveList legalMoves = new();

            pack.Clear();
            Span<Move> randomMoves = stackalloc Move[16];

            while (true)
            {
                thread.SetStop(false);
                thread.TT.Clear();
                thread.TT.TTUpdate();
                thread.History.Clear();

            Retry:
                pos.LoadStartpos();

                int randMoveCount = rand.Next(RandomPlies, RandomPlies + (RandomizeStartSide ? 1 : 0));
                for (int i = 0; i < randMoveCount; i++)
                {
                    int legals = pos.GenerateLegal(ref legalMoves);

                    if (legals == 0)
                        goto Retry;

                    Move rMove = legalMoves[rand.Next(0, legals)].Move;
                    randomMoves[i] = rMove;
                    pos.MakeMove(rMove);
                }

                if (pos.GenerateLegal(ref legalMoves) == 0)
                    continue;

                SetupThread(pos, thread);
                thread.Search(ref prelim);
                if (Math.Abs(thread.RootMoves[0].Score) >= MaxOpeningScore)
                    continue;

                for (int i = 0; i < randMoveCount; i++)
                    pack.PushUnscored(randomMoves[i]);

                thread.TT.Clear();
                thread.TT.TTUpdate();
                thread.History.Clear();
                return;
            }
        }


        private static int SetupThread(Position pos, SearchThread td)
        {
            td.Reset();
            td.SetStop(false);

            MoveList list = new();
            int size = pos.GenerateLegal(ref list);

            td.RootMoves.Clear();
            for (int j = 0; j < size; j++)
                td.RootMoves[j].ReplaceWith(list[j].Move);
            td.RootMoves.Resize(size);

            return size;
        }

    }
}
