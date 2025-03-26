
namespace Peeper.Logic.Search
{
    public static class SearchOptions
    {
        public static int Hash = 32;

        public static int Threads = 1;

        public static int MultiPV = 1;

        public static int MoveOverhead = 25;

        public static bool CuteChessWorkaround = false;

        public const bool UseRFP = true;
        public const bool UseNMP = true;
        public const bool UseRazoring = true;

        public static int AspWindow = 0;

        public static int RFPMult = 100;
        public static int RFPDepth = 5;

        public static int NMPDepth = 5;
        public static int NMPBaseRed = 3;
        public static int NMPDepthDiv = 4;

        public static int RazoringMaxDepth = 4;
        public static int RazoringMult = 280;

        public static int StatBonusMult = 100;
    }
}
