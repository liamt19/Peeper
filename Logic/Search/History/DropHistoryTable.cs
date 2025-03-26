
using System.Runtime.InteropServices;

namespace Peeper.Logic.Search.History
{
    public readonly unsafe struct DropHistoryTable
    {
        private readonly StatEntry* _History;
        private const int HistoryElements = ColorNB * PieceNB * SquareNB;

        public DropHistoryTable()
        {
            _History = AlignedAllocZeroed<StatEntry>(HistoryElements);
        }

        public StatEntry this[int idx]
        {
            get => _History[idx];
            set => _History[idx] = value;
        }

        public ref StatEntry this[int pc, Move m]
        {
            get => ref _History[HistoryIndex(pc, m)];
            //set => _History[HistoryIndex(pc, m)] = value;
        }

        public static int HistoryIndex(int pc, Move m) => (pc * PieceNB * SquareNB) + (m.DroppedPiece * SquareNB) + m.To;

        public void Dispose() => NativeMemory.AlignedFree(_History);
        public void Clear() => NativeMemory.Clear(_History, (nuint)sizeof(StatEntry) * HistoryElements);
    }
}