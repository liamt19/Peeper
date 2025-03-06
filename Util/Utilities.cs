using Peeper.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Peeper.Util
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

        public static int Not(int color) => color ^ 1;

        public static Bitmask SquareBB(int idx) => ((Bitmask)1) << idx;

        public static int CoordToIndex(int file, int rank) => 80 - (8 - file + 9 * rank);
        public static (int, int) IndexToCoord(int index) => (index % 9, index / 9);


        public static string ColorToString(int color)
        {
            return color switch
            {
                Color.White => nameof(Color.White),
                Color.Black => nameof(Color.Black),
                _ => "None"
            };
        }

        public static int StringToColor(string colorName)
        {
            return colorName.ToLower() switch
            {
                "white" => Color.White,
                "gote" => Color.White,
                "black" => Color.Black,
                "sente" => Color.Black,
                _ => Color.ColorNB
            };
        }

        public static int SFenToColor(char c) => char.IsUpper(c) ? Black : White;

        public static int SFenToPiece(char c)
        {
            return char.ToLower(c) switch
            {
                'p' => Piece.Pawn,
                'l' => Piece.Lance,
                'n' => Piece.Knight,
                's' => Piece.Silver,
                'b' => Piece.Bishop,
                'r' => Piece.Rook,
                'g' => Piece.Gold,
                'k' => Piece.King,
                _ => Piece.None
            };
        }

        public static char PieceToFENChar(int pieceType)
        {
            return pieceType switch
            {
                Piece.Pawn => 'P',
                Piece.Lance => 'L',
                Piece.Knight => 'N',
                Piece.Silver => 'S',
                Piece.Bishop => 'B',
                Piece.Rook => 'R',

                Piece.PawnPromoted => 'P',
                Piece.LancePromoted => 'L',
                Piece.KnightPromoted => 'N',
                Piece.SilverPromoted => 'S',
                Piece.BishopPromoted => 'B',
                Piece.RookPromoted => 'R',

                Piece.Gold => 'G',
                Piece.King => 'K',
                _ => ' '
            };
        }

        public static string PieceToSFen(int color, int type)
        {
            char t = PieceToFENChar(type);
            if (color == White)
                t = char.ToLower(t);

            return $"{(Piece.IsPromoted(type) ? "+" : "")}{t}";
        }

        public static string PieceToString(int type)
        {
            return type switch
            {
                Piece.Pawn => nameof(Piece.Pawn),
                Piece.Lance => nameof(Piece.Lance),
                Piece.Knight => nameof(Piece.Knight),
                Piece.Silver => nameof(Piece.Silver),
                Piece.Bishop => nameof(Piece.Bishop),
                Piece.Rook => nameof(Piece.Rook),

                Piece.PawnPromoted => nameof(Piece.PawnPromoted),
                Piece.LancePromoted => nameof(Piece.LancePromoted),
                Piece.KnightPromoted => nameof(Piece.KnightPromoted),
                Piece.SilverPromoted => nameof(Piece.SilverPromoted),
                Piece.BishopPromoted => nameof(Piece.BishopPromoted),
                Piece.RookPromoted => nameof(Piece.RookPromoted),

                Piece.Gold => nameof(Piece.Gold),
                Piece.King => nameof(Piece.King),
                _ => "None"
            };
        }

        public static string PieceToString(int color, int type) => $"{ColorToString(color)} {PieceToString(type)}";
        public static string IndexToString(int idx) => $"{GetIndexFileName(idx)}{GetIndexRankName(idx)}";

        public static int GetIndexFileName(int idx) => 9 - (idx % 9);
        public static char GetIndexRankName(int idx) => (char)('i' - (idx / 9));


        public static bool HasBit(this Bitmask b, int idx) => (b & SquareBB(idx)) != UInt128.Zero;


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
    }
}
