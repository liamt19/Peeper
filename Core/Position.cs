using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peeper.Core
{
    public class Position
    {
        public Bitboard bb;

        public int ToMove;
        public int MoveNumber;

        public Position(string? fen = null)
        {
            bb = new Bitboard();

            LoadFromSFen(fen ?? InitialFEN);
        }

        public void MakeMove(Move m)
        {

        }

        public void UnmakeMove(Move m)
        {

        }

        public void LoadFromSFen(string fen)
        {
            bb.Clear();
            MoveNumber = 1;

            var fields = fen.Split(' ');

            var ranks = fields[0].Split('/');
            for (int splitIdx = 0; splitIdx < RankNB; splitIdx++)
            {
                var rankStr = ranks[splitIdx];
                int rank = splitIdx;
                int file = -1;

                foreach (char c in rankStr)
                {
                    file++;

                    bool isPromoted = false;
                    if (char.IsDigit(c))
                    {
                        file += (int.Parse(c.ToString()) - 1);
                        continue;
                    }

                    if (c == '+')
                    {
                        isPromoted = true;
                        continue;
                    }

                    int idx = CoordToIndex(file, rank);
                    int color = SFenToColor(c);
                    int type = SFenToPiece(c);

                    type += isPromoted ? PromotionNB : 0;

                    Log($"Adding {PieceToString(color, type)} on {IndexToString(idx)}");

                    bb.AddPiece(idx, color, type);
                    isPromoted = false;


                }
            }

            ToMove = fields[1] == "b" ? Black : White;
            if (fields.Length > 3) { }
        }

        public string GetSFen()
        {
            StringBuilder sb = new StringBuilder();



            return sb.ToString();
        }

        public override string ToString()
        {
            return $"{bb}\r\n\r\n";
        }
    }
}
