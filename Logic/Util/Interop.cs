
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Peeper.Logic.Util
{
    public static unsafe class Interop
    {

        [MethodImpl(Inline)]
        public static bool MoreThanOne(Bitmask value) => (value & (value - 1)) != 0;

        [MethodImpl(Inline)]
        public static int Popcount(Bitmask value) => (int)Bitmask.PopCount(value);

        [MethodImpl(Inline)]
        public static int LSB(Bitmask value) => (int)Bitmask.TrailingZeroCount(value);

        [MethodImpl(Inline)]
        public static int PopLSB(Bitmask* value)
        {
            int sq = LSB(*value);
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
