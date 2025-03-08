using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Data
{
    public readonly struct Move(int from, int to, int flags = 0)
    {
        public static readonly Move Null = new Move();

        private readonly uint _data = (uint)((from << 0) | (to << 8) | flags);

        [MethodImpl(Inline)]
        public uint GetData() => _data;

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

        public readonly int From => (int)((_data >> 0) & 0b1111111);
        public readonly int To => (int)((_data >> 8) & 0b1111111);

        public (int from, int to) Unpack() => (From, To);

        public readonly bool IsDrop => (_data & FlagDrop) != 0;
        public readonly int DroppedPiece => (int)((_data >> DropShift) & 0b1111);

        public readonly bool IsPromotion => (_data & FlagPromotion) != 0;

        [MethodImpl(Inline)]
        public readonly bool IsNull() => (_data & Mask_ToFrom) == 0;


        public override string ToString()
        {
            if (IsDrop)
            {
                return $"{PieceToSFenChar(DroppedPiece)}*{IndexToString(To)}";
            }

            return $"{IndexToString(From)}{IndexToString(To)}{(IsPromotion ? "+" : "")}";
        }


        [MethodImpl(Inline)]
        public bool Equals(Move move) => move.GetData() == GetData();


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
            };
        }

        public static bool operator ==(Move left, Move right) => left.Equals(right);
        public static bool operator !=(Move left, Move right) => !left.Equals(right);

        public static bool operator ==(Move left, ScoredMove right) => left.Equals(right);
        public static bool operator !=(Move left, ScoredMove right) => !left.Equals(right);
    }
}
