
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

using System.Runtime.Intrinsics;
using static Peeper.Logic.Evaluation.NetworkArchitecture;

namespace Peeper.Logic.Evaluation
{
    public readonly unsafe struct NetworkContainerT<T, U>
    {
        public readonly T* FTWeights;
        public readonly T* FTBiases;
        public readonly U* L1Weights;
        public readonly U* L1Biases;

        public NetworkContainerT()
        {
            FTWeights = AlignedAllocZeroed<T>(FT_SIZE * L1_SIZE * INPUT_BUCKETS);
            FTBiases = AlignedAllocZeroed<T>(L1_SIZE);

            L1Weights = AlignedAllocZeroed<U>(OUTPUT_BUCKETS * L1_SIZE * 2);
            L1Biases = AlignedAllocZeroed<U>((nuint)Math.Max(OUTPUT_BUCKETS, Vector256<short>.Count));
        }
    }

    
}
