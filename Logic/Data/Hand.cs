
using System.Text;

namespace Peeper.Logic.Data
{
    public unsafe struct Hand
    {
        public int Data { get; private set; }

        private const int PawnShift   =  0;
        private const int LanceShift  =  4;
        private const int KnightShift =  8;
        private const int SilverShift = 12;
        private const int BishopShift = 26;
        private const int RookShift   = 20;
        private const int GoldShift   = 24;

        private const int PawnMask   = 0b1111 << PawnShift;
        private const int LanceMask  = 0b1111 << LanceShift;
        private const int KnightMask = 0b1111 << KnightShift;
        private const int SilverMask = 0b1111 << SilverShift;
        private const int BishopMask = 0b1111 << BishopShift;
        private const int RookMask   = 0b1111 << RookShift;
        private const int GoldMask   = 0b1111 << GoldShift;

        public Hand()
        {
            Clear();
        }

        public void Clear() => Data = 0;
        public bool IsEmpty => (Data == 0);

        public int NumHeld(int type)
        {
            var mask = MaskFor(type);
            var shift = ShiftFor(type);
            return (Data & mask) >> shift; 
        }

        public void SetNumHeld(int type, int n)
        {
            var mask = MaskFor(type);
            var shift = ShiftFor(type);
            Data = (Data & ~mask) | (n << shift);
        }

        public void AddToHand(int type)
        {
            var n = NumHeld(type);
            SetNumHeld(type, n + 1);
        }

        public void TakeFromHand(int type)
        {
            var n = NumHeld(type);
            Assert(n > 0, $"TakeFromHand({type}) Type isn't in hand");
            SetNumHeld(type, n - 1);
        }

        public void SetFromSFen(string handStr, int color)
        {
            if (handStr == "-")
                return;

            int n = 1;
            foreach (char c in handStr)
            {
                if (char.IsDigit(c))
                {
                    n = int.Parse(c.ToString());
                    continue;
                }

                if (color == Black && char.IsLower(c) ||
                    color == White && char.IsUpper(c))
                {
                    n = 1;
                    continue;
                }

                SetNumHeld(SFenToPiece(c), n);
            }
        }

        public int GetTotalHeld()
        {
            return NumHeld(Pawn) + NumHeld(Lance) + NumHeld(Knight) + NumHeld(Silver) + NumHeld(Bishop) + NumHeld(Rook) + NumHeld(Gold);
        }

        private static int ShiftFor(int type)
        {
            return type switch
            {
                Pawn    => PawnShift,
                Lance   => LanceShift,
                Knight  => KnightShift,
                Silver  => SilverShift,
                Bishop  => BishopShift,
                Rook    => RookShift,
                Gold    => GoldShift,
                _       => 0
            };
        }

        private static int MaskFor(int type)
        {
            return type switch
            {
                Pawn    => PawnMask,
                Lance   => LanceMask,
                Knight  => KnightMask,
                Silver  => SilverMask,
                Bishop  => BishopMask,
                Rook    => RookMask,
                Gold    => GoldMask,
                _       => 0
            };
        }

        public string ToString(int color)
        {
            StringBuilder sb = new StringBuilder();

            //  pieces are always listed in the order rook, bishop, gold, silver, knight, lance, pawn
            int[] order = [Rook, Bishop, Gold, Silver, Knight, Lance, Pawn];
            foreach (int type in order)
            {
                int n = NumHeld(type);
                char c = PieceToSFenChar(type);

                if (n > 1)
                {
                    sb.Append(n);
                    sb.Append(c);
                }
                else if (n == 1)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
