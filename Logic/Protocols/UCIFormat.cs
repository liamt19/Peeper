using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Peeper.Logic.Protocols
{
    public class UCIFormat : IFormat
    {
        public string FormatSquare(int sq)
        {
            return $"{(char)('a' + GetIndexFile(sq))}{(char)('1' + GetIndexRank(sq))}";
        }

        public int ParseSquare(string str)
        {
            if (str.Length != 2)
                return SquareNB;

            if (str[0] < 'a' || str[0] > 'i' || str[1] < '1' || str[1] > '9')
                return SquareNB;

            int rank = str[1] - '1';
            int file = str[0] - 'a';

            return rank * 9 + file;
        }


        public string FormatMove(Move move)
        {
            if (move.IsDrop)
            {
                return $"{PieceToSFenChar(move.DroppedPiece)}@{FormatSquare(move.To)}";
            }

            return $"{FormatSquare(move.From)}{FormatSquare(move.To)}{(move.IsPromotion ? "+" : "")}";
        }

        public Move ParseMove(string str)
        {
            if (str[1] == '*')
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
            string fen = pos.GetSFen();

            var splits = fen.Split(' ');
            string stm = splits[1] == "w" ? "b" : "w";

            var fmv = (pos.MoveNumber - 1) / 2;
            return $"{splits[0]}[{splits[2]} {stm} - {fmv}";
        }

        public string ParseSFen(string sfen)
        {
            if (!sfen.Contains('['))
                return sfen;

            var splits = sfen.Split([' ', '[', ']'], StringSplitOptions.RemoveEmptyEntries);
            var placements = splits[0];
            var hand = splits[1];
            var stm = splits[2] == "w" ? "b" : "w";
            
            var fmv = 1;
            if (splits.Length > 3 && int.TryParse(splits[3], out int mv))
            {
                fmv = ((mv * 2) - (stm == "b" ? 1 : 0));
            }

            return $"{placements} {stm} {hand} {fmv}";
        }



        public string FormatMateDistance(int score)
        {
            int dist = (score > 0) ? (( ScoreMate - score + 1) / 2) 
                                   : ((-ScoreMate - score    ) / 2);
            return dist.ToString();
        }


        public string GetGoBlackChar()
        {
            return "w";
        }

        public string GetGoWhiteChar()
        {
            return "b";
        }


        public void SendInitialReadyResponse()
        {
            Console.WriteLine("option name UCI_Variant type combo default shogi var shogi");
            Console.WriteLine("uciok");
        }

        public string GetReadyResponse()
        {
            return "uciok";
        }

        public string SetupNewGameCommand()
        {
            return "ucinewgame";
        }

        public string RespondReadyCommand()
        {
            return "isready";
        }
    }
}
