
using System.Runtime.InteropServices;
using static Peeper.Data.Direction;

namespace Peeper.Data
{
    public static unsafe class Precomputed
    {
        private static readonly Bitmask* PawnMasks;
        private static readonly Bitmask* LanceMasks;
        private static readonly Bitmask* KnightMasks;
        private static readonly Bitmask* SilverMasks;
        private static readonly Bitmask* GoldMasks;

        private static readonly Bitmask* KingMasks;
        private static readonly Bitmask* RookRays;
        private static readonly Bitmask* BishopRays;

        private static readonly Bitmask* ForwardRays;
        private static readonly Bitmask* BackwardRays;

        public static Bitmask PawnMoveMask(int pc, int idx) => PawnMasks[pc * SquareNB + idx];
        public static Bitmask LanceMoveMask(int pc, int idx) => LanceMasks[pc * SquareNB + idx];
        public static Bitmask KnightMoveMask(int pc, int idx) => KnightMasks[pc * SquareNB + idx];
        public static Bitmask SilverMoveMask(int pc, int idx) => SilverMasks[pc * SquareNB + idx];
        public static Bitmask GoldMoveMask(int pc, int idx) => GoldMasks[pc * SquareNB + idx];
        public static Bitmask KingMoveMask(int idx) => KingMasks[idx];

        public static Bitmask BishopRay(int idx) => BishopRays[idx];
        public static Bitmask RookRay(int idx) => RookRays[idx];
        public static Bitmask ForwardRay(int pc, int idx) => ForwardRays[pc * SquareNB + idx];

        static Precomputed()
        {
            PawnMasks = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);
            LanceMasks = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);
            KnightMasks = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);
            SilverMasks = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);
            GoldMasks = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);

            ForwardRays = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);
            BackwardRays = AlignedAllocZeroed<Bitmask>(SquareNB * ColorNB);

            KingMasks = AlignedAllocZeroed<Bitmask>(SquareNB);
            RookRays = AlignedAllocZeroed<Bitmask>(SquareNB);
            BishopRays = AlignedAllocZeroed<Bitmask>(SquareNB);

            CreateRays();
            CreatePieceMasks();
        }

        private static void CreateRays()
        {
            for (int sq = 0; sq < SquareNB; sq++)
            {
                RookRays[sq] = (GetFileBB(sq) | GetRankBB(sq)) & (~SquareBB(sq));
                foreach (int dir in new int[] { Direction.NorthEast, Direction.NorthWest, Direction.SouthEast, Direction.SouthWest })
                {
                    int tempSq = sq;
                    while (DirectionOK(tempSq, dir))
                    {
                        tempSq += dir;
                        BishopRays[sq] |= SquareBB(tempSq);
                    }
                }

                ref var l = ref ForwardRays[Black * SquareNB + sq];
                for (int d = sq + North; SquareOK(d); d += North)
                {
                    ForwardRays[Black * SquareNB + sq] |= SquareBB(d);
                    BackwardRays[White * SquareNB + sq] |= SquareBB(d);
                }

                for (int d = sq + South; SquareOK(d); d += South)
                {
                    BackwardRays[Black * SquareNB + sq] |= SquareBB(d);
                    ForwardRays[White * SquareNB + sq] |= SquareBB(d);
                }
            }
        }

        private static void FillMask(Bitmask* dst, int[] directions, Bitmask? exclude = null)
        {
            for (int i = 0; i < SquareNB; i++)
                directions.Where(x => DirectionOK(i, x)).ForEach(d => { dst[i] |= (SquareBB(i + d) & ~(exclude ?? 0)); });
        }

        private static void FillRingExcluding(Bitmask* dst, int[] exclude)
        {
            for (int i = 0; i < SquareNB; i++)
                new[] { 8, 9, 10, -1, 1, -8, -9, -10 }.Where(x => DirectionOK(i, x) && !exclude.Contains(x)).ForEach(d => { dst[i] |= SquareBB(i + d); });
        }

        private static void CreatePieceMasks()
        {
            FillRingExcluding(KingMasks, []);

            for (int perspective = 0; perspective < ColorNB; perspective++)
            {
                var up = ShiftUpDir(perspective);
                var down = -up;
                Bitmask upEdge = (perspective == Black) ? RankA_Mask : RankI_Mask;

                int colOffset = perspective * SquareNB;

                FillMask(&PawnMasks[colOffset], [up]);
                FillRingExcluding(&GoldMasks[colOffset], [down + West, down + East]);
                FillRingExcluding(&SilverMasks[colOffset], [West, East, down]);

                for (int sq = 0; sq < SquareNB; sq++)
                {
                    Bitmask sqMask = SquareBB(sq);
                    int maskOffset = colOffset + sq;

                    LanceMasks[maskOffset] = ForwardRays[maskOffset];
                    KnightMasks[maskOffset] = SquareBB(sq + up + up + West) | SquareBB(sq + up + up + East);
                }
            }
        }
    }
}
