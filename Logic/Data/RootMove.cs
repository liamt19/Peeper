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

        public ulong NodesSpent;

        public PVMoveBuffer PV;
        public int PVLength;

        public RootMove(Move move, int score = -ScoreInfinite)
        {
            ReplaceWith(move, score);
        }


        public void ReplaceWith(Move newMove, int score = -ScoreInfinite)
        {
            Move = newMove;
            Score = PreviousScore = AverageScore = score;
            Depth = 0;

            NodesSpent = 0;

            fixed (Move* ptr = &PV[0])
                new Span<Move>(ptr, MaxPly).Clear();
            PV[0] = newMove;
            PVLength = 1;
        }


        [MethodImpl(Inline)]
        public int CompareTo(RootMove other)
        {
            return (Score != other.Score) ? Score.CompareTo(other.Score)
                                          : PreviousScore.CompareTo(other.PreviousScore);
        }


        public override string ToString() => $"{Move}: {Score}, Avg: {AverageScore}";
    }
}