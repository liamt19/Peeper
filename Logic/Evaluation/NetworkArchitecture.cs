using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using static Peeper.Logic.Evaluation.NNUE;

namespace Peeper.Logic.Evaluation
{
    public static class NetworkArchitecture
    {
        public const int INPUT_BUCKETS = 1;
        public const int FT_SIZE = 2344;
        public const int L1_SIZE = 128;
        public const int OUTPUT_BUCKETS = 8;

        public const int FT_QUANT = 255;
        public const int L1_QUANT = 64;
        public const int OutputScale = 400;

        public const int N_FTW = FT_SIZE * L1_SIZE * INPUT_BUCKETS;
        public const int N_FTB = L1_SIZE;
        public const int N_L1W = OUTPUT_BUCKETS * L1_SIZE * 2;
        public const int N_L1B = OUTPUT_BUCKETS;

        public const long ExpectedNetworkSize = (N_FTW + N_FTB + N_L1W + N_L1B) * sizeof(short);

        [MethodImpl(Inline)]
        private static int HandOffset(int type)
        {
            return type switch
            {
                Pawn   => 0,
                Lance  => 18,
                Knight => 22,
                Silver => 26,
                Gold   => 30,
                Bishop => 24,
                Rook   => 36,
                _      => int.MaxValue
            };
        }

        [MethodImpl(Inline)]
        public static int Rotate(int sq) => 80 - sq;

        [MethodImpl(Inline)]
        public static int Orient(int sq, int perspective) => (perspective == Black ? sq : Rotate(sq));


        private const int TYPE_STRIDE = SquareNB;
        private const int HAND_STRIDE = TYPE_STRIDE * PieceNB;
        private const int COLOR_STRIDE = HAND_STRIDE + 38;
        
        public static int BoardFeatureIndexSingle(int color, int type, int sq, int perspective)
        {
            sq = Orient(sq, perspective);
            return (((color ^ perspective) * COLOR_STRIDE) + (type * TYPE_STRIDE) + sq) * L1_SIZE;
        }

        public static int HandFeatureIndexSingle(int handColor, int type, int held, int perspective)
        {
            return (((handColor ^ perspective) * COLOR_STRIDE) + HAND_STRIDE + HandOffset(type) + held) * L1_SIZE;
        }

        public static (int bIdx, int wIdx) BoardFeatureIndex(int color, int type, int sq)
        {
            return (BoardFeatureIndexSingle(color, type, sq, Black), BoardFeatureIndexSingle(color, type, sq, White));
        }

        public static (int bIdx, int wIdx) HandFeatureIndex(int handColor, int type, int held)
        {
            return (HandFeatureIndexSingle(handColor, type, held, Black), HandFeatureIndexSingle(handColor, type, held, White));
        }

    }
}
