
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
        public static Move MakeDrop(int type, int sq) => new(DropSourceSquare, sq, DropFlagFor(type));
        public static Move MakePromo(int from, int to) => new(from, to, Move.FlagPromotion);
        public static Move MakeNormal(int from, int to) => new(from, to);

        public readonly uint Data { get; } = (uint)((from << 0) | (to << 8) | flags);

        private const int Mask_ToFrom = 0xFFFF;
        private const int DropShift = 18;
        private const int PromoShift = 24;

        public const int FlagPromotion = 0b1 << PromoShift;

        private const int FlagDrop = 0b10000 << DropShift;
        public const int FlagDropPawn   = FlagDrop | ((Piece.Pawn) << DropShift);
        public const int FlagDropLance  = FlagDrop | ((Piece.Lance) << DropShift);
        public const int FlagDropKnight = FlagDrop | ((Piece.Knight) << DropShift);
        public const int FlagDropSilver = FlagDrop | ((Piece.Silver) << DropShift);
        public const int FlagDropBishop = FlagDrop | ((Piece.Bishop) << DropShift);
        public const int FlagDropRook   = FlagDrop | ((Piece.Rook) << DropShift);
        public const int FlagDropGold   = FlagDrop | ((Piece.Gold) << DropShift);

        public readonly int From => (int)((Data >> 0) & 0b1111111);
        public readonly int To => (int)((Data >> 8) & 0b1111111);

        public (int from, int to) Unpack() => (From, To);

        public readonly bool IsDrop => (Data & FlagDrop) != 0;
        public readonly int DroppedPiece => (int)((Data >> DropShift) & 0b1111);

        public readonly bool IsPromotion => (Data & FlagPromotion) != 0;

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

        public static int DropFlagFor(int type)
        {
            return type switch
            {
                Pawn => FlagDropPawn,
                Lance => FlagDropLance,
                Knight => FlagDropKnight,
                Silver => FlagDropSilver,
                Bishop => FlagDropBishop,
                Rook => FlagDropRook,
                Gold => FlagDropGold,
                _ => 0
            };
        }

        public static bool operator ==(Move left, Move right) => left.Equals(right);
        public static bool operator !=(Move left, Move right) => !left.Equals(right);

        public static bool operator ==(Move left, ScoredMove right) => left.Equals(right);
        public static bool operator !=(Move left, ScoredMove right) => !left.Equals(right);
    }
}
