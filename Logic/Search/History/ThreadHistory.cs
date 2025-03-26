
namespace Peeper.Logic.Search.History
{
    public unsafe struct ThreadHistory
    {
        public const int NormalClamp = 16384;

        public readonly DropHistoryTable DropHistory;
        public readonly QuietHistoryTable QuietHistory;
        public readonly ContinuationHistory Continuations;

        public readonly ref StatEntry HistoryForMove(int pc, Move m)
        {
            return ref m.IsDrop ? ref DropHistory[pc, m] : ref QuietHistory[pc, m];
        }

        public ThreadHistory()
        {
            Continuations = new();
            DropHistory = new();
            QuietHistory = new();
        }

        public void Clear()
        {
            QuietHistory.Clear();
            DropHistory.Clear();
            Continuations.Clear();
        }

        public void Dispose()
        {
            QuietHistory.Dispose();
            DropHistory.Dispose();
            Continuations.Dispose();
        }
    }
}
