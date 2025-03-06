using Peeper.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Peeper.Util
{
    public static unsafe class Interop
    {
        public static int Popcount(Bitmask value)
        {
            return (int)UInt128.PopCount(value);
        }

        public static int LSB(Bitmask value)
        {
            return (int)UInt128.TrailingZeroCount(value);
        }

        public static int PopLSB(Bitmask* value)
        {
            int sq = (int)UInt128.TrailingZeroCount(*value);
            *value = *value & (*value - 1);
            return sq;
        }


        public static unsafe void* AlignedAllocZeroed(nuint byteCount, nuint alignment = 64)
        {
            void* block = NativeMemory.AlignedAlloc(byteCount, alignment);
            NativeMemory.Clear(block, byteCount);

            return block;
        }

        public static unsafe T* AlignedAllocZeroed<T>(nuint items, nuint alignment = 64)
        {
            nuint bytes = ((nuint)sizeof(T) * (nuint)items);
            void* block = NativeMemory.AlignedAlloc(bytes, alignment);
            NativeMemory.Clear(block, bytes);

            return (T*)block;
        }
    }
}
