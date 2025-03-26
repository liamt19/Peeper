
using System.Runtime.InteropServices;

namespace Peeper.Logic.Search.History
{

    public unsafe struct Butterfly
    {
        private StatEntry* _History;

        private const short FillValue = 0;

        private const int DimX = PieceNB * 2;
        private const int DimY = SquareNB;

        public const int Length = DimX * DimY;

        public Butterfly() { }

        public StatEntry this[int color, int type, int sq]
        {
            get => _History[GetIndex(color, type, sq)];
            set => _History[GetIndex(color, type, sq)] = value;
        }

        public StatEntry this[int idx]
        {
            get => _History[idx];
            set => _History[idx] = value;
        }


        public static int GetIndex(int color, int type, int sq)
        {
            Assert((((type + (PieceNB * color)) * DimY) + sq) is >= 0 and < Length, $"GetIndex({color}, {type}, {sq}) should be < {Length}");
            return ((type + (PieceNB * color)) * DimY) + sq;
        }

        public void Alloc() => _History = AlignedAllocZeroed<StatEntry>(Length);
        public void Dispose() => NativeMemory.AlignedFree(_History);
        public void Clear() => new Span<StatEntry>(_History, (int)Length).Fill(FillValue);
    }

}