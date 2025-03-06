using Peeper.Logic.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Peeper.Logic.Util
{
    public static unsafe class Interop
    {
        public static int Popcount(Bitmask value)
        {
            return (int)Bitmask.PopCount(value);
        }

        public static int LSB(Bitmask value)
        {
            return (int)Bitmask.TrailingZeroCount(value);
        }

        public static int PopLSB(Bitmask* value)
        {
            int sq = (int)Bitmask.TrailingZeroCount(*value);
            *value = *value & *value - 1;
            return sq;
        }

        public static ulong Pext(Bitmask a, Bitmask mask)
        {
            int shift = (int)ulong.PopCount(mask.Lower());
            var lo = Bmi2.X64.ParallelBitExtract(a.Lower(), mask.Lower());
            var hi = Bmi2.X64.ParallelBitExtract(a.Upper(), mask.Upper());

            return hi << shift | lo;
        }


        public static unsafe void* AlignedAllocZeroed(nuint byteCount, nuint alignment = 64)
        {
            void* block = NativeMemory.AlignedAlloc(byteCount, alignment);
            NativeMemory.Clear(block, byteCount);

            return block;
        }

        public static unsafe T* AlignedAllocZeroed<T>(nuint items, nuint alignment = 64)
        {
            nuint bytes = (nuint)sizeof(T) * items;
            void* block = NativeMemory.AlignedAlloc(bytes, alignment);
            NativeMemory.Clear(block, bytes);

            return (T*)block;
        }
    }
}
