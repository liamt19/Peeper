
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

using Peeper.Logic.NN;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;


namespace Peeper.Logic.Evaluation
{
    public unsafe struct Accumulator
    {
        public const int ByteSize = NetworkArchitecture.L1_SIZE * sizeof(short);

        public readonly short* Black;
        public readonly short* White;

        public fixed bool NeedsRefresh[2];
        public fixed bool Computed[2];
        public NetworkUpdate Update;

        public Accumulator()
        {
            Black = AlignedAllocZeroed<short>(NetworkArchitecture.L1_SIZE);
            White = AlignedAllocZeroed<short>(NetworkArchitecture.L1_SIZE);

            NeedsRefresh[Color.Black] = NeedsRefresh[Color.White] = true;
            Computed[Color.Black] = Computed[Color.White] = false;
        }

        public Vector256<short>* this[int perspective] => (perspective == Color.Black) ? (Vector256<short>*)Black : (Vector256<short>*)White;

        [MethodImpl(Inline)]
        public void CopyTo(Accumulator* target)
        {
            Unsafe.CopyBlock(target->Black, Black, ByteSize);
            Unsafe.CopyBlock(target->White, White, ByteSize);

            target->NeedsRefresh[0] = NeedsRefresh[0];
            target->NeedsRefresh[1] = NeedsRefresh[1];

        }

        [MethodImpl(Inline)]
        public void CopyTo(ref Accumulator target, int perspective)
        {
            Unsafe.CopyBlock(target[perspective], this[perspective], ByteSize);
            target.NeedsRefresh[perspective] = NeedsRefresh[perspective];
        }

        public void ResetWithBiases(short* biases, uint byteCount)
        {
            Unsafe.CopyBlock(Black, biases, byteCount);
            Unsafe.CopyBlock(White, biases, byteCount);
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(Black);
            NativeMemory.AlignedFree(White);
        }

        public void MarkDirty()
        {
            NeedsRefresh[0] = NeedsRefresh[1] = true;
            Computed[0] = Computed[1] = false;
        }
    }
}
