using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Peeper.Logic.Data
{

    public static class Color
    {
        /// <summary> Sente </summary>
        public const int Black = 0;

        /// <summary> Gote </summary>
        public const int White = 1;

        public const int ColorNB = 2;
    }

    public static class Piece
    {
        public const int Pawn = 0;
        public const int PawnPromoted = 1;
        public const int Lance = 2;
        public const int Knight = 3;
        public const int LancePromoted = 4;
        public const int KnightPromoted = 5;
        public const int Silver = 6;
        public const int SilverPromoted = 7;
        public const int Gold = 8;
        public const int Bishop = 9;
        public const int Rook = 10;
        public const int BishopPromoted = 11;
        public const int RookPromoted = 12;
        public const int King = 13;

        public const int None = 14;
        public const int PieceNB = 14;

        public static ReadOnlySpan<int> DroppableTypes => [Pawn, Lance, Knight, Silver, Gold, Bishop, Rook];

        public static bool IsPromoted(int type)
        {
            const int bits = 0b001100010110010;
            return ((bits >> type) & 1) != 0;
        }

        public static bool CanPromote(int type)
        {
            Assert(type != None);
            const int bits = 0b000011001001101;
            return ((bits >> type) & 1) != 0;
        }


        private static ReadOnlySpan<int> _Promote => [PawnPromoted, None, LancePromoted, KnightPromoted, None, None, SilverPromoted, None, None, BishopPromoted, RookPromoted, None, None, None, None];
        public static int Promote(int type)
        {
            Assert(CanPromote(type));
            return _Promote[type];
        }


        private static ReadOnlySpan<int> _Demote => [None, Pawn, None, None, Lance, Knight, None, Silver, None, None, None, Bishop, Rook, None, None];
        public static int Demote(int type)
        {
            Assert(IsPromoted(type));
            return _Demote[type];
        }

        [MethodImpl(Inline)]
        public static int DemoteMaybe(int type)
        {
            return IsPromoted(type) ? Demote(type) : type;
        }


        private static ReadOnlySpan<int> _FromDropIndex => [Pawn, Lance, Knight, Silver, Gold, Bishop, Rook];
        public static int FromDropIndex(int d)
        {
            return _FromDropIndex[d];
        }

        private static ReadOnlySpan<int> _ToDropIndex => [0, None, 1, 2, None, None, 3, None, 4, 5, 6, None, None, None, None];
        public static int ToDropIndex(int type)
        {
            return _ToDropIndex[type];
        }
    }

    public static class Ranks
    {
        public const int RankI = 0;
        public const int RankH = 1;
        public const int RankG = 2;
        public const int RankF = 3;
        public const int RankE = 4;
        public const int RankD = 5;
        public const int RankC = 6;
        public const int RankB = 7;
        public const int RankA = 8;

        public const int RankNB = 9;
    }

    public static class Files
    {
        public const int File1 = 0;
        public const int File2 = 1;
        public const int File3 = 2;
        public const int File4 = 3;
        public const int File5 = 4;
        public const int File6 = 5;
        public const int File7 = 6;
        public const int File8 = 7;
        public const int File9 = 8;

        public const int FileNB = 9;
    }


    public static class Direction
    {
        public const int North = 9;
        public const int East = 1;
        public const int South = -North;
        public const int West = -East;

        public const int NorthEast = North + East;
        public const int SouthEast = South + East;
        public const int SouthWest = South + West;
        public const int NorthWest = North + West;

        public const int NorthNorth = North + North;
        public const int SouthSouth = South + South;
    }


    public interface SearchNodeType { }
    public struct PVNode : SearchNodeType { }
    public struct NonPVNode : SearchNodeType { }
    public struct RootNode : SearchNodeType { }

    public enum Sennichite
    {
        None,
        Draw,
        Win,
    };

    public enum TTNodeType
    {
        Invalid,
        /// <summary>
        /// Upper Bound
        /// </summary>
        Beta,
        /// <summary>
        /// Lower bound
        /// </summary>
        Alpha,
        Exact = Beta | Alpha
    };

    public static class Bound
    {
        public const int BoundNone = (int)TTNodeType.Invalid;
        public const int BoundUpper = (int)TTNodeType.Beta;
        public const int BoundLower = (int)TTNodeType.Alpha;
        public const int BoundExact = (int)TTNodeType.Exact;
    }

    public static class Squares
    {
        public const int I9 = 0;
        public const int I8 = 1;
        public const int I7 = 2;
        public const int I6 = 3;
        public const int I5 = 4;
        public const int I4 = 5;
        public const int I3 = 6;
        public const int I2 = 7;
        public const int I1 = 8;
        public const int H9 = 9;
        public const int H8 = 10;
        public const int H7 = 11;
        public const int H6 = 12;
        public const int H5 = 13;
        public const int H4 = 14;
        public const int H3 = 15;
        public const int H2 = 16;
        public const int H1 = 17;
        public const int G9 = 18;
        public const int G8 = 19;
        public const int G7 = 20;
        public const int G6 = 21;
        public const int G5 = 22;
        public const int G4 = 23;
        public const int G3 = 24;
        public const int G2 = 25;
        public const int G1 = 26;
        public const int F9 = 27;
        public const int F8 = 28;
        public const int F7 = 29;
        public const int F6 = 30;
        public const int F5 = 31;
        public const int F4 = 32;
        public const int F3 = 33;
        public const int F2 = 34;
        public const int F1 = 35;
        public const int E9 = 36;
        public const int E8 = 37;
        public const int E7 = 38;
        public const int E6 = 39;
        public const int E5 = 40;
        public const int E4 = 41;
        public const int E3 = 42;
        public const int E2 = 43;
        public const int E1 = 44;
        public const int D9 = 45;
        public const int D8 = 46;
        public const int D7 = 47;
        public const int D6 = 48;
        public const int D5 = 49;
        public const int D4 = 50;
        public const int D3 = 51;
        public const int D2 = 52;
        public const int D1 = 53;
        public const int C9 = 54;
        public const int C8 = 55;
        public const int C7 = 56;
        public const int C6 = 57;
        public const int C5 = 58;
        public const int C4 = 59;
        public const int C3 = 60;
        public const int C2 = 61;
        public const int C1 = 62;
        public const int B9 = 63;
        public const int B8 = 64;
        public const int B7 = 65;
        public const int B6 = 66;
        public const int B5 = 67;
        public const int B4 = 68;
        public const int B3 = 69;
        public const int B2 = 70;
        public const int B1 = 71;
        public const int A9 = 72;
        public const int A8 = 73;
        public const int A7 = 74;
        public const int A6 = 75;
        public const int A5 = 76;
        public const int A4 = 77;
        public const int A3 = 78;
        public const int A2 = 79;
        public const int A1 = 80;

        public const int SquareNB = 81;
    }
}
