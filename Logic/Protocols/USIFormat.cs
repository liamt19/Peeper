using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Protocols
{
    public class USIFormat : IFormat
    {
        public string FormatSquare(int sq)
        {
            return SquareToString(sq);
        }

        public int ParseSquare(string str)
        {
            return StringToSquare(str);
        }


        public string FormatMove(Move move)
        {
            if (move.IsDrop)
            {
                return $"{PieceToSFenChar(move.DroppedPiece)}*{FormatSquare(move.To)}";
            }

            return $"{FormatSquare(move.From)}{FormatSquare(move.To)}{(move.IsPromotion ? "+" : "")}";
        }

        public Move ParseMove(string str)
        {
            if (str[1] == '@')
            {
                int sq = ParseSquare(str[2..]);
                int type = SFenToPiece(str[0]);
                return Move.MakeDrop(type, sq);
            }

            Assert(str.Length < 5 || str[4] == '+');

            int src = ParseSquare(str[2..]);
            int dst = ParseSquare(str[..2]);
            int flag = str.Length == 5 ? Move.FlagPromotion : 0;
            return new Move(src, dst, flag);
        }


        public string FormatSFen(Position pos)
        {
            return pos.GetSFen();
        }

        public string ParseSFen(string sfen)
        {
            return sfen;
        }


        public string DisplayBoard(Bitboard bb)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            char[] fileNames = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i'];

            sb.AppendLine($"   {string.Join(" |  ", Enumerable.Range(1, 9).Reverse())}  ");
            sb.AppendLine($"+----+----+----+----+----+----+----+----+----+");
            for (int y = 0; y < 9; y++)
            {
                sb.Append("|");
                for (int x = 0; x < 9; x++)
                {
                    int sq = CoordToIndex(x, y);
                    int type = bb.GetPieceAtIndex(sq);

                    sb.Append(' ');

                    if (type != None)
                    {
                        int color = bb.GetColorAtIndex(sq);
                        var c = PieceToSFen(color, type);
                        sb.Append(c.Length == 1 ? $" {c}" : c);
                    }
                    else
                    {
                        sb.Append("  ");
                    }

                    sb.Append(" |");
                }
                sb.Append($" {char.ToUpper(fileNames[y])}");

                sb.AppendLine();
                sb.AppendLine("+----+----+----+----+----+----+----+----+----+  ");
            }

            return sb.ToString();
        }



        public string FormatMateDistance(int score)
        {
            int dist = (score > 0) ? ( ScoreMate - score) 
                                   : (-ScoreMate - score);
            return dist.ToString();
        }


        public string GetGoBlackChar()
        {
            return "b";
        }

        public string GetGoWhiteChar()
        {
            return "w";
        }


        public void SendInitialReadyResponse()
        {
            Console.WriteLine("usiok");
        }

        public string GetReadyResponse()
        {
            return "usiok";
        }

        public string SetupNewGameCommand()
        {
            return "isready";
        }

        public string RespondReadyCommand()
        {
            return "isready";
        }

        public (string response, bool abort) HandleImpasse()
        {
            return ("bestmove win", true);
        }
    }
}
