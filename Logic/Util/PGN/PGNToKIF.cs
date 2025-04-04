﻿using Peeper.Logic.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Peeper.Logic.Util.PGN
{
    public static unsafe class PGNToKIF
    {
        private static string PieceToKanji(int type)
        {
            return type switch
            {
                Pawn => "歩",
                Lance => "香",
                Knight => "桂",
                Silver => "銀",
                Bishop => "角",
                Rook => "飛",
                PawnPromoted => "と",
                LancePromoted => "成香",
                KnightPromoted => "成桂",
                BishopPromoted => "馬",
                SilverPromoted => "成銀",
                RookPromoted => "龍",
                Gold => "金",
                King => "玉",
                _ => "???",
            };
        }

        private static char ToFullwidthDigit(int n) => (char)('０' + n);

        private static readonly string KanjiNumbers = "　一二三四五六七八九";
        private static readonly string PGNBlackName = "Sente";
        private static readonly string PGNWhiteName = "Gote";

        public static void ParseFromSTDIn()
        {
            var backupEncoding = Console.OutputEncoding;
            Console.OutputEncoding = Encoding.Unicode;

            Console.WriteLine("Paste your PGN below:\r\n");
            string pgnStr = GameRecreation.ReadPGNFromSTDIn();
            
            var startpos = GameRecreation.PGNStartpos(pgnStr, out bool isSetup);
            Position pos = new Position(startpos, false, null);

            List<string> moveStrings = [];

            var lines = pgnStr.Split(Environment.NewLine);

            //  Skip tags
            var movesList = string.Join(" ", lines.SkipWhile(x => !x.StartsWith("1. "))).TrimEnd();

            string gameResult = GetGameResult(movesList);

            //  Remove annotations
            movesList = Regex.Replace(movesList, @" {.+?}", string.Empty);

            //  Delete game result
            movesList = Regex.Replace(movesList, @"(:?\*|1\-0|0\-1|1\/2-1\/2)$", string.Empty);

            //  Match triplets of (number. move move)
            var matches = Regex.Matches(movesList, @"(\d*)\. (?:(\S+) ?(\S*)?)");

            var (bName, wName) = ParsePlayerNames(lines);
            string bHand = HandToString(pos, Black);
            string wHand = HandToString(pos, White);
            string board = BoardToString(pos);
            int startPosToMove = pos.ToMove;

            Move blackMove = Move.Null;
            Move whiteMove = Move.Null;
            int priorSquare = SquareNB + 1;
            int printNumber = 1;
            for (int fmv = 0; fmv < matches.Count; fmv++)
            {
                //  Iterate through each pair of moves made
                Match m = matches[fmv];
                Assert(m.Groups.Count == 4);
                int moveNumber = int.Parse(m.Groups[1].Value);
                string bMoveStr = m.Groups[2].Value;
                string wMoveStr = m.Groups[3].Value;

                blackMove = GameRecreation.ParsePGNMove(pos, bMoveStr);
                moveStrings.Add(FormatMove(pos, blackMove, priorSquare, printNumber++));
                priorSquare = blackMove.To;
                pos.MakeMove(blackMove);

                if (wMoveStr.Length == 0)
                {
                    break;
                }

                whiteMove = GameRecreation.ParsePGNMove(pos, wMoveStr);
                moveStrings.Add(FormatMove(pos, whiteMove, priorSquare, printNumber++));
                priorSquare = whiteMove.To;
                pos.MakeMove(whiteMove);
            }

            Console.WriteLine("\r\n\r\nReconstructed KIF:");

            if (isSetup)
            {
                Console.WriteLine(bHand);
                Console.WriteLine(board);
                Console.WriteLine(wHand);
            }
            else
            {
                //  Handicap: even
                Console.WriteLine("手合割：平手");

                //  "Second move"
                if (startPosToMove == White)
                    Console.WriteLine("後手番");
            }

            Console.WriteLine("先手：" + bName);
            Console.WriteLine("後手：" + wName);

            Console.WriteLine("手数----指手---------消費時間--");
            foreach (var moveStr in moveStrings)
            {
                Console.WriteLine(moveStr);
            }

            if (gameResult.Length > 0)
                Console.WriteLine($"{gameResult}");

            Console.WriteLine();
            Console.OutputEncoding = backupEncoding;
        }


        private static string BoardToString(Position pos)
        {
            StringBuilder sb = new();

            ref Bitboard bb = ref pos.bb;

            sb.AppendLine("  ９ ８ ７ ６ ５ ４ ３ ２ １");
            sb.AppendLine("+---------------------------+");
            for (int y = 0; y < 9; y++)
            {
                sb.Append("|");

                for (int x = 0; x < 9; x++)
                {
                    int sq = CoordToIndex(x, y);
                    int type = bb.GetPieceAtIndex(sq);

                    if (type != None)
                    {
                        int color = bb.GetColorAtIndex(sq);
                        sb.Append(color == White ? 'v' : ' ');
                        sb.Append(PieceToKanji(type));
                    }
                    else
                    {
                        sb.Append(" ・");
                    }
                }

                sb.AppendLine($"|{KanjiNumbers[y + 1]}");
            }
            sb.Append("+---------------------------+");

            return sb.ToString();
        }


        private static string HandToString(Position pos, int color)
        {
            StringBuilder sb = new();

            if (color == Black)
                sb.Append("後手");
            else
                sb.Append("先手");

            sb.Append("の持駒：");
            var hand = pos.State->Hands[color];
            if (hand.IsEmpty)
            {
                sb.Append("なし");
            }
            else
            {
                foreach (var type in DroppableTypes)
                {
                    int n = hand.NumHeld(type);
                    if (n == 0)
                        continue;

                    sb.Append(PieceToKanji(type));
                    if (n > 1)
                        sb.Append(KanjiNumbers[n]);
                    sb.Append(" ");
                }
            }

            return sb.ToString();
        }


        private static string FormatMove(Position pos, Move move, int priorSquare, int moveNumber)
        {
            StringBuilder sb = new();

            var (moveFrom, moveTo) = move.Unpack();
            int type = pos.MovedPiece(move);

            sb.Append($"{moveNumber,3}  ");

            if (moveTo == priorSquare)
            {
                sb.Append("同　");
            }
            else
            {
                var file = 9 - GetIndexFile(moveTo);
                sb.Append(ToFullwidthDigit(file));

                int dispRank = 9 - GetIndexRank(moveTo);
                sb.Append(KanjiNumbers[dispRank]);
            }

            sb.Append(PieceToKanji(type));

            if (move.IsDrop)
                sb.Append("打");
            else if (move.IsPromotion)
                sb.Append("成");

            if (!move.IsDrop)
            {
                var file = 9 - GetIndexFile(moveFrom);
                var rank = 9 - GetIndexRank(moveFrom);
                sb.Append($"({file}{rank})");
            }

            return sb.ToString();
        }


        private static (string blackName, string whiteName) ParsePlayerNames(string[] lines)
        {
            var b = lines
                .Where(x => x.StartsWith("[Black \""))
                .FirstOrDefault(PGNBlackName)
                .Skip(8)
                .TakeWhile(x => x != '"')
                .ToArray();

            var w = lines
                .Where(x => x.StartsWith("[White \""))
                .FirstOrDefault(PGNWhiteName)
                .Skip(8)
                .TakeWhile(x => x != '"')
                .ToArray();

            return (new string(b), new string(w));
        }


        private const string IllegalMove = "反則負け";
        private static string GetGameResult(string lines)
        {
            if (lines.Contains("an illegal move"))
                return $"#{IllegalMove}";

            if (lines.EndsWith("1-0") || lines.EndsWith("0-1"))
                return "詰み";

            if (lines.EndsWith("1/2-1/2"))
                return "千日手";

            return string.Empty;
        }
    }
}
