
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()

using Peeper.Logic.Protocols;
using Peeper.Logic.USI;
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Data
{
    public readonly struct Move(int from, int to, int flags = 0)
    {
        public static readonly Move Null = new Move();
        public static Move MakeDrop(int type, int sq) => new(Piece.ToDropIndex(type), sq, Move.FlagDrop);
        public static Move MakePromo(int from, int to) => new(from, to, Move.FlagPromotion);
        public static Move MakeNormal(int from, int to) => new(from, to);

        public readonly ushort Data { get; } = (ushort)((to << 0) | (from << 7) | flags);

        private const int PromoShift = 14;
        private const int DropShift = 15;

        public const int FlagPromotion = 1 << PromoShift;
        private const int FlagDrop = 1 << DropShift;
        private const int Mask_ToFrom = (1 << 14) - 1;

        public readonly int To => (int)((Data >> 0) & 127);
        public readonly int From => (int)((Data >> 7) & 127);
        public readonly int MoveMask => (To * SquareNB + From);

        public (int from, int to) Unpack() => (From, To);

        public readonly bool IsDrop => (Data & FlagDrop) != 0;
        public readonly bool IsPromotion => (Data & FlagPromotion) != 0;
        public readonly int DroppedPiece => Piece.FromDropIndex(From);


        [MethodImpl(Inline)]
        public readonly bool IsNull() => (Data & Mask_ToFrom) == 0;


        public override string ToString()
        {
            return ActiveFormatter.FormatMove(this);
        }

        public static Move FromString(string str)
        {
            return ActiveFormatter.ParseMove(str);
        }


        [MethodImpl(Inline)]
        public bool Equals(Move move) => move.Data == Data;


        [MethodImpl(Inline)]
        public bool Equals(ScoredMove move) => move.Move.Equals(this);

        public static bool operator ==(Move left, Move right) => left.Equals(right);
        public static bool operator !=(Move left, Move right) => !left.Equals(right);

        public static bool operator ==(Move left, ScoredMove right) => left.Equals(right);
        public static bool operator !=(Move left, ScoredMove right) => !left.Equals(right);

        public static implicit operator bool(Move m) => !m.IsNull();
    }
}
