
using System.Runtime.InteropServices;

namespace Peeper.Logic.Search.History
{
    public readonly unsafe struct QuietHistoryTable
    {
        private readonly StatEntry* _History;
        private const int HistoryElements = ColorNB * SquareNB * SquareNB;

        public QuietHistoryTable()
        {
            _History = AlignedAllocZeroed<StatEntry>(HistoryElements);
        }

        public ref StatEntry this[int pc, Move m]
        {
            get => ref _History[HistoryIndex(pc, m)];
        }

        public static int HistoryIndex(int pc, Move m) => (pc * SquareNB * SquareNB) + m.MoveMask;

        public void Dispose() => NativeMemory.AlignedFree(_History);
        public void Clear() => NativeMemory.Clear(_History, (nuint)sizeof(StatEntry) * HistoryElements);
    }
}