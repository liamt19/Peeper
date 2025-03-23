﻿
using System.Runtime.CompilerServices;
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

        public StatEntry this[int idx]
        {
            get => _History[idx];
            set => _History[idx] = value;
        }

        public StatEntry this[int pc, Move m]
        {
            get => _History[HistoryIndex(pc, m)];
            set => _History[HistoryIndex(pc, m)] = value;
        }

        [MethodImpl(Inline)]
        public static int HistoryIndex(int pc, Move m)
        {
            return (pc * SquareNB * SquareNB) + m.MoveMask;
        }

        public void Dispose() => NativeMemory.AlignedFree(_History);
        public void Clear() => NativeMemory.Clear(_History, (nuint)sizeof(StatEntry) * HistoryElements);
    }
}