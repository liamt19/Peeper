using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Peeper.Logic.Datagen
{
    public readonly record struct StoatScoredMove(Move Move, short Score);

    public unsafe ref struct Stoatpack
    {
        public StoatScoredMove[] Moves = new StoatScoredMove[DatagenParameters.WritableDataLimit];
        public Move[] UnscoredMoves = new Move[16];

        public int MoveIndex { get; private set; } = 0;
        public int UnscoredIndex { get; private set; } = 0;

        public Stoatpack() { }

        public void Clear() => MoveIndex = UnscoredIndex = 0;
        public bool IsAtMoveLimit() => (MoveIndex == DatagenParameters.WritableDataLimit - 1);

        public void Push(Move move, short score)
        {
            Moves[MoveIndex++] = new StoatScoredMove(move, score);
            //Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Moves), MoveIndex++) = new StoatScoredMove(move, score);
        }

        public void PushUnscored(Move move)
        {
            UnscoredMoves[UnscoredIndex++] = move;
            //Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(UnscoredMoves), UnscoredIndex++) = move;
        }

        public void AddResultsAndWrite(GameResult gr, BinaryWriter outputWriter)
        {
            byte wdl = (byte)((int)gr << 6);
            outputWriter.Write(wdl);

            ushort unscoredCount = (ushort)UnscoredIndex;
            outputWriter.Write(unscoredCount);
            fixed (void* unscored = UnscoredMoves)
                outputWriter.Write(new Span<byte>(unscored, unscoredCount * sizeof(Move)));

            fixed (void* scored = Moves)
                outputWriter.Write(new Span<byte>(scored, MoveIndex * sizeof(StoatScoredMove)));

            const int NullTerm = 0;
            outputWriter.Write(NullTerm);

            outputWriter.Flush();
        }
    }
}
