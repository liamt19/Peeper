using Peeper.Logic.Data;
using Peeper.Logic.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Peeper.Logic.Util.PGN
{
    public static unsafe class GameRecreation
    {
        public static void ParseFromSTDIn(int? side = null)
        {
            string pgnStr = ReadPGNFromSTDIn();

            var startpos = PGNStartpos(pgnStr);
            Position pos = new Position(startpos, false, null);

            side ??= pos.ToMove;

            var lines = pgnStr.Split(Environment.NewLine);

            //  Skip tags
            var moveSection = string.Join(" ", lines.SkipWhile(x => !x.StartsWith("1. "))).TrimEnd();

            int stm = Black;
            if (moveSection.Contains("..."))
            {
                stm = White;
            }

            string fullMoveStr = " moves";
            var matches = Regex.Matches(moveSection, @"(\S+) {(\S+) (\d*)\/(\d*) (\d*) (\d*)(?:.*?)}");
            foreach (Match match in matches)
            {
                var moveStr = match.Groups[1].Value;
                var moveScore = match.Groups[2].Value;
                var depth = int.Parse(match.Groups[3].Value);
                var seldepth = int.Parse(match.Groups[4].Value);
                var time = int.Parse(match.Groups[5].Value);
                var nodes = ulong.Parse(match.Groups[6].Value);

                if (stm == side)
                {
                    var mStr = fullMoveStr.Length != 6 ? fullMoveStr : string.Empty;
                    Console.WriteLine($"position sfen {startpos}{mStr}");
                    Console.WriteLine($"go nodes {nodes}");
                    Console.WriteLine("wait");
                    Console.WriteLine();
                }

                var parsedMove = ParsePGNMove(pos, moveStr);
                fullMoveStr += $" {parsedMove}";

                pos.MakeMove(parsedMove);

                stm = Not(stm);
            }
        }


        public static string ReadPGNFromSTDIn()
        {
            int blankLines = 0;
            bool started = false;
            StringBuilder sb = new();
            while (blankLines < 2)
            {
                var str = Console.ReadLine();
                if (string.IsNullOrEmpty(str))
                {
                    blankLines += started ? 1 : 0;
                    continue;
                }

                started = !str.StartsWith('[');
                sb.AppendLine(str);

                if (started && (str.Contains("*") || str.Contains("1-0") || str.Contains("0-1") || str.Contains("1/2-1/2")))
                    break;
            }

            return sb.ToString();
        }


        public static string PGNStartpos(string pgn) => PGNStartpos(pgn, out _);
        public static string PGNStartpos(string pgn, out bool hadSetup)
        {
            var startpos = InitialFEN_UCI;

            var lines = pgn.Split(Environment.NewLine);
            var setup = lines.Where(x => x.StartsWith("[FEN ")).ToArray();
            if (setup.Length != 0)
                startpos = Regex.Match(setup.First(), @"\[FEN ""(.+?)""\]").Groups[1].Value;

            hadSetup = setup.Length != 0;

            startpos = UCIFormatter.ParseSFen(startpos);

            return startpos;
        }


        public static Move ParsePGNMove(Position pos, string moveStr)
        {
            MoveList list = new();
            int size = pos.GenerateLegal(ref list);

            bool isPromoted = false;
            if (moveStr.StartsWith('+'))
            {
                isPromoted = true;
                moveStr = moveStr[1..];
            }

            int type = SFenToPiece(moveStr[0]);
            if (isPromoted)
                type = Promote(type);

            //  Move was capture, unnecessary
            moveStr = moveStr.Replace("x", string.Empty);

            bool promoting = false;
            string dstStr = moveStr[^2..];
            if (moveStr.EndsWith('+'))
            {
                promoting = true;
                moveStr = moveStr[..^1];
                dstStr = moveStr[^2..];
            }

            int dst = UCIFormat.ParseSquare(dstStr);

            if (moveStr.Contains('@'))
                return Move.MakeDrop(type, dst);

            List<Move> candidates = [];
            for (int i = 0; i < size; i++)
            {
                var move = list[i].Move;
                if (move.IsDrop)
                    continue;

                int movingType = pos.bb.GetPieceAtIndex(move.From);

                if (move.To == dst && move.IsPromotion == promoting && movingType == type)
                    candidates.Add(move);
            }

            if (candidates.Count == 1)
                return candidates[0];


            if (moveStr.Length == 5)
            {
                int src = UCIFormat.ParseSquare(moveStr[1..2]);
                return promoting ? Move.MakePromo(src, dst) : Move.MakeNormal(src, dst);
            }

            char disambig = moveStr[1];
            if (char.IsDigit(disambig))
            {
                int srcRank = UCIFormat.ParseRankChar(disambig);
                return candidates.Where(x => GetIndexRank(x.From) == srcRank).First();
            }

            int srcFile = UCIFormat.ParseFileChar(disambig);
            return candidates.Where(x => GetIndexFile(x.From) == srcFile).First();
        }
    
    }
}
