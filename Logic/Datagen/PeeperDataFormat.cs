
using Peeper.Logic.Datagen;
using System.Runtime.InteropServices;

namespace Peeper.Logic.Datagen
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct PeeperDataFormat
    {
        [FieldOffset( 0)] PeeperFormatEntry PFE;
        [FieldOffset(60)] Move BestMove;

        public void SetResult(GameResult res)
        {
            int r = (int)res;
            if (PFE.STM == White)
                r = (2 - r);

            PFE.WDL = r;
        }

        public void Fill(Position pos, Move bestMove, int score)
        {
            PFE = PeeperFormatEntry.FromPosition(pos, (short)score, GameResult.Draw);
            BestMove = bestMove;
        }
    }
}
