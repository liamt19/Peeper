
using System.Runtime.CompilerServices;

namespace Peeper.Logic.Search.History
{
    public unsafe struct ThreadHistory
    {
        public const int NormalClamp = 16384;

        public readonly CaptureHistoryTable CaptureHistory;
        public readonly QuietHistoryTable QuietHistory;
        public readonly DropHistoryTable DropHistory;
        public readonly ContinuationHistory Continuations;

        public ThreadHistory()
        {
            CaptureHistory = new CaptureHistoryTable();
            QuietHistory = new QuietHistoryTable();
            DropHistory = new DropHistoryTable();
            Continuations = new ContinuationHistory();
        }

        [MethodImpl(Inline)]
        public int CaptureScore(int pc, int pt, int toSquare, int capturedPt)
        {
            return CaptureHistory[pc, pt, toSquare, capturedPt];
        }

        [MethodImpl(Inline)]
        public int QuietScore(int stm, Move m)
        {
            if (m.IsDrop)
                return DropHistory[stm, m];

            return QuietHistory[stm, m];
        }


        public void Clear()
        {
            CaptureHistory.Clear();
            QuietHistory.Clear();
            DropHistory.Clear();
            Continuations.Clear();
        }

        public void Dispose()
        {
            CaptureHistory.Dispose();
            QuietHistory.Dispose();
            DropHistory.Dispose();
            Continuations.Dispose();
        }
    }
}