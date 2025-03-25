using Peeper.Logic.Data;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace Peeper.Logic.Magic
{
    public static unsafe class MagicBitboards
    {
        public const nuint ROOK_TABLE_SIZE = 0x79000;
        public const nuint BISH_TABLE_SIZE = 0x4F00;
        public const nuint LANCE_TABLE_SIZE = 0x900;

        private static readonly Bitmask* BishopTable;
        private static readonly Bitmask* RookTable;
        private static readonly MagicSquare* BishopMagics;
        private static readonly MagicSquare* RookMagics;

        private static readonly Bitmask* BlackLanceTable;
        private static readonly Bitmask* WhiteLanceTable;
        private static readonly MagicSquare* BlackLanceMagics;
        private static readonly MagicSquare* WhiteLanceMagics;

        static MagicBitboards()
        {
            BishopTable = AlignedAllocZeroed<Bitmask>(BISH_TABLE_SIZE);
            RookTable = AlignedAllocZeroed<Bitmask>(ROOK_TABLE_SIZE);

            BishopMagics = InitializeMagics(Bishop, BishopTable);
            RookMagics = InitializeMagics(Rook, RookTable);

            BlackLanceTable = AlignedAllocZeroed<Bitmask>(LANCE_TABLE_SIZE);
            WhiteLanceTable = AlignedAllocZeroed<Bitmask>(LANCE_TABLE_SIZE);

            BlackLanceMagics = InitializeLanceMagics(Black, BlackLanceTable);
            WhiteLanceMagics = InitializeLanceMagics(White, WhiteLanceTable);
        }

        [MethodImpl(Inline)]
        public static Bitmask GetRookMoves(int sq, Bitmask occ)
        {
            ref MagicSquare m = ref RookMagics[sq];
            return m.attacks[Pext(occ, m.mask)];
        }

        [MethodImpl(InlineMaybe)]
        public static Bitmask GetBishopMoves(int sq, Bitmask occ)
        {
            ref MagicSquare m = ref BishopMagics[sq];
            return m.attacks[Pext(occ, m.mask)];
        }

        public static Bitmask GetPromotedRookMoves(int sq, Bitmask occ) => GetRookMoves(sq, occ) | KingMoveMask(sq);
        public static Bitmask GetPromotedBishopMoves(int sq, Bitmask occ) => GetBishopMoves(sq, occ) | KingMoveMask(sq);

        private static MagicSquare* InitializeMagics(int type, Bitmask* table)
        {
            MagicSquare* magicArray = AlignedAllocZeroed<MagicSquare>(SquareNB);

            Bitmask b = 0;
            ulong size = 0;
            for (int sq = I9; sq <= A1; sq++)
            {
                ref MagicSquare m = ref magicArray[sq];
                m.mask = GetBlockerMask(type, sq);

                m.attacks = sq == I9 ? table : magicArray[sq - 1].attacks + size;

                b = 0;
                size = 0;
                do
                {
                    m.attacks[Pext(b, m.mask)] = SlidingAttacks(type, sq, b);

                    size++;
                    b = b - m.mask & m.mask;
                }
                while (b != 0);
            }

            return magicArray;
        }

        private static Bitmask SlidingAttacks(int type, int sq, Bitmask occupied)
        {
            Bitmask mask = 0UL;

            int[] dirs = type == Bishop ? [Direction.NorthEast, Direction.NorthWest, Direction.SouthEast, Direction.SouthWest]
                                        : [Direction.North, Direction.East, Direction.South, Direction.West];

            foreach (int dir in dirs)
            {
                int tempSq = sq;
                while (DirectionOK(tempSq, dir))
                {
                    tempSq += dir;
                    mask |= SquareBB(tempSq);

                    if ((occupied & SquareBB(tempSq)) != 0)
                    {
                        break;
                    }
                }
            }

            return mask;
        }


        private static Bitmask GetBlockerMask(int type, int sq)
        {
            Bitmask mask = type == Bishop ? BishopRay(sq) : RookRay(sq);

            int rank = GetIndexRank(sq);
            int file = GetIndexFile(sq);

            if (rank == RankA)
                mask &= ~RankI_Mask;
            else if (rank == RankI)
                mask &= ~RankA_Mask;
            else
                mask &= ~RankI_Mask & ~RankA_Mask;

            if (file == File1)
                mask &= ~File1_Mask;
            else if (file == File9)
                mask &= ~File9_Mask;
            else
                mask &= ~File1_Mask & ~File9_Mask;

            return mask;

            Bitmask RookRay(int sq) => (GetFileBB(sq) | GetRankBB(sq)) & ~SquareBB(sq);
            Bitmask BishopRay(int sq)
            {
                Bitmask v = 0;
                foreach (int dir in new int[] { Direction.NorthEast, Direction.NorthWest, Direction.SouthEast, Direction.SouthWest })
                {
                    int tempSq = sq;
                    while (DirectionOK(tempSq, dir))
                    {
                        tempSq += dir;
                        v |= SquareBB(tempSq);
                    }
                }
                return v;
            }
        }

        [MethodImpl(InlineMaybe)]
        public static Bitmask GetLanceMoves(int color, int sq, Bitmask occ)
        {
            ref MagicSquare m = ref (color == Black ? ref BlackLanceMagics[sq] : ref WhiteLanceMagics[sq]);
            return m.attacks[Pext(occ, m.mask)];
        }



        private static MagicSquare* InitializeLanceMagics(int color, Bitmask* table)
        {
            MagicSquare* magicArray = AlignedAllocZeroed<MagicSquare>(SquareNB * ColorNB);

            Bitmask b = 0;
            ulong size = 0;
            for (int sq = I9; sq <= A1; sq++)
            {
                ref MagicSquare m = ref magicArray[sq];
                m.mask = ForwardRay(color, sq) & ~(RankA_Mask | RankI_Mask);

                m.attacks = sq == I9 ? table : magicArray[sq - 1].attacks + size;

                b = 0;
                size = 0;
                do
                {
                    m.attacks[Pext(b, m.mask)] = LanceAttacks(color, sq, b);

                    size++;
                    b = b - m.mask & m.mask;
                }
                while (b != 0);
            }

            return magicArray;

            Bitmask LanceAttacks(int color, int sq, Bitmask occupied)
            {
                Bitmask mask = 0UL;
                int tempSq = sq;
                while (DirectionOK(tempSq, ShiftUpDir(color)))
                {
                    tempSq += ShiftUpDir(color);
                    mask |= SquareBB(tempSq);

                    if ((occupied & SquareBB(tempSq)) != 0)
                    {
                        break;
                    }
                }
                return mask;
            }

            Bitmask ForwardRay(int color, int sq)
            {
                int dir = (color == Black ? Direction.North : Direction.South);
                Bitmask v = 0;
                for (int d = sq + dir; SquareOK(d); d += dir)
                    v |= SquareBB(d);
                return v;
            }
        }
    }
}