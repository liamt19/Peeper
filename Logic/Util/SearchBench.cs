using Peeper.Logic.Search;
using Peeper.Logic.USI;

namespace Peeper.Logic.Util
{
    public static class SearchBench
    {
        public const int DefaultDepth = 9;

        public static void Go(int depth = DefaultDepth, bool openBench = false)
        {
            Position pos = new Position(InitialFEN, owner: GlobalSearchPool.MainThread);

            Stopwatch sw = Stopwatch.StartNew();

            ulong totalNodes = 0;
            SearchInformation info = new(pos);
            info.DepthLimit = depth;
            info.OnDepthFinish = null;
            info.OnSearchFinish = null;
            TimeManager.SetHardLimit(20000);

            GlobalSearchPool.MainThread.WaitForThreadFinished();
            GlobalSearchPool.TTable.Clear();
            GlobalSearchPool.Clear();

            foreach (string fen in BenchFENs)
            {
                pos.LoadFromSFen(fen);
                GlobalSearchPool.StartSearch(pos, ref info);

                GlobalSearchPool.MainThread.WaitForThreadFinished();

                ulong thisNodeCount = GlobalSearchPool.GetNodeCount();
                totalNodes += thisNodeCount;
                if (!openBench)
                {
                    Log($"{fen,-76}\t{thisNodeCount}");
                }

                GlobalSearchPool.TTable.Clear();
                GlobalSearchPool.Clear();
            }
            sw.Stop();

            if (openBench)
            {
                Console.WriteLine($"info string {sw.Elapsed.TotalSeconds} seconds");

                var time = (int)(totalNodes / sw.Elapsed.TotalSeconds);
                Console.WriteLine($"{totalNodes} nodes {string.Join("", time.ToString("N0").Where(char.IsDigit))} nps");
            }
            else if (USIClient.Active)
            {
                Console.WriteLine($"info string nodes {totalNodes} time {Math.Round(sw.Elapsed.TotalSeconds)} nps {(int)(totalNodes / sw.Elapsed.TotalSeconds):N0}");
            }
            else
            {
                Log($"\r\nNodes searched: {totalNodes} in {sw.Elapsed.TotalSeconds} s ({(int)(totalNodes / sw.Elapsed.TotalSeconds):N0} nps)" + "\r\n");
            }
        }

        private static string[] BenchFENs = new string[]
        {
            "lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL b - 1",
            "8l/1l+R2P3/p2pBG1pp/kps1p4/Nn1P2G2/P1P1P2PP/1PS6/1KSG3+r1/LN2+p3L w Sbgn3p 124",
            "lnsgkgsnl/1r7/p1ppp1bpp/1p3pp2/7P1/2P6/PP1PPPP1P/1B3S1R1/LNSGKG1NL b - 9",
            "l4S2l/4g1gs1/5p1p1/pr2N1pkp/4Gn3/PP3PPPP/2GPP4/1K7/L3r+s2L w BS2N5Pb 1",
            "6n1l/2+S1k4/2lp4p/1np1B2b1/3PP4/1N1S3rP/1P2+pPP+p1/1p1G5/3KG2r1 b GSN2L4Pgs2p 1",
            "l6nl/5+P1gk/2np1S3/p1p4Pp/3P2Sp1/1PPb2P1P/P5GS1/R8/LN4bKL w RGgsn5p 1",
        };
    }
}
