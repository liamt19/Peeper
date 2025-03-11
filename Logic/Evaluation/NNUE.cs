using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Text;

using NetworkT = Peeper.Logic.Evaluation.NetworkContainerT<short, short>;

using Peeper.Logic.Evaluation;
using static Peeper.Logic.Evaluation.NNUEUtils;
using System.Reflection;

namespace Peeper.Logic.Evaluation
{
    public static unsafe class NNUE
    {
        public const int INPUT_BUCKETS = 1;
        public const int FT_SIZE = 2344;
        public const int L1_SIZE = 128;
        public const int OUTPUT_BUCKETS = 1;

        private const int FT_QUANT = 255;
        private const int L1_QUANT = 64;
        private const int OutputScale = 400;

        public static string NetworkName
        {
            get
            {
                try
                {
                    return Assembly.GetEntryAssembly()?.GetCustomAttribute<EvalFileAttribute>()?.EvalFile.Trim();
                }
                catch { return ""; }
            }
        }

        private const int N_FTW = FT_SIZE * L1_SIZE * INPUT_BUCKETS;
        private const int N_FTB = L1_SIZE;
        private const int N_L1W = OUTPUT_BUCKETS * L1_SIZE * 2;
        private const int N_L1B = OUTPUT_BUCKETS;

        private const long ExpectedNetworkSize = (N_FTW + N_FTB + N_L1W + N_L1B) * sizeof(short);

        private static readonly NetworkT Net;

        static NNUE()
        {
            Net = new NetworkT();
            Initialize(NetworkName);
        }

        public static void Initialize(string networkToLoad, bool exitIfFail = true)
        {
            using Stream netStream = NNUEUtils.TryOpenFile(networkToLoad, exitIfFail);

            BinaryReader br = new BinaryReader(netStream);

            long toRead = ExpectedNetworkSize;
            if (br.BaseStream.Position + toRead > br.BaseStream.Length)
            {
                Console.WriteLine("Bucketed768's BinaryReader doesn't have enough data for all weights and biases to be read!");
                Console.WriteLine($"It expects to read {toRead} bytes, but the stream's position is {br.BaseStream.Position} / {br.BaseStream.Length}");
                Console.WriteLine("The file being loaded is either not a valid 768 network, or has different layer sizes than the hardcoded ones.");
                if (exitIfFail)
                {
                    Environment.Exit(-1);
                }
                else
                {
                    return;
                }
            }

            for (int i = 0; i < N_FTW; i++) Net.FTWeights[i] = br.ReadInt16();
            for (int i = 0; i < N_FTB; i++) Net.FTBiases[i]  = br.ReadInt16();

            for (int i = 0; i < N_L1W; i++) Net.L1Weights[i] = br.ReadInt16();
            for (int i = 0; i < N_L1B; i++) Net.L1Biases[i]  = br.ReadInt16();

            br.Dispose();

            //TransposeLayerWeights((short*)Net.L1Weights, L1_SIZE, OUTPUT_BUCKETS);
        }


        public static void _RefreshAccumulator(Position pos)
        {
            _RefreshAccumulatorPerspectiveFull(pos, Black);
            _RefreshAccumulatorPerspectiveFull(pos, White);
        }

        public static void _RefreshAccumulatorPerspectiveFull(Position pos, int perspective)
        {
            ref Accumulator accumulator = ref *pos.State->Accumulator;
            ref Bitboard bb = ref pos.bb;

            var acc = (short*)accumulator[perspective];
            Unsafe.CopyBlock(acc, Net.FTBiases, Accumulator.ByteSize);
            accumulator.NeedsRefresh[perspective] = false;
            accumulator.Computed[perspective] = true;

            int ourKing = pos.State->KingSquares[perspective];
            var occ = bb.Occupancy;
            while (occ != 0)
            {
                int sq = PopLSB(&occ);

                int type = bb.GetPieceAtIndex(sq);
                int color = bb.GetColorAtIndex(sq);

                int idx = BoardFeatureIndex(color, type, sq, perspective);
                AddFeature(acc, acc, idx);
            }


#if NO
            if (pos.Owner.CachedBuckets == null)
            {
                //  TODO: Upon SearchThread init, this isn't created yet :(
                return;
            }

            ref BucketCache cache = ref pos.Owner.CachedBuckets[BucketForPerspective(ourKing, perspective)];
            ref Bitboard entryBB = ref cache.Boards[perspective];
            ref Accumulator entryAcc = ref cache.Accumulator;

            accumulator.CopyTo(ref entryAcc, perspective);
            bb.CopyTo(ref entryBB);
#endif
        }

