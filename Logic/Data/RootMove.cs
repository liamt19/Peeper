using System.Runtime.CompilerServices;

namespace Peeper.Logic.Data
{
    public unsafe struct RootMove : IComparable<RootMove>
    {
        public Move Move;

        public int Score;
        public int PreviousScore;
        public int AverageScore;
        public int Depth;

        public PVMoveBuffer PV;
        public int PVLength;

        public RootMove(Move move, int score = -ScoreInfinite)
        {
            Move = move;
            Score = score;
            PreviousScore = score;
            AverageScore = score;
            Depth = 0;

            fixed (Move* ptr = &PV[0])
                new Span<Move>(ptr, MaxPly).Clear();
            PV[0] = move;
            PVLength = 1;
        }

        public void ReplaceWith(Move newMove)
        {
            Move = newMove;
            Score = PreviousScore = AverageScore = -ScoreInfinite;
            Depth = 0;

            fixed (Move* ptr = &PV[0])
                new Span<Move>(ptr, MaxPly).Clear();
            PV[0] = newMove;
            PVLength = 1;
        }

        [MethodImpl(Inline)]
        public int CompareTo(RootMove other)
        {
            if (Score != other.Score)
            {
                return Score.CompareTo(other.Score);
            }
            else
            {
                return PreviousScore.CompareTo(other.PreviousScore);
            }
        }

        public override string ToString()
        {
            return Move.ToString() + ": " + Score + ", Avg: " + AverageScore;
        }
    }
}