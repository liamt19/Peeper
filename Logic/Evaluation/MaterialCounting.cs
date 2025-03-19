
namespace Peeper.Logic.Evaluation
{
    public static unsafe class MaterialCounting
    {
        private const int ValuePawn           = 100;
        private const int ValuePawnPromoted   = 350;
        private const int ValueLance          = 400;
        private const int ValueKnight         = 500;
        private const int ValueLancePromoted  = 790;
        private const int ValueKnightPromoted = 790;
        private const int ValueSilver         = 700;
        private const int ValueSilverPromoted = 790;
        private const int ValueGold           = 800;
        private const int ValueBishop         = 1100;
        private const int ValueRook           = 1300;
        private const int ValueBishopPromoted = 1500;
        private const int ValueRookPromoted   = 1700;

        //  PieceValue plus a small amount for normal pieces,
        //  and the value of the piece itself + the promoted value for captured promoted pieces.
        public static int GetHandValue(int type)
        {
            int v = GetPieceValue(type) + 20;
            if (IsPromoted(type))
            {
                v += GetPieceValue(Promote(type));
            }
            return v;
        }

        public static int GetPieceValue(int type)
        {
            return type switch
            {
                Pawn           => ValuePawn,
                PawnPromoted   => ValuePawnPromoted,
                Lance          => ValueLance,
                Knight         => ValueKnight,
                LancePromoted  => ValueLancePromoted,
                KnightPromoted => ValueKnightPromoted,
                Silver         => ValueSilver,
                SilverPromoted => ValueSilverPromoted,
                Gold           => ValueGold,
                Bishop         => ValueBishop,
                Rook           => ValueRook,
                BishopPromoted => ValueBishopPromoted,
                RookPromoted   => ValueRookPromoted,
                _ => 0
            };
        }


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
