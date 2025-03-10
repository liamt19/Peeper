
namespace Peeper.Logic.Search
{
    public static class SearchConstants
    {
        public const int ScoreNone = 32000;
        public const int ScoreInfinite = 31000;
        public const int ScoreMate = 30000;
        public const int ScoreDraw = 0;

        public const int ScoreTTWin = ScoreMate - 512;
        public const int ScoreTTLoss = -ScoreTTWin;

        public const int ScoreMateMax = ScoreMate - 256;
        public const int ScoreMatedMax = -ScoreMateMax;
    }
}
