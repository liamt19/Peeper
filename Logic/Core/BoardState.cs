
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Peeper.Logic.Core
{
    public unsafe struct BoardState
    {
        public BitmaskBuffer2 BlockingPieces;
        public BitmaskBuffer2 Pinners;
        public Bitmask Checkers = 0;
        public fixed int KingSquares[2];
        public int CapturedPiece = None;

        public BoardState() { }
    }
}
