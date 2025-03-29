
using Peeper.Logic.Search.History;
using static Peeper.Logic.Evaluation.MaterialCounting;

namespace Peeper.Logic.Search.Ordering
{
    public ref struct MovePicker
    {
        private readonly Position pos;
        public MoveList MoveList;
        public MovePickerStage Stage;

        private readonly Move TTMove = Move.Null;
        private bool SkipQuiets = false;

        private readonly int STM;

        private int MoveIndex = 0;
        private int LastIndex = 0;

        public MovePicker(Position pos, Move ttMove, bool negamax)
        {
            MoveList = new();

            bool inCheck = pos.Checked;
            Stage = negamax ? MovePickerStage.NM_TT
                  : inCheck ? MovePickerStage.QS_EvasionsTT
                  :           MovePickerStage.QS_TT;

            this.pos = pos;
            TTMove = ttMove;
            STM = pos.ToMove;

            if (!TTMove || !pos.IsPseudoLegal(TTMove))
                Stage++;
        }

        public static MovePicker Negamax(Position pos, Move ttMove) => new(pos, ttMove, true);
        public static MovePicker QSearch(Position pos, Move ttMove) => new(pos, ttMove, false);


        public void StartSkippingQuiets() => SkipQuiets = true;


        public Move Next()
        {
            Move move;
            switch (Stage)
            {
                case MovePickerStage.NM_TT:
                case MovePickerStage.QS_TT:
                case MovePickerStage.QS_EvasionsTT:
                {
                    Stage++;
                    return TTMove;
                }

                case MovePickerStage.NM_GenCaptures:
                {
                    int added = pos.AddCaptures(ref MoveList);
                    LastIndex = MoveList.Size;

                    ScoreCaptures();

                    Stage++;
                    goto case MovePickerStage.NM_PlayCaptures;
                    //  Fallthrough
                }
                case MovePickerStage.NM_PlayCaptures:
                {
                    if (move = SelectNotTT())
                    {
                        return move;
                    }

                    Stage++;
                    goto case MovePickerStage.NM_GenQuiets;
                    //  Fallthrough
                }
                case MovePickerStage.NM_GenQuiets:
                {
                    if (!SkipQuiets)
                    {
                        int added = pos.AddQuiets(ref MoveList);
                        LastIndex += added;

                        ScoreQuiets();
                    }

                    Stage++;
                    goto case MovePickerStage.NM_PlayQuiets;
                    //  Fallthrough
                }
                case MovePickerStage.NM_PlayQuiets:
                {
                    if (!SkipQuiets)
                    {
                        if (move = SelectNotTT())
                        {
                            return move;
                        }
                    }

                    Stage = MovePickerStage.End;
                    goto case MovePickerStage.End;
                }


                case MovePickerStage.QS_GenCaptures:
                {
                    int added = pos.AddCaptures(ref MoveList);
                    LastIndex = MoveList.Size;

                    ScoreCaptures();

                    Stage++;
                    goto case MovePickerStage.QS_PlayCaptures;
                    //  Fallthrough
                }
                case MovePickerStage.QS_PlayCaptures:
                {
                    if (move = SelectNotTT())
                    {
                        return move;
                    }

                    Stage = MovePickerStage.End;
                    goto case MovePickerStage.End;
                }

                case MovePickerStage.QS_GenEvasions:
                {
                    int added = pos.GeneratePseudoLegal(ref MoveList);
                    LastIndex = MoveList.Size;

                    ScoreEvasions();

                    Stage++;
                    goto case MovePickerStage.QS_PlayEvasions;
                    //  Fallthrough
                }
                case MovePickerStage.QS_PlayEvasions:
                {
                    if (move = SelectNotTT())
                    {
                        return move;
                    }

                    goto case MovePickerStage.End;
                }

                case MovePickerStage.End:
                {
                    return Move.Null;
                }

                default:
                {
                    FailFast($"{pos.GetSFen()} entered stage {Stage}\t{TTMove}\t{MoveList.ToString()}");
                    return Move.Null;
                }
            }

            return Move.Null;
        }


        private void ScoreCaptures()
        {
            for (int i = MoveIndex; i < LastIndex; i++)
            {
                Move m = MoveList[i].Move;

                int thisPiece = pos.MovedPiece(m);
                int captured = pos.GetPieceAtIndex(m.To);

                MoveList[i].Score = (captured * 100) - thisPiece;
            }
        }

        private void ScoreQuiets()
        {
            ref ThreadHistory history = ref pos.Owner.History;
            for (int i = MoveIndex; i < LastIndex; i++)
            {
                Move m = MoveList[i].Move;
                MoveList[i].Score = history.HistoryForMove(STM, m);
            }
        }

        private void ScoreEvasions()
        {
            ref ThreadHistory history = ref pos.Owner.History;
            for (int i = MoveIndex; i < LastIndex; i++)
            {
                Move m = MoveList[i].Move;
                var (moveFrom, moveTo) = m.Unpack();

                int captured = pos.GetPieceAtIndex(moveTo);
                if (captured != None)
                {
                    int thisPiece = pos.MovedPiece(m);
                    MoveList[i].Score = (captured * 100) - thisPiece;
                }
                else
                {
                    MoveList[i].Score = history.HistoryForMove(STM, m);
                }
            }
        }


        private Move SelectNotTT()
        {
            while (MoveIndex < LastIndex)
            {
                var idx = FindNext();
                var move = MoveList[idx].Move;

                if (move != TTMove)
                    return move;
            }

            return Move.Null;
        }

        private Move SelectEverything()
        {
            while (MoveIndex < LastIndex)
            {
                var idx = FindNext();
                return MoveList[idx].Move;
            }

            return Move.Null;
        }


        private int FindNext()
        {
            int maxIndex = MoveIndex;
            int maxValue = MoveList[MoveIndex].Score;

            for (int i = MoveIndex + 1; i < LastIndex; i++)
            {
                if (MoveList[i].Score > maxValue)
                {
                    maxIndex = i;
                    maxValue = MoveList[maxIndex].Score;
                }
            }

            (MoveList[maxIndex], MoveList[MoveIndex]) = (MoveList[MoveIndex], MoveList[maxIndex]);

            return MoveIndex++;
        }
    }

    public enum MovePickerStage
    {
        NM_TT = 0,
        NM_GenCaptures,
        NM_PlayCaptures,
        NM_GenQuiets,
        NM_PlayQuiets,

        QS_TT = 5,
        QS_GenCaptures,
        QS_PlayCaptures,

        QS_EvasionsTT = 8,
        QS_GenEvasions,
        QS_PlayEvasions,

        End,
    }
}
