using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peeper.Logic.Data
{
    public unsafe struct RootMoveList
    {
        public RootMove[] Buffer;
        public int Size { get; private set; }
        public int Count => Size;

        public RootMoveList()
        {
            Buffer = new RootMove[MoveListSize];
            Size = 0;
        }

        [UnscopedRef]
        public ref RootMove this[int i] => ref Buffer[i];

        [UnscopedRef]
        public ref RootMove Last() => ref Buffer[Size - 1];

        public void ReplaceWith(int i, Move m) => Buffer[i].ReplaceWith(m);
        public void Add(RootMove m) => Buffer[Size++] = m;
        public void Resize(int newSize) => Size = newSize;
        public void Clear() => Size = 0;

        public void FilterWhere(Func<RootMove, bool> predicate)
        {
            for (int i = 0; i < Size; i++)
            {
                var m = this[i];
                if (!predicate(m))
                {
                    this[i] = Last();
                    Size--;
                    i--;
                }
            }
        }

        public Span<RootMove> ToSpan()
        {
            fixed (RootMove* buff = &Buffer[0])
                return new Span<RootMove>(buff, Size);
        }

        public RootMove* ToSpicyPointer()
        {
            fixed (RootMove* buff = &Buffer[0])
                return buff;
        }
    }
}
