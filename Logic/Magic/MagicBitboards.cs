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


        public static Bitmask GetRookMoves(int idx, Bitmask occ)
        {
            ref MagicSquare m = ref RookMagics[idx];
            return m.attacks[Pext(occ, m.mask)];
        }

        public static Bitmask GetBishopMoves(int idx, Bitmask occ)
        {
            ref MagicSquare m = ref BishopMagics[idx];
            return m.attacks[Pext(occ, m.mask)];
        }

        private static MagicSquare* InitializeMagics(int pt, Bitmask* table)
        {
            MagicSquare* magicArray = AlignedAllocZeroed<MagicSquare>(SquareNB);

            Bitmask b = 0;
            ulong size = 0;
            for (int sq = I9; sq <= A1; sq++)
            {
                ref MagicSquare m = ref magicArray[sq];
                m.mask = GetBlockerMask(pt, sq);

                m.attacks = sq == I9 ? table : magicArray[sq - 1].attacks + size;

                b = 0;
                size = 0;
                do
                {
                    m.attacks[Pext(b, m.mask)] = SlidingAttacks(pt, sq, b);

                    size++;
                    b = b - m.mask & m.mask;
                }
                while (b != 0);
            }

            return magicArray;
        }

        private static Bitmask SlidingAttacks(int pt, int idx, Bitmask occupied)
        {
            Bitmask mask = 0UL;

            int[] dirs = pt == Bishop ? [Direction.NorthEast, Direction.NorthWest, Direction.SouthEast, Direction.SouthWest]
                                        : [Direction.North, Direction.East, Direction.South, Direction.West];

            foreach (int dir in dirs)
            {
                int tempSq = idx;
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


        private static Bitmask GetBlockerMask(int pt, int idx)
        {
            Bitmask mask = pt == Bishop ? BishopRay(idx) : RookRay(idx);

            int rank = GetIndexRank(idx);
            int file = GetIndexFile(idx);

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
        }


        public static Bitmask GetLanceMoves(int idx, int color, Bitmask occ)
        {
            ref MagicSquare m = ref (color == Black ? ref BlackLanceMagics[idx] : ref WhiteLanceMagics[idx]);
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

            Bitmask LanceAttacks(int color, int idx, Bitmask occupied)
            {
                Bitmask mask = 0UL;
                int tempSq = idx;
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
        }
    }
}