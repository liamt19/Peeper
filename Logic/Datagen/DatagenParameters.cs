﻿
namespace Peeper.Logic.Datagen
{
    public static class DatagenParameters
    {
        public const int HashSize = 8;

        public const int MinOpeningPly = 8;
        public const int MaxOpeningPly = 9;

        public const int SoftNodeLimit = 5000;
        public const int DepthLimit = 14;

        public const int WritableDataLimit = 256;

        public const int AdjudicateMoves = 4;
        public const int AdjudicateScore = 3000;
        public const int MaxFilteringScore = 6000;

        public const int MaxOpeningScore = 1200;
    }
}
