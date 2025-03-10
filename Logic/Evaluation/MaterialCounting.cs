
namespace Peeper.Logic.Evaluation
{
    public static unsafe class MaterialCounting
    {
        private static ReadOnlySpan<int> PieceValues =>
        [
            100, 400, 500, 700, 1100, 1300,
            800, 790, 790, 790, 1500, 1700,
            800, 
            0,
        ];

        //  PieceValue plus a small amount for normal pieces,
        //  and the value of the piece itself + the promoted value for captured promoted pieces.
        private static ReadOnlySpan<int> HandValues =>
        [
            120,  420,  520,  800, 1400, 1600,
            900, 1190, 1290, 1490, 2600, 3000,
            750,
            0,
        ];

        public static int GetHandValue(int type) => HandValues[type];
        public static int GetPieceValue(int type) => PieceValues[type];


        public static int GetMaterial(Position pos)
        {
            ref Bitboard bb = ref pos.bb;
            var stm = pos.ToMove;
            int mat = 0;

            var hands = pos.State->Hands;

            var mask = bb.Colors[stm];
            while (mask != 0)
            {
                int sq = PopLSB(&mask);
                mat += GetPieceValue(bb.GetPieceAtIndex(sq));
            }
            
            mask = bb.Colors[Not(stm)];
            while (mask != 0)
            {
                int sq = PopLSB(&mask);
                mat -= GetPieceValue(bb.GetPieceAtIndex(sq));
            }

            for(int type = 0; type < PieceNB; type++)
            {
                int d = hands[stm].NumHeld(type) - hands[Not(stm)].NumHeld(type);

                mat += (GetHandValue(type) * d);
            }

            mat *= (stm == Black) ? 1 : -1;

            return mat;
        }

    }
}
