﻿
using Peeper.Logic.Search.History;
using static Peeper.Logic.Evaluation.MaterialCounting;

namespace Peeper.Logic.Search
{
    public static unsafe class MoveOrdering
    {
        public static void AssignScores(Position pos, SearchStack* ss, in ThreadHistory history, ref MoveList list, Move ttMove)
        {
            ref Bitboard bb = ref pos.bb;
            int stm = pos.ToMove;

            int size = list.Size;
            for (int i = 0; i < size; i++)
            {
                ref ScoredMove sm = ref list[i];
                Move m = sm.Move;
                var (moveFrom, moveTo) = m.Unpack();

                if (m.Equals(ttMove))
                {
                    sm.Score = int.MaxValue - 1000000;
                    continue;
                }

                int captured = bb.GetPieceAtIndex(moveTo);
                if (captured == None)
                {
                    sm.Score = history.HistoryForMove(stm, m);
                }
                else
                {
                    sm.Score = (captured * 12) - pos.MovedPiece(m);
                }
            }
        }


        public static void AssignQSearchScores(Position pos, ref MoveList list, Move ttMove)
        {
            ref Bitboard bb = ref pos.bb;
            int size = list.Size;

            for (int i = 0; i < size; i++)
            {
                ref ScoredMove sm = ref list[i];
                Move m = sm.Move;
                var (moveFrom, moveTo) = m.Unpack();

                int thisPiece = bb.GetPieceAtIndex(moveFrom);
                int captured = bb.GetPieceAtIndex(moveTo);
                Assert(captured != None);

                if (m.Equals(ttMove))
                {
                    sm.Score = int.MaxValue - 1000000;
                }
                else if (captured != None)
                {
                    //  Approximately mvv lva.
                    sm.Score = (GetHandValue(captured) * 10) - GetHandValue(thisPiece);
                }
            }
        }

        public static Move NextMove(ScoredMove* moves, int size, int listIndex)
        {
            return moves[listIndex].Move;
        }

        public static Move OrderNextMove(ref MoveList list, int listIndex)
        {
            int max = int.MinValue;
            int maxIndex = listIndex;
            int size = list.Size;

            for (int i = listIndex; i < size; i++)
            {
                if (list[i].Score > max)
                {
                    max = list[i].Score;
                    maxIndex = i;
                }
            }

            (list[maxIndex], list[listIndex]) = (list[listIndex], list[maxIndex]);

            return list[listIndex].Move;
        }
    }
}
