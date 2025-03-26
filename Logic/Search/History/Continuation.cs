
using System.Runtime.InteropServices;

namespace Peeper.Logic.Search.History
{

    public unsafe struct ContinuationHistory
    {
        private Butterfly* _History;

        private const int DimX = PieceNB * 2;
        private const int DimY = SquareNB;

        public const int Length = DimX * DimY;

        public ContinuationHistory()
        {
            _History = AlignedAllocZeroed<Butterfly>(Length);

            for (nuint i = 0; i < Length; i++)
            {
                (_History + i)->Alloc();
            }
        }

        public Butterfly* this[int color, int type, int sq] => &_History[Butterfly.GetIndex(color, type, sq)];
        public Butterfly* this[int idx] => &_History[idx];

        public void Dispose()
        {
            for (nuint i = 0; i < Length; i++)
                _History[i].Dispose();

            NativeMemory.AlignedFree(_History);
        }

        public void Clear()
        {
            for (nuint i = 0; i < Length; i++)
                _History[i].Clear();
        }
    }
}