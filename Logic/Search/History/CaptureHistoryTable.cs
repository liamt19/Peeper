
using System.Runtime.InteropServices;

namespace Peeper.Logic.Search.History
{
    public readonly unsafe struct CaptureHistoryTable
    {
        private readonly StatEntry* _History;
        private const int HistoryElements = ColorNB * PieceNB * SquareNB * PieceNB;

        public CaptureHistoryTable()
        {
            _History = AlignedAllocZeroed<StatEntry>(HistoryElements);
        }

        public StatEntry this[int idx]
        {
            get => _History[idx];
            set => _History[idx] = value;
        }

        public StatEntry this[int pc, int pt, int toSquare, int capturedPt]
        {
            get => _History[HistoryIndex(pc, pt, toSquare, capturedPt)];
            set => _History[HistoryIndex(pc, pt, toSquare, capturedPt)] = value;
        }

        public static int HistoryIndex(int pc, int pt, int toSquare, int capturedPt)
        {
            return (capturedPt * SquareNB * PieceNB * ColorNB) + (toSquare * PieceNB * ColorNB) + (pt * ColorNB) + pc;
        }

        public void Dispose() => NativeMemory.AlignedFree(_History);
        public void Clear() => NativeMemory.Clear(_History, (nuint)sizeof(StatEntry) * HistoryElements);
    }
}