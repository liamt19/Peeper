
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()

namespace Peeper.Logic.Data
{
    public struct ScoredMove
    {
        public static readonly ScoredMove Null = new ScoredMove(Move.Null);

        public Move Move = Move.Null;
        public int Score = ScoreNone;

        public ScoredMove(Move m, int score = 0)
        {
            this.Move = m;
            this.Score = score;
        }

        public override string ToString()
        {
            return $"{Move}, {Score}";
        }

        public static bool operator <(ScoredMove a, ScoredMove b) => a.Score < b.Score;
        public static bool operator >(ScoredMove a, ScoredMove b) => a.Score > b.Score;

        public static bool operator ==(ScoredMove a, ScoredMove b) => a.Move == b.Move;
        public static bool operator !=(ScoredMove a, ScoredMove b) => a.Move != b.Move;

        public bool Equals(ScoredMove other)
        {
            return Score == other.Score && Move.Equals(other.Move);
        }
    }
}
