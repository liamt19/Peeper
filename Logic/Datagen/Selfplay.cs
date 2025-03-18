
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

#define DBG_PRINT

using System.Runtime.InteropServices;

using static Peeper.Logic.Datagen.DatagenParameters;

using Peeper.Logic.NN;
using Peeper.Logic.Threads;
using Peeper.Logic.Search;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Data;


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

            SearchThreadPool pool = new SearchThreadPool(1);
            Position pos = new Position(owner: pool.MainThread);
            ref Bitboard bb = ref pos.bb;

            Random rand = ThreadRNG.Value;
            MoveList legalMoves = new();

            Move bestMove = Move.Null;
            int bestMoveScore = 0;

            string fName = $"{softNodeLimit / 1000}k_{depthLimit}d_{threadID}.bin";

#if DBG_PRINT
            using var debugStream = File.OpenWrite("dbg.txt");
            using var debugStreamWriter = new StreamWriter(debugStream);
#endif
            using FileStream bfeOutputFileStream = File.Open(fName, FileMode.OpenOrCreate);
            using BinaryWriter outputWriter = new BinaryWriter(bfeOutputFileStream);
            Span<PeeperDataFormat> datapoints = stackalloc PeeperDataFormat[WritableDataLimit];

            ulong totalBadPositions = 0;
            ulong totalGoodPositions = 0;

            SearchInformation info = SearchInformation.DatagenStandard(pos, softNodeLimit, (int)depthLimit);
            SearchInformation prelimInfo = SearchInformation.DatagenPrelim(pos, softNodeLimit, (int)depthLimit);

            Stopwatch sw = Stopwatch.StartNew();

            for (ulong gameNum = 0; gameNum < gamesToRun; gameNum++)
            {
                GetStartPos(pool, pos, ref prelimInfo);

                GameResult result = GameResult.Draw;
                int toWrite = 0;
                int filtered = 0;
                int adjudicationCounter = 0;

                while (true)
                {
                    pool.StartSearch(pos, ref info);
                    pool.BlockCallerUntilFinished();

                    bestMove = pool.GetBestThread().RootMoves[0].Move;
                    bestMoveScore = pool.GetBestThread().RootMoves[0].Score;

                    if (bestMoveScore == -ScoreInfinite)
                    {
                        int z = 0;
                    }

#if DBG_PRINT
                    debugStreamWriter.Write($"{pos.GetSFen()}\t{bestMove} {bestMoveScore}\t");
#endif

                    bestMoveScore *= (pos.ToMove == White ? -1 : 1);

                    if (Math.Abs(bestMoveScore) >= AdjudicateScore)
                    {
                        if (++adjudicationCounter > AdjudicateMoves)
                        {
                            result = (bestMoveScore > 0) ? GameResult.BlackWin : GameResult.WhiteWin;
                            break;
                        }
                    }
                    else
                        adjudicationCounter = 0;


                    bool inCheck = pos.Checked;
                    bool bmCap = pos.IsCapture(bestMove);
                    bool badScore = Math.Abs(bestMoveScore) > MaxFilteringScore;
                    bool isOk = !(inCheck || bmCap || badScore);
                    if (isOk)
                    {
#if DBG_PRINT
                        debugStreamWriter.WriteLine();
#endif
                        datapoints[toWrite].Fill(pos, bestMove, bestMoveScore);
                        toWrite++;
                    }
                    else
                    {
#if DBG_PRINT
                        debugStreamWriter.WriteLine($"Skipped {inCheck} {bmCap} {badScore}");
#endif
                        filtered++;
                    }

                    pos.MakeMove(bestMove);

                    if (pos.GenerateLegal(ref legalMoves) == 0)
                    {
                        result = !pos.Checked          ? GameResult.Draw 
                               : (pos.ToMove == White) ? GameResult.BlackWin 
                               :                         GameResult.WhiteWin;
                        
                        break;
                    }
                    else if (pos.IsDraw())
                    {
                        result = GameResult.Draw;
                        break;
                    }
                    else if (toWrite == WritableDataLimit - 1)
                    {
                        result = bestMoveScore >  800 ? GameResult.BlackWin :
                                 bestMoveScore < -800 ? GameResult.WhiteWin :
                                                        GameResult.Draw;
                        break;
                    }
                }

#if DBG_PRINT
                debugStreamWriter.WriteLine($"done, {result}");
#endif


                totalBadPositions += (uint)filtered;
                totalGoodPositions += (uint)toWrite;

                var goodPerSec = totalGoodPositions / sw.Elapsed.TotalSeconds;
                var totalPerSec = (totalGoodPositions + totalBadPositions) / sw.Elapsed.TotalSeconds;

                ProgressBroker.ReportProgress(threadID, gameNum, totalGoodPositions, goodPerSec);
                AddResultsAndWrite(datapoints[..toWrite], result, outputWriter);
            }

        }


        private static void GetStartPos(SearchThreadPool pool, Position pos, ref SearchInformation prelim)
        {
            Random rand = ThreadRNG.Value;
            MoveList legalMoves = new();

            while (true)
            {
                pool.TTable.Clear();
                pool.Clear();

            Retry:

                pos.LoadFromSFen(InitialFEN);

                int randMoveCount = rand.Next(MinOpeningPly, MaxOpeningPly + 1);
                for (int i = 0; i < randMoveCount; i++)
                {
                    int legals = pos.GenerateLegal(ref legalMoves);

                    if (legals == 0)
                        goto Retry;

                    pos.MakeMove(legalMoves[rand.Next(0, legals)].Move);
                }

                if (pos.GenerateLegal(ref legalMoves) == 0)
                    continue;

                pool.StartSearch(pos, ref prelim);
                pool.BlockCallerUntilFinished();

                if (Math.Abs(pool.GetBestThread().RootMoves[0].Score) >= MaxOpeningScore)
                    continue;

                return;
            }
        }

        private static void AddResultsAndWrite(Span<PeeperDataFormat> datapoints, GameResult gr, BinaryWriter outputWriter)
        {
            for (int i = 0; i < datapoints.Length; i++)
                datapoints[i].SetResult(gr);

            fixed (PeeperDataFormat* ptr = datapoints)
            {
                byte* data = (byte*)ptr;
                outputWriter.Write(new Span<byte>(data, datapoints.Length * sizeof(PeeperDataFormat)));
            }

            outputWriter.Flush();
        }



        public static void ResetPosition(Position pos)
        {
            ref Bitboard bb = ref pos.bb;

            pos.MoveNumber = 1;

            pos.State = pos.StartingState;

            var st = pos.State;
            NativeMemory.Clear(st, BoardState.StateCopySize);
            st->CapturedPiece = None;
            st->KingSquares[White] = bb.KingIndex(White);
            st->KingSquares[Black] = bb.KingIndex(Black);

            pos.SetState();

            NNUE.RefreshAccumulator(pos);
        }


        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
    }
}
