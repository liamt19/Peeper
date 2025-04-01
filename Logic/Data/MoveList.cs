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

        [MethodImpl(Inline)]
        public void AddMove(Move m)
        {
            Buffer[Size++].Move = m;
        }

        public void Resize(int newSize) => Size = newSize;
        public void Clear() => Size = 0;

        public Span<ScoredMove> ToSpan()
        {
            fixed (ScoredMove* buff = &Buffer[0])
                return new Span<ScoredMove>(buff, Size);
        }

        public ScoredMove* ToSpicyPointer()
        {
            fixed (ScoredMove* buff = &Buffer[0])
                return buff;
        }

        public override string ToString()
        {
            return $"{Size}: {Stringify(ToSpan(), Size)}";
        }

        public string StringifyByType(Bitboard bb)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Total of {Size}");
            var mArr = ToSpan().ToArray();

            for (int type = 0; type < PieceNB; type++)
            {
                int n = mArr.Count(x => !x.Move.IsDrop && bb.GetPieceAtIndex(x.Move.From) == type);
                if (n == 0)
                    continue;

                sb.Append($"{PieceToString(type)}\t{n} -> ");

                for (int i = 0; i < Size; i++)
                {
                    Move m = Buffer[i].Move;
                    var (from, _) = m.Unpack();

                    if (!m.IsDrop && bb.GetPieceAtIndex(from) == type)
                    {
                        sb.Append($"{m} ");
                    }
                }

                sb.AppendLine();
            }

            int drops = mArr.Count(x => x.Move.IsDrop);
            if (drops != 0)
            {
                sb.AppendLine($"Drops == {drops}");

                foreach (var type in DroppableTypes)
                {
                    int n = mArr.Count(x => x.Move.IsDrop && x.Move.DroppedPiece == type);
                    if (n == 0)
                        continue;

                    sb.Append($"{PieceToString(type)}\t{n} -> ");

                    for (int i = 0; i < Size; i++)
                    {
                        Move m = Buffer[i].Move;
                        if (!m.IsDrop || m.DroppedPiece != type)
                            continue;

                        sb.Append($"{m} ");
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }

}
