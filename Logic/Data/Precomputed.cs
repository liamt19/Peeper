using Peeper.Logic.Util;
using System.Runtime.InteropServices;
using static Peeper.Logic.Data.Direction;

namespace Peeper.Logic.Data
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

        private static readonly Bitmask** BetweenBB;
        private static readonly Bitmask** LineBB;
        private static readonly Bitmask** RayBB;

        public static Bitmask PawnMoveMask(int color, int sq) => PawnMasks[color * SquareNB + sq];
        public static Bitmask LanceMoveMask(int color, int sq) => LanceMasks[color * SquareNB + sq];
        public static Bitmask KnightMoveMask(int color, int sq) => KnightMasks[color * SquareNB + sq];
        public static Bitmask SilverMoveMask(int color, int sq) => SilverMasks[color * SquareNB + sq];
        public static Bitmask GoldMoveMask(int color, int sq) => GoldMasks[color * SquareNB + sq];
        public static Bitmask KingMoveMask(int sq) => KingMasks[sq];

        public static Bitmask BishopRay(int sq) => BishopRays[sq];
        public static Bitmask RookRay(int sq) => RookRays[sq];
        public static Bitmask LanceRay(int color, int sq) => ForwardRay(color, sq);
        public static Bitmask ForwardRay(int color, int sq) => ForwardRays[color * SquareNB + sq];

        public static Bitmask BlackPawnMoves(int sq) => PawnMasks[Black * SquareNB + sq];
        public static Bitmask WhitePawnMoves(int sq) => PawnMasks[White * SquareNB + sq];

        public static Bitmask Between(int a, int b) => BetweenBB[a][b];
        public static Bitmask Line(int a, int b) => LineBB[a][b];
        public static Bitmask Ray(int a, int b) => RayBB[a][b];

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

            BetweenBB = (Bitmask**)AlignedAllocZeroed((nuint)sizeof(Bitmask*) * SquareNB);
            LineBB = (Bitmask**)AlignedAllocZeroed((nuint)sizeof(Bitmask*) * SquareNB);
            RayBB = (Bitmask**)AlignedAllocZeroed((nuint)sizeof(Bitmask*) * SquareNB);

            CreatePieceRays();
            CreatePieceMasks();
            CreateSpecialRays();
        }

        private static void CreatePieceRays()
        {
            for (int sq = 0; sq < SquareNB; sq++)
            {
                RookRays[sq] = (GetFileBB(sq) | GetRankBB(sq)) & ~SquareBB(sq);
                foreach (int dir in new int[] { NorthEast, NorthWest, SouthEast, SouthWest })
                {
                    int tempSq = sq;
                    while (DirectionOK(tempSq, dir))
                    {
                        tempSq += dir;
                        BishopRays[sq] |= SquareBB(tempSq);
                    }
                }

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

        private static void CreatePieceMasks()
        {
            FillRingExcluding(KingMasks, []);

            for (int perspective = 0; perspective < ColorNB; perspective++)
            {
                var up = ShiftUpDir(perspective);
                var down = -up;
                Bitmask upEdge = perspective == Black ? RankA_Mask : RankI_Mask;

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
        
        private static void CreateSpecialRays()
        {
            for (int s1 = 0; s1 < SquareNB; s1++)
            {
                LineBB[s1] = AlignedAllocZeroed<Bitmask>(SquareNB);
                BetweenBB[s1] = AlignedAllocZeroed<Bitmask>(SquareNB);
                RayBB[s1] = AlignedAllocZeroed<Bitmask>(SquareNB);

                int f1 = GetIndexFile(s1);
                int r1 = GetIndexRank(s1);
                for (int s2 = 0; s2 < SquareNB; s2++)
                {
                    int f2 = GetIndexFile(s2);
                    int r2 = GetIndexRank(s2);

                    BetweenBB[s1][s2] = 0;
                    RayBB[s1][s2] = SquareBB(s1) | SquareBB(s2);

                    if ((RookRays[s1] & SquareBB(s2)) != 0)
                    {
                        BetweenBB[s1][s2] = GetRookMoves(s1, SquareBB(s2)) & GetRookMoves(s2, SquareBB(s1));
                        RayBB[s1][s2] |= (RookRays[s1] & RookRays[s2]);
                    }
                    else if ((BishopRays[s1] & SquareBB(s2)) != 0)
                    {
                        BetweenBB[s1][s2] = GetBishopMoves(s1, SquareBB(s2)) & GetBishopMoves(s2, SquareBB(s1));
                        RayBB[s1][s2] |= (BishopRays[s1] & BishopRays[s2]);
                    }

                    LineBB[s1][s2] = BetweenBB[s1][s2] | SquareBB(s2);
                }
            }
        }


        private static void FillMask(Bitmask* dst, int[] directions, Bitmask? exclude = null)
        {
            for (int i = 0; i < SquareNB; i++)
                directions.Where(x => DirectionOK(i, x)).ForEach(d => { dst[i] |= SquareBB(i + d) & ~(exclude ?? 0); });
        }

        private static void FillRingExcluding(Bitmask* dst, int[] exclude)
        {
            for (int i = 0; i < SquareNB; i++)
                new[] { 8, 9, 10, -1, 1, -8, -9, -10 }.Where(x => DirectionOK(i, x) && !exclude.Contains(x)).ForEach(d => { dst[i] |= SquareBB(i + d); });
        }


    }
}
