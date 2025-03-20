using System.Runtime.CompilerServices;

namespace Peeper.Logic.Data
{
    [InlineArray(MoveListSize)]
    public struct ScoredMoveBuffer { ScoredMove _; }

    [InlineArray(MaxPly)]
    public struct PVMoveBuffer { Move _; }

    [InlineArray(PieceNB)]
    public struct BitmaskBuffer14 { Bitmask _; }

    [InlineArray(ColorNB)]
    public struct BitmaskBuffer2 { Bitmask _; }

    [InlineArray(ColorNB)]
    public struct HandBuffer { Hand _; }
}
