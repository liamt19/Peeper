
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

using System.Runtime.InteropServices;
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
            FTWeights = AlignedAllocZeroed<T>(N_FTW);
            FTBiases = AlignedAllocZeroed<T>(N_FTB);

            L1Weights = AlignedAllocZeroed<U>(N_L1W);
            L1Biases = AlignedAllocZeroed<U>((nuint)CeilToMultiple(OUTPUT_BUCKETS, Vector256<short>.Count));
        }

        public void ReadFrom(BinaryReader br)
        {
            for (int i = 0; i < N_FTW; i++) FTWeights[i] = Read<T>(br);
            for (int i = 0; i < N_FTB; i++) FTBiases[i]  = Read<T>(br);

            for (int i = 0; i < N_L1W; i++) L1Weights[i] = Read<U>(br);
            for (int i = 0; i < N_L1B; i++) L1Biases[i]  = Read<U>(br);
        }

        private static _T Read<_T>(BinaryReader br)
        {
            object o = Type.GetTypeCode(typeof(_T)) switch
            {
                TypeCode.SByte => br.ReadSByte(),
                TypeCode.Int16 => br.ReadInt16(),
                TypeCode.Int32 => br.ReadInt32(),
                TypeCode.Single => br.ReadSingle(),
                _ => (object)br.ReadInt16(),
            };

            return (_T)o;
        }
    }

    
}
