
using Peeper.Logic.Evaluation;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Peeper.Logic.Core
{
    public unsafe struct BoardState
    {
        public static readonly uint StateCopySize = (uint)Marshal.OffsetOf<BoardState>(nameof(Accumulator));
        public const int StateStackSize = 3072;

        public BitmaskBuffer2 BlockingPieces;
        public BitmaskBuffer2 Pinners;
        public Bitmask Checkers = 0;
        public HandBuffer Hands;
        public ulong Hash;
        public fixed int KingSquares[2];
        public fixed int ConsecutiveChecks[2];
        public int CapturedPiece = None;
        public Accumulator* Accumulator;

        public BoardState() { }
    }
}
