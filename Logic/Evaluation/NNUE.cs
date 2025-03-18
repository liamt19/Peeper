using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Text;

using NetworkT = Peeper.Logic.Evaluation.NetworkContainerT<short, short>;

using static Peeper.Logic.Evaluation.FunUnrollThings;
using static Peeper.Logic.Evaluation.NNUEUtils;

using System.Reflection;
using Peeper.Logic.NN;
using System.Runtime.CompilerServices;
using Peeper.Logic.Search;


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


        public static void RefreshAccumulator(Position pos)
        {
            RefreshAccumulatorPerspective(pos, Black);
            RefreshAccumulatorPerspective(pos, White);
        }

        public static void RefreshAccumulatorPerspective(Position pos, int perspective)
        {
            ref Accumulator accumulator = ref *pos.State->Accumulator;
            ref Bitboard bb = ref pos.bb;

            var acc = (short*)accumulator[perspective];
            Unsafe.CopyBlock(acc, Net.FTBiases, Accumulator.ByteSize);
            accumulator.NeedsRefresh[perspective] = false;
            accumulator.Computed[perspective] = true;

            var occ = bb.Occupancy;
            while (occ != 0)
            {
                int sq = PopLSB(&occ);

                int type = bb.GetPieceAtIndex(sq);
                int color = bb.GetColorAtIndex(sq);

                int idx = BoardFeatureIndexSingle(color, type, sq, perspective);
                UnrollAdd(acc, acc, &Net.FTWeights[idx]);
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
                        int idx = HandFeatureIndexSingle(handColor, type, featureCount, perspective);
                        UnrollAdd(acc, acc, &Net.FTWeights[idx]);
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

            accumulator.Computed[perspective] = true;
        }


        public static short GetEvaluation(Position pos)
        {
            int ev = Evaluate(pos);
            ev = int.Clamp(ev, -NNUEAbsMax, NNUEAbsMax);
            return (short)ev;
        }

        private static int Evaluate(Position pos)
        {
            ref Accumulator accumulator = ref *pos.State->Accumulator;
            ProcessUpdates(pos);

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


        public static void MakeMove(Position pos, Move m)
        {
            ref Bitboard bb = ref pos.bb;

            Accumulator* src = pos.State->Accumulator;
            Accumulator* dst = pos.NextState->Accumulator;

            dst->NeedsRefresh[0] = src->NeedsRefresh[0];
            dst->NeedsRefresh[1] = src->NeedsRefresh[1];

            dst->Computed[0] = dst->Computed[1] = false;

            var (moveFrom, moveTo) = m.Unpack();

            int us = pos.ToMove;
            int ourPiece = pos.MovedPiece(m);

            int them = Not(us);
            int theirPiece = bb.GetPieceAtIndex(moveTo);

            ref PerspectiveUpdate bUpdate = ref dst->Update[Black];
            ref PerspectiveUpdate wUpdate = ref dst->Update[White];

            //  Remove any updates that are present
            bUpdate.Clear();
            wUpdate.Clear();

            var (bFrom, wFrom) = BoardFeatureIndex(us, ourPiece, moveFrom);
            var (bTo, wTo) = BoardFeatureIndex(us, m.IsPromotion ? Piece.Promote(ourPiece) : ourPiece, moveTo);

            if (m.IsDrop)
            {
                var held = pos.State->Hands[us].NumHeld(ourPiece) - 1;
                (bFrom, wFrom) = HandFeatureIndex(us, ourPiece, held);
                (bTo, wTo) = BoardFeatureIndex(us, ourPiece, moveTo);

                bUpdate.PushSubAdd(bFrom, bTo);
                wUpdate.PushSubAdd(wFrom, wTo);
            }
            else if (theirPiece != None)
            {
                var (bCap, wCap) = BoardFeatureIndex(them, theirPiece, moveTo);

                theirPiece = DemoteMaybe(theirPiece);
                int held = pos.State->Hands[us].NumHeld(theirPiece);
                var (bHand, wHand) = HandFeatureIndex(us, theirPiece, held);

                bUpdate.PushSubSubAddAdd(bFrom, bCap, bTo, bHand);
                wUpdate.PushSubSubAddAdd(wFrom, wCap, wTo, wHand);
            }
            else
            {
                bUpdate.PushSubAdd(bFrom, bTo);
                wUpdate.PushSubAdd(wFrom, wTo);
            }
        }


        [MethodImpl(Inline)]
        public static void MakeNullMove(Position pos)
        {
            var currAcc = pos.State->Accumulator;
            var nextAcc = pos.NextState->Accumulator;

            currAcc->CopyTo(nextAcc);

            nextAcc->Computed[Black] = currAcc->Computed[Black];
            nextAcc->Computed[White] = currAcc->Computed[White];
            nextAcc->Update[Black].Clear();
            nextAcc->Update[White].Clear();
        }

        [MethodImpl(Inline)]
        public static void ProcessUpdates(Position pos)
        {
            BoardState* st = pos.State;
            for (int perspective = 0; perspective < 2; perspective++)
            {
                //  If the current state is correct for our perspective, no work is needed
                if (st->Accumulator->Computed[perspective])
                    continue;

                //  If the current state needs a refresh, don't bother with previous states
                if (st->Accumulator->NeedsRefresh[perspective])
                {
                    RefreshAccumulatorPerspective(pos, perspective);
                    continue;
                }

                //  Find the most recent computed or refresh-needed accumulator
                BoardState* curr = st - 1;
                while (!curr->Accumulator->Computed[perspective] && !curr->Accumulator->NeedsRefresh[perspective])
                    curr--;

                if (curr->Accumulator->NeedsRefresh[perspective])
                {
                    //  The most recent accumulator would need to be refreshed,
                    //  so don't bother and refresh the current one instead
                    RefreshAccumulatorPerspective(pos, perspective);
                }
                else
                {
                    //  Update incrementally till the current accumulator is correct
                    while (curr != st)
                    {
                        BoardState* prev = curr;
                        curr++;
                        UpdateSingle(prev->Accumulator, curr->Accumulator, perspective);
                    }
                }

            }
        }

        [MethodImpl(Inline)]
        public static void UpdateSingle(Accumulator* prev, Accumulator* curr, int perspective)
        {
            ref var updates = ref curr->Update[perspective];

            if (updates.AddCnt == 0 && updates.SubCnt == 0)
            {
                //  For null moves, we still need to carry forward the correct accumulator state
                prev->CopyTo(ref *curr, perspective);
                return;
            }

            var src = (short*)((*prev)[perspective]);
            var dst = (short*)((*curr)[perspective]);

            var FeatureWeights = Net.FTWeights;

            if (updates.AddCnt == 1 && updates.SubCnt == 1)
            {
                SubAdd(src, dst,
                    &FeatureWeights[updates.Subs[0]],
                    &FeatureWeights[updates.Adds[0]]);
            }
            else if (updates.AddCnt == 2 && updates.SubCnt == 2)
            {
                SubSubAddAdd(src, dst,
                    &FeatureWeights[updates.Subs[0]],
                    &FeatureWeights[updates.Subs[1]],
                    &FeatureWeights[updates.Adds[0]],
                    &FeatureWeights[updates.Adds[1]]);
            }

            curr->Computed[perspective] = true;
        }




        public static void AddFeature(short* src, short* dst, int offset)
        {
            var weights = &Net.FTWeights[offset];
            for (int i = 0; i < L1_SIZE; i++)
            {
                dst[i] = (short)(src[i] + weights[i]);
            }
        }

        public static void RemoveFeature(short* src, short* dst, int offset)
        {
            var weights = &Net.FTWeights[offset];
            for (int i = 0; i < L1_SIZE; i++)
            {
                dst[i] = (short)(src[i] - weights[i]);
            }
        }

        private static string Debug_GetAccumulatorStatus(Position pos)
        {
            StringBuilder sb = new StringBuilder();
            BoardState* st = pos.State;
            BoardState* first = pos.StartingState;

            int dist = (int)(st - first);
            int i = 0;
            do
            {
                var acc = st->Accumulator;
                sb.Append(i == 0 ? '*' : ' ');
                sb.Append($"{i,2}\t");
                sb.Append($"Computed: {(acc->Computed[0] ? 1 : 0)}/{(acc->Computed[1] ? 1 : 0)}\t");
                sb.Append($"Refresh: {(acc->NeedsRefresh[0] ? 1 : 0)}/{(acc->NeedsRefresh[1] ? 1 : 0)}\t");
                sb.Append($"<{acc->Black[0]} {acc->Black[1]} {acc->Black[2]} {acc->Black[3]}>\t");
                sb.Append($"<{acc->White[0]} {acc->White[1]} {acc->White[2]} {acc->White[3]}>");
                sb.AppendLine();
                st--;
                i++;
            } while (st != first);

            return sb.ToString();
        }
    }
}
