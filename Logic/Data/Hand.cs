
using System.Text;

namespace Peeper.Logic.Data
{
    public unsafe struct Hand
    {
        public const int MaxHeld = 9 * ColorNB;

        public int Data { get; private set; }

        private const int PawnShift   = 0;
        private const int LanceShift  = PawnShift + 8;
        private const int KnightShift = LanceShift + 4;
        private const int SilverShift = KnightShift + 4;
        private const int BishopShift = SilverShift + 4;
        private const int RookShift   = BishopShift + 4;
        private const int GoldShift   = RookShift + 4;

        private const int PawnMask   = 0b11111 << PawnShift;
        private const int LanceMask  = 0b01111 << LanceShift;
        private const int KnightMask = 0b01111 << KnightShift;
        private const int SilverMask = 0b01111 << SilverShift;
        private const int BishopMask = 0b01111 << BishopShift;
        private const int RookMask   = 0b01111 << RookShift;
        private const int GoldMask   = 0b01111 << GoldShift;

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

        public int AddToHand(int type)
        {
            var n = NumHeld(type);
            SetNumHeld(type, n + 1);
            return n + 1;
        }

        public int TakeFromHand(int type)
        {
            var n = NumHeld(type);
            Assert(n > 0, $"TakeFromHand({type}) Type isn't in hand");
            SetNumHeld(type, n - 1);
            return n - 1;
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
                n = 1;
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

            int[] order = [Pawn, Lance, Knight, Silver, Bishop, Gold, Rook];
            foreach (int type in order)
            {
                int n = NumHeld(type);
                char c = PieceToSFenChar(color, type);

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
