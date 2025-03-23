
using System.Runtime.CompilerServices;

namespace Peeper.Logic.Search.History
{
    public unsafe struct ThreadHistory
    {
        public const int NormalClamp = 16384;

        public readonly CaptureHistoryTable CaptureHistory;
        public readonly QuietHistoryTable QuietHistory;
        public readonly ContinuationHistory Continuations;

        public ThreadHistory()
        {
            Continuations = new ContinuationHistory();
            QuietHistory = new QuietHistoryTable();
            CaptureHistory = new CaptureHistoryTable();
        }

        [MethodImpl(Inline)]
        public int QuietScore(int stm, Move m)
        {
            return QuietHistory[stm, m];
        }

        [MethodImpl(Inline)]
        public int CaptureScore(int pc, int pt, int toSquare, int capturedPt)
        {
            return CaptureHistory[pc, pt, toSquare, capturedPt];
        }


        public void Clear()
        {
            QuietHistory.Clear();
            CaptureHistory.Clear();
            Continuations.Clear();
        }

        public void Dispose()
        {
            QuietHistory.Dispose();
            CaptureHistory.Dispose();
            Continuations.Dispose();
        }
    }
}