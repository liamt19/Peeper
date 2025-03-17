
using System.Runtime.InteropServices;

namespace Peeper.Logic.Datagen
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct PeeperFormatEntry
    {
        private const uint HandMask = 0xFFFFFFFF;
        private const uint STMMask = 0x1;
        private const uint WDLMask = 0x3;

        private const int HandShift = 96;
        private const int STMShift = 90;
        private const int WDLShift = 88;

        public const int ByteSize = 64;

        [FieldOffset( 0)] public Bitmask BlackOcc;
        [FieldOffset(16)] public Bitmask WhiteOcc;
        [FieldOffset(32)] public fixed byte PiecesBuff[20];
        [FieldOffset(52)] public short Score;
        [FieldOffset(54)] public ushort Ply;
        [FieldOffset(56)] public fixed byte _pad[8];

        public int BlackHand
        {
            get => (int)((BlackOcc >> HandShift) & HandMask);
            set => BlackOcc = (BlackOcc & ~((Bitmask)HandMask << HandShift)) | ((Bitmask)value) << HandShift;
        }

        public int WhiteHand
        {
            get => (int)((WhiteOcc >> HandShift) & HandMask);
            set => WhiteOcc = (WhiteOcc & ~((Bitmask)HandMask << HandShift)) | ((Bitmask)value) << HandShift;
        }

        public int STM
        {
            get => (int)((BlackOcc >> WDLShift) & STMShift);
            set => BlackOcc = (BlackOcc & ~((Bitmask)STMMask << STMShift)) | ((Bitmask)value) << STMShift;
        }

        public int WDL
        {
            get => (int)((BlackOcc >> WDLShift) & WDLMask);
            set => BlackOcc = (BlackOcc & ~((Bitmask)WDLMask << WDLShift)) | ((Bitmask)value) << WDLShift;
        }


        public static PeeperFormatEntry FromPosition(Position pos, short score, GameResult result)
        {
            ref Bitboard bb = ref pos.bb;

            PeeperFormatEntry entry = new PeeperFormatEntry
            {
                BlackOcc = bb.Colors[Black],
                WhiteOcc = bb.Colors[White],
                Score = score,
                Ply = (byte)(2 * score),
                STM = pos.ToMove,
                WDL = (int)result,
                BlackHand = pos.State->Hands[Black].Data,
                WhiteHand = pos.State->Hands[White].Data,
            };

            int i = 0;
            var mask = bb.Colors[Black];
            while (mask != 0)
            {
                int sq = PopLSB(&mask);
                int type = pos.GetPieceAtIndex(sq);
                entry.PiecesBuff[i / 2] |= (byte)type;
                i++;
            }

            mask = bb.Colors[White];
            while (mask != 0)
            {
                int sq = PopLSB(&mask);
                int type = pos.GetPieceAtIndex(sq);
                entry.PiecesBuff[i / 2] |= (byte)type;
                i++;
            }

            return entry;
        }

        public void WriteToBuffer(Span<byte> buff)
        {
            fixed (void* buffPtr = &buff[0], thisPtr = &this)
                Unsafe.CopyBlock(buffPtr, thisPtr, PeeperFormatEntry.ByteSize);
        }
    }
}
