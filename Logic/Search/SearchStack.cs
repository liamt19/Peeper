using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Peeper.Logic.Search
{
    public unsafe struct SearchStack
    {
        public Move* PV;
        public Move CurrentMove;
        public short Ply;

        public void Clear()
        {
            if (PV != null)
            {
                NativeMemory.AlignedFree(PV);
                PV = null;
            }

            CurrentMove = Move.Null;
            Ply = 0;
        }
    }
}