        public static void RefreshAccumulator(Position pos)
        {
            ref Accumulator accumulator = ref *pos.State->Accumulator;
            ref Bitboard bb = ref pos.bb;

            var bAcc = (short*)accumulator[Black];
            var wAcc = (short*)accumulator[White];
            Unsafe.CopyBlock(bAcc, Net.FTBiases, Accumulator.ByteSize);
            Unsafe.CopyBlock(wAcc, Net.FTBiases, Accumulator.ByteSize);
            accumulator.NeedsRefresh[Black] = accumulator.NeedsRefresh[White] = false;
            accumulator.Computed[Black] = accumulator.Computed[White] = true;

            var occ = bb.Occupancy;
            while (occ != 0)
            {
                int sq = PopLSB(&occ);

                int type = bb.GetPieceAtIndex(sq);
                int color = bb.GetColorAtIndex(sq);

                AddFeature(bAcc, bAcc, BoardFeatureIndex(color, type, sq, Black));
                AddFeature(wAcc, wAcc, BoardFeatureIndex(color, type, sq, White));
            }

            var hands = pos.State->Hands;
            for (int handColor = 0; handColor < ColorNB; handColor++)
            {
                var thisHand = hands[handColor];

                foreach (var type in DroppableTypes)
                {
                    int count = thisHand.NumHeld(type);
                    for (int featureCount = 0; featureCount < count; featureCount++)
                    {
                        var b = HandFeatureIndex(handColor, type, featureCount, Black);
                        var w = HandFeatureIndex(handColor, type, featureCount, White);

                        AddFeature(bAcc, bAcc, b);
                        AddFeature(wAcc, wAcc, w);
                    }
                }
            }

#if NO
            if (pos.Owner.CachedBuckets == null)
            {
                //  TODO: Upon SearchThread init, this isn't created yet :(
                return;
            }

            ref BucketCache cache = ref pos.Owner.CachedBuckets[BucketForPerspective(ourKing, perspective)];
            ref Bitboard entryBB = ref cache.Boards[perspective];
            ref Accumulator entryAcc = ref cache.Accumulator;

            accumulator.CopyTo(ref entryAcc, perspective);
            bb.CopyTo(ref entryBB);
#endif
        }


        public static int GetEvaluation(Position pos)
        {
            RefreshAccumulator(pos);
            ref Accumulator accumulator = ref *pos.State->Accumulator;

            Vector256<short> maxVec = Vector256.Create((short)FT_QUANT);
            Vector256<short> zeroVec = Vector256<short>.Zero;
            Vector256<int> sum = Vector256<int>.Zero;

            int SimdChunks = L1_SIZE / Vector256<short>.Count;

            const int outputBucket = 0;

            var us = pos.ToMove;

            var stm  = accumulator[us];
            var nstm = accumulator[Not(us)];
            var stmWeights  = (Vector256<short>*)(&Net.L1Weights[outputBucket * (L1_SIZE * 2)]);
            var nstmWeights = (Vector256<short>*)(&Net.L1Weights[outputBucket * (L1_SIZE * 2) + L1_SIZE]);

            for (int i = 0; i < SimdChunks; i++)
            {
                Vector256<short> clamp = Vector256.Min(maxVec, Vector256.Max(zeroVec, stm[i]));
                Vector256<short> mult = clamp * stmWeights[i];

                (var mLo, var mHi) = Vector256.Widen(mult);
                (var cLo, var cHi) = Vector256.Widen(clamp);

                sum = Vector256.Add(sum, Vector256.Add(mLo * cLo, mHi * cHi));
            }

            for (int i = 0; i < SimdChunks; i++)
            {
                Vector256<short> clamp = Vector256.Min(maxVec, Vector256.Max(zeroVec, nstm[i]));
                Vector256<short> mult = clamp * nstmWeights[i];

                (var mLo, var mHi) = Vector256.Widen(mult);
                (var cLo, var cHi) = Vector256.Widen(clamp);

                sum = Vector256.Add(sum, Vector256.Add(mLo * cLo, mHi * cHi));
            }

            int output = Vector256.Sum(sum);

            return (output / FT_QUANT + Net.L1Biases[outputBucket]) * OutputScale / (FT_QUANT * L1_QUANT);
        }

        public static void AddFeature(short* src, short* dst, int offset)
        {
            var weights = &Net.FTWeights[offset * L1_SIZE];
            for (int i = 0; i < L1_SIZE; i++)
            {
                dst[i] = (short)(src[i] + weights[i]);
            }
        }

        public static void RemoveFeature(short* src, short* dst, int offset)
        {
            var weights = &Net.FTWeights[offset * L1_SIZE];
            for (int i = 0; i < L1_SIZE; i++)
            {
                dst[i] = (short)(src[i] - weights[i]);
            }
        }

    }
}
