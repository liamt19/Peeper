using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Data
{
    public unsafe ref struct MoveList
    {
        public ScoredMoveBuffer Buffer;
        public int Size { get; private set; }

        public MoveList()
        {
            Size = 0;
        }

        [UnscopedRef]
        public ref ScoredMove this[int i] => ref Buffer[i];

        [UnscopedRef]
        public ref ScoredMove Last() => ref Buffer[Size - 1];

        public void AddMove(Move m)
        {
            Buffer[Size++].Move = m;
        }

        public Span<ScoredMove> ToSpan()
        {
            fixed (ScoredMove* buff = &Buffer[0])
                return new Span<ScoredMove>(buff, Size);
        }

        public override string ToString()
        {
            return $"{Size}: {Stringify(ToSpan())}";
        }

        public string StringifyByType(Bitboard bb)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Total of {Size}");

            for (int type = 0; type < PieceNB; type++)
            {
                int n = ToSpan().ToArray().Count(x => bb.GetPieceAtIndex(x.Move.From) == type);
                if (n == 0)
                    continue;

                sb.Append($"{PieceToString(type)}\t{n} -> ");

                for (int i = 0; i < Size; i++)
                {
                    Move m = Buffer[i].Move;
                    var (from, _) = m.Unpack();

                    if (bb.GetPieceAtIndex(from) == type)
                    {
                        sb.Append($"{m} ");
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

}
