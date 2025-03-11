
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
            this.OnSearchFinish = (ref SearchInformation info) => Log($"bestmove {info.Position.Owner.AssocPool.GetBestThread().RootMoves[0].Move.ToString()}");
        }


        public bool HasDepthLimit => (DepthLimit != Utilities.MaxDepth);
        public bool HasNodeLimit => (HardNodeLimit != MaximumSearchNodes);
        public bool HasTimeLimit => (TimeManager.HardTimeLimit != SearchConstants.MaximumSearchTime);
        public bool IsInfinite => !HasDepthLimit && !HasTimeLimit;


    }
}
