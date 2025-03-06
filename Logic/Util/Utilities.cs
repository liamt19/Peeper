using Peeper.Logic.Core;
using Peeper.Logic.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Peeper.Logic.Util
{
    public static class Utilities
    {
        public const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

        public static readonly Bitmask AllMask = new(0x1ffff, 0xffffffffffffffff);
        public static readonly Bitmask EmptyMask = new();

        public static readonly Bitmask RankI_Mask = new(0, 0x1ff);
        public static readonly Bitmask RankH_Mask = new(0, 0x3fe00);
        public static readonly Bitmask RankG_Mask = new(0, 0x7fc0000);
        public static readonly Bitmask RankF_Mask = new(0, 0xff8000000);
        public static readonly Bitmask RankE_Mask = new(0, 0x1ff000000000);
        public static readonly Bitmask RankD_Mask = new(0, 0x3fe00000000000);
        public static readonly Bitmask RankC_Mask = new(0, 0x7fc0000000000000);
        public static readonly Bitmask RankB_Mask = new(0xff, 0x8000000000000000);
        public static readonly Bitmask RankA_Mask = new(0x1ff00, 0);
        public static readonly Bitmask File9_Mask = new(0x100, 0x8040201008040201);
        public static readonly Bitmask File8_Mask = new(0x201, 0x80402010080402);
        public static readonly Bitmask File7_Mask = new(0x402, 0x100804020100804);
        public static readonly Bitmask File6_Mask = new(0x804, 0x201008040201008);
        public static readonly Bitmask File5_Mask = new(0x1008, 0x402010080402010);
        public static readonly Bitmask File4_Mask = new(0x2010, 0x804020100804020);
        public static readonly Bitmask File3_Mask = new(0x4020, 0x1008040201008040);
        public static readonly Bitmask File2_Mask = new(0x8040, 0x2010080402010080);
        public static readonly Bitmask File1_Mask = new(0x10080, 0x4020100804020100);

        public const string InitialFEN = @"lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL b - 1";



        public static void Log(string s)
        {
            Console.WriteLine(s);
            Debug.WriteLine(s);
        }

        public static void Log(Bitmask s)
        {
            Log(s.FormattedString());
            Log($"0x{s:X}");
        }

        public static int Not(int color) => color ^ 1;

        public static Bitmask SquareBB(int idx) => (Bitmask)1 << idx;

        public static int CoordToIndex(int file, int rank) => 80 - (8 - file + 9 * rank);
        public static (int, int) IndexToCoord(int index) => (index % 9, index / 9);

        public static Bitmask GetRankBB(int rank) => RankI_Mask << 9 * GetIndexRank(rank);
        public static Bitmask GetFileBB(int file) => File9_Mask << GetIndexFile(file);

        public static ulong Upper(this Bitmask b) => (ulong)(b >> 64);
        public static ulong Lower(this Bitmask b) => (ulong)b;


        public static Bitmask Shift(this Bitmask b, int dir)
        {
            return dir switch
            {
                Direction.North      => b << 9,
                Direction.South      => b >> 9,
                Direction.NorthNorth => b << 18,
                Direction.SouthSouth => b >> 18,
                Direction.East       => (b & ~File1_Mask) << 1,
                Direction.West       => (b & ~File9_Mask) >> 1,
                Direction.NorthEast  => (b & ~File1_Mask) << 10,
                Direction.NorthWest  => (b & ~File9_Mask) << 8,
                Direction.SouthEast  => (b & ~File1_Mask) >> 8,
                Direction.SouthWest  => (b & ~File9_Mask) >> 10,
                _ => 0
            };
        }

        [MethodImpl(Inline)]
        public static int ShiftUpDir(int color) => color == Black ? Direction.North : Direction.South;

        [MethodImpl(Inline)]
        public static int GetIndexFile(int index) => index % 9;

        [MethodImpl(Inline)]
        public static int GetIndexRank(int index) => index / 9;

        public static bool SquareOK(int sq)
        {
            return sq >= I9 && sq <= A1;
        }

        public static bool DirectionOK(int sq, int dir)
        {
            if (!SquareOK(sq + dir))
            {
                return false;
            }

            int rankDistance = Math.Abs(GetIndexRank(sq) - GetIndexRank(sq + dir));
            int fileDistance = Math.Abs(GetIndexFile(sq) - GetIndexFile(sq + dir));
            return Math.Max(rankDistance, fileDistance) <= 2;
        }

        public static string ColorToString(int color)
        {
            return color switch
            {
                White => nameof(White),
                Black => nameof(Black),
                _ => "None"
            };
        }

        public static int StringToColor(string colorName)
        {
            return colorName.ToLower() switch
            {
                "white" => White,
                "gote" => White,
                "black" => Black,
                "sente" => Black,
                _ => ColorNB
            };
        }

        public static int SFenToColor(char c) => char.IsUpper(c) ? Black : White;

        public static int SFenToPiece(char c)
        {
            return char.ToLower(c) switch
            {
                'p' => Pawn,
                'l' => Lance,
                'n' => Knight,
                's' => Silver,
                'b' => Bishop,
                'r' => Rook,
                'g' => Gold,
                'k' => King,
                _ => None
            };
        }

        public static char PieceToFENChar(int pieceType)
        {
            return pieceType switch
            {
                Pawn => 'P',
                Lance => 'L',
                Knight => 'N',
                Silver => 'S',
                Bishop => 'B',
                Rook => 'R',

                PawnPromoted => 'P',
                LancePromoted => 'L',
                KnightPromoted => 'N',
                SilverPromoted => 'S',
                BishopPromoted => 'B',
                RookPromoted => 'R',

                Gold => 'G',
                King => 'K',
                _ => ' '
            };
        }

        public static string PieceToSFen(int color, int type)
        {
            char t = PieceToFENChar(type);
            if (color == White)
                t = char.ToLower(t);

            return $"{(IsPromoted(type) ? "+" : "")}{t}";
        }

        public static string PieceToString(int type)
        {
            return type switch
            {
                Pawn => nameof(Pawn),
                Lance => nameof(Lance),
                Knight => nameof(Knight),
                Silver => nameof(Silver),
                Bishop => nameof(Bishop),
                Rook => nameof(Rook),

                PawnPromoted => nameof(PawnPromoted),
                LancePromoted => nameof(LancePromoted),
                KnightPromoted => nameof(KnightPromoted),
                SilverPromoted => nameof(SilverPromoted),
                BishopPromoted => nameof(BishopPromoted),
                RookPromoted => nameof(RookPromoted),

                Gold => nameof(Gold),
                King => nameof(King),
                _ => "None"
            };
        }

        public static string PieceToString(int color, int type) => $"{ColorToString(color)} {PieceToString(type)}";
        public static string IndexToString(int idx) => $"{GetIndexFileName(idx)}{GetIndexRankName(idx)}";

        public static int GetIndexFileName(int idx) => 9 - idx % 9;
        public static char GetIndexRankName(int idx) => (char)('i' - idx / 9);


        public static bool HasBit(this Bitmask b, int idx) => (b & SquareBB(idx)) != Bitmask.Zero;


        public static ReadOnlySpan<char> FileNames => ['i', 'h', 'g', 'f', 'e', 'd', 'c', 'b', 'a'];
        public static string PrintBoard(Bitboard bb)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            sb.AppendLine($"   {string.Join(" |  ", Enumerable.Range(1, 9).Reverse())}  ");
            sb.AppendLine($"+----+----+----+----+----+----+----+----+----+");
            for (int y = 8; y >= 0; y--)
            {
                sb.Append("|");
                for (int x = 0; x < 9; x++)
                {
                    int idx = CoordToIndex(x, y);
                    int pt = bb.GetPieceAtIndex(idx);
                    int pc = bb.GetColorAtIndex(idx);

                    sb.Append(' ');

                    if (pt != None)
                    {
                        var c = PieceToSFen(pc, pt);
                        sb.Append(c.Length == 1 ? $" {c}" : c);
                    }
                    else
                    {
                        sb.Append("  ");
                    }

                    sb.Append(" |");
                }
                sb.Append($" {char.ToUpper(FileNames[y])}");

                sb.AppendLine();
                sb.AppendLine("+----+----+----+----+----+----+----+----+----+  ");
            }

            return sb.ToString();
        }

        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
                action(item);
        }

        public static string FormattedString(this Bitmask b)
        {
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    int idx = CoordToIndex(x, y);
                    sb.Append(b.HasBit(idx) ? '1' : '\u00B7');
                }

                if (y != 8)
                    sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
