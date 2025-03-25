
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Data
{
    public unsafe struct Hand
    {
        public const int MaxHeld = 9 * ColorNB;

        public int Data { get; private set; }

        private const int PawnBits   = 5;
        private const int LanceBits  = 3;
        private const int KnightBits = 3;
        private const int SilverBits = 3;
        private const int GoldBits   = 3;
        private const int BishopBits = 2;
        private const int RookBits   = 2;

        private const int PawnShift   = 0;
        private const int LanceShift  = PawnShift + PawnBits;
        private const int KnightShift = LanceShift + LanceBits;
        private const int SilverShift = KnightShift + KnightBits;
        private const int GoldShift   = SilverShift + SilverBits;
        private const int BishopShift = GoldShift + GoldBits;
        private const int RookShift   = BishopShift + BishopBits;

        private const int PawnMask   = ((1 << PawnBits) - 1) << PawnShift;
        private const int LanceMask  = ((1 << LanceBits) - 1) << LanceShift;
        private const int KnightMask = ((1 << KnightBits) - 1) << KnightShift;
        private const int SilverMask = ((1 << SilverBits) - 1) << SilverShift;
        private const int GoldMask   = ((1 << GoldBits) - 1) << GoldShift;
        private const int BishopMask = ((1 << BishopBits) - 1) << BishopShift;
        private const int RookMask   = ((1 << RookBits) - 1) << RookShift;

        public Hand()
        {
            Clear();
        }

        public void Clear() => Data = 0;
        public bool IsEmpty => (Data == 0);

        [MethodImpl(Inline)]
        public readonly int NumHeld(int type)
        {
            var mask = MaskFor(type);
            var shift = ShiftFor(type);
            return (Data & mask) >> shift; 
        }

        [MethodImpl(Inline)]
        public void SetNumHeld(int type, int n)
        {
            var mask = MaskFor(type);
            var shift = ShiftFor(type);
            Data = (Data & ~mask) | (n << shift);
        }

        [MethodImpl(Inline)]
        public int AddToHand(int type)
        {
            var n = NumHeld(type);
            SetNumHeld(type, n + 1);
            return n + 1;
        }

        [MethodImpl(Inline)]
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
            return NumHeld(Pawn) + NumHeld(Lance) + NumHeld(Knight) + NumHeld(Silver) + NumHeld(Gold) + NumHeld(Bishop) + NumHeld(Rook);
        }


        private static ReadOnlySpan<int> _ShiftFor => [PawnShift, 0, LanceShift, KnightShift, 0, 0, SilverShift, 0, GoldShift, BishopShift, RookShift, 0, 0, 0];
        private static int ShiftFor(int type)
        {
            return _ShiftFor[type];
        }

        private static ReadOnlySpan<int> _MaskFor => [PawnMask, 0, LanceMask, KnightMask, 0, 0, SilverMask, 0, GoldMask, BishopMask, RookMask, 0, 0, 0];
        private static int MaskFor(int type)
        {
            return _MaskFor[type];
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
