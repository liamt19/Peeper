
using System.Runtime.CompilerServices;

namespace Peeper.Logic.Search.History
{
    public unsafe struct ThreadHistory
    {
        public const int NormalClamp = 16384;

        public readonly QuietHistoryTable QuietHistory;
        public readonly ContinuationHistory Continuations;

        public ThreadHistory()
        {
            Continuations = new ContinuationHistory();
            QuietHistory = new QuietHistoryTable();
        }

        [MethodImpl(Inline)]
        public int QuietScore(int stm, Move m)
        {
            return QuietHistory[stm, m];
        }


        public void Clear()
        {
            QuietHistory.Clear();
            Continuations.Clear();
        }

        public void Dispose()
        {
            QuietHistory.Dispose();
            Continuations.Dispose();
        }
    }
}