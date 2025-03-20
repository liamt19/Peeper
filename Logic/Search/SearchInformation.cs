
namespace Peeper.Logic.Search
{
    public struct SearchInformation
    {
        public delegate void ActionRef<T>(ref T info);
        public ActionRef<SearchInformation>? OnDepthFinish;
        public ActionRef<SearchInformation>? OnSearchFinish;

        public Position Position;

        public int DepthLimit = Utilities.MaxDepth;
        public ulong HardNodeLimit = MaximumSearchNodes;
        public ulong SoftNodeLimit = MaximumSearchNodes;

        public bool SearchFinishedCalled = false;
        public bool SearchActive = false;

        public SearchInformation(Position p)
        {
            this.Position = p;

            this.OnDepthFinish = Utilities.PrintSearchInfo;
            this.OnSearchFinish = (ref SearchInformation info) => Log($"bestmove {info.Position.Owner.AssocPool.GetBestThread().RootMoves[0].Move}");
        }


        public bool HasDepthLimit => (DepthLimit != Utilities.MaxDepth);
        public bool HasNodeLimit => (HardNodeLimit != MaximumSearchNodes);
        public bool HasTimeLimit => (TimeManager.HardTimeLimit != SearchConstants.MaximumSearchTime);
        public bool IsInfinite => !HasDepthLimit && !HasTimeLimit;


        public static SearchInformation DatagenPrelim(Position pos, ulong nodeLimit, int depthLimit)
        {
            return new SearchInformation(pos)
            {
                SoftNodeLimit = nodeLimit * 4,
                HardNodeLimit = nodeLimit * 20,
                DepthLimit = Math.Max(8, depthLimit),
                OnDepthFinish = null,
                OnSearchFinish = null,
            };
        }

        public static SearchInformation DatagenStandard(Position pos, ulong nodeLimit, int depthLimit)
        {
            return new SearchInformation(pos)
            {
                SoftNodeLimit = nodeLimit,
                HardNodeLimit = nodeLimit * 20,
                DepthLimit = depthLimit,
                OnDepthFinish = null,
                OnSearchFinish = null,
            };
        }
    }
}
