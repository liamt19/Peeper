
using System.Runtime.CompilerServices;

namespace Peeper.Logic.Search
{
    public static class SearchConstants
    {
        public const short ScoreNone = 32000;
        public const int ScoreInfinite = 31000;
        public const int ScoreMate = 30000;
        public const int ScoreDraw = 0;

        public const int ScoreMateMax = ScoreMate - 256;
        public const int ScoreMatedMax = -ScoreMateMax;

        public const int AlphaStart = -ScoreMate;
        public const int BetaStart = ScoreMate;

        public const int ScoreTTWin = ScoreMate - 512;
        public const int ScoreTTLoss = -ScoreTTWin;

        public const short NNUEAbsMax = ScoreTTWin - 1;


        public const int MaximumSearchTime = int.MaxValue - 1;
        public const ulong MaximumSearchNodes = ulong.MaxValue - 1;
        public const int DefaultMovesToGo = 20;


        [MethodImpl(Inline)]
        public static int MakeDrawScore(ulong nodes)
        {
            return ScoreDraw;
        }

        [MethodImpl(Inline)]
        public static int MakeMateScore(int ply)
        {
            return -ScoreMate + ply;
        }

        [MethodImpl(Inline)]
        public static bool IsScoreMate(int score)
        {
            return Math.Abs(Math.Abs(score) - ScoreMate) < MaxDepth;
        }
    }
}
