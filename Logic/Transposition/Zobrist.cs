using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Peeper.Logic.Transposition
{
    public static unsafe class Zobrist
    {
        private const int DefaultSeed = 0xBEEF;

        private static readonly ulong[] PSQHashes = new ulong[ColorNB * PieceNB * SquareNB];
        private static readonly ulong[] HandHashes = new ulong[ColorNB * PieceNB * Hand.MaxHeld];
        private static ulong BlackHash;
        private static readonly Random rand = new(DefaultSeed);

        public static ulong ColorHash => BlackHash;

        private static int PSQIndex(int color, int type, int square) => (color * PieceNB * SquareNB) + (type * SquareNB) + square;
        private static int HandIndex(int color, int type, int held) => (color * PieceNB * Hand.MaxHeld) + (type * Hand.MaxHeld) + held;

        public static ulong HashForPiece(int color, int type, int sq) => PSQHashes[PSQIndex(color, type, sq)];
        public static ulong HashForHeldPiece(int color, int type, int sq) => HandHashes[HandIndex(color, type, sq)];


        [ModuleInitializer]
        public static void Initialize()
        {
            for (int type = Pawn; type < PieceNB; type++)
            {
                for (int sq = 0; sq < SquareNB; sq++)
                {
                    PSQHashes[PSQIndex(Black, type, sq)] = rand.NextUlong();
                    PSQHashes[PSQIndex(White, type, sq)] = rand.NextUlong();
                }
            }

            for (int type = Pawn; type < PieceNB; type++)
            {
                HandHashes[HandIndex(Black, type, 0)] = HandHashes[HandIndex(White, type, 0)] = 0;
                for (int num = 1; num < Hand.MaxHeld; num++)
                {
                    HandHashes[HandIndex(Black, type, num)] = rand.NextUlong();
                    HandHashes[HandIndex(White, type, num)] = rand.NextUlong();
                }
            }

            BlackHash = rand.NextUlong();
        }

        public static ulong GetHash(Position pos)
        {
            ref Bitboard bb = ref pos.bb;

            ulong hash = 0;

            var black = bb.Colors[Black];
            while (black != 0)
            {
                int sq = PopLSB(&black);
                int type = bb.GetPieceAtIndex(sq);
                hash ^= PSQHashes[PSQIndex(Black, type, sq)];
            }

            var white = bb.Colors[White];
            while (white != 0)
            {
                int sq = PopLSB(&white);
                int type = bb.GetPieceAtIndex(sq);
                hash ^= PSQHashes[PSQIndex(White, type, sq)];
            }

            for (int color = 0; color < ColorNB; color++)
            {
                var thisHand = pos.State->Hands[color];
                if (thisHand.IsEmpty)
                    continue;

                foreach (var type in DroppableTypes)
                {
                    int num = thisHand.NumHeld(type);
                    hash ^= HandHashes[HandIndex(color, type, num)];
                }
            }

            if (pos.ToMove == Black)
            {
                hash ^= BlackHash;
            }

            return hash;
        }


        public static void ZobristMove(this ref ulong hash, int from, int to, int color, int type)
        {
            Assert(from is >= I9 and <= A1, $"ZobristMove({from}, {to}, {color}, {type}) wasn't given a valid From square!");
            Assert(to is >= I9 and <= A1, $"ZobristMove({from}, {to}, {color}, {type}) wasn't given a valid To square!");
            Assert(color is White or Black, $"ZobristMove({from}, {to}, {color}, {type}) wasn't given a valid piece color!");
            Assert(type is >= Pawn and < PieceNB, $"ZobristMove({from}, {to}, {color}, {type}) wasn't given a valid piece type!");

            var fromIndex = PSQIndex(color, type, from);
            var toIndex = PSQIndex(color, type, to);
            ref var start = ref MemoryMarshal.GetArrayDataReference(PSQHashes);

            hash ^= Unsafe.Add(ref start, fromIndex) ^ Unsafe.Add(ref start, toIndex);
        }

        public static void ZobristToggleSquare(this ref ulong hash, int color, int type, int sq)
        {
            Assert(color is White or Black, $"ZobristToggleSquare({color}, {type}, {sq}) wasn't given a valid piece color!");
            Assert(type is >= Pawn and < PieceNB, $"ZobristToggleSquare({color}, {type}, {sq}) wasn't given a valid piece type!");
            Assert(sq is >= I9 and <= A1, $"ZobristToggleSquare({color}, {type}, {sq}) wasn't given a valid square!");

            var index = PSQIndex(color, type, sq);

            hash ^= Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(PSQHashes), index);
        }

        public static void ZobristUpdateHand(this ref ulong hash, int color, int type, int held, int newHeld)
        {
            hash ^= Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(HandHashes), HandIndex(color, type, held));
            hash ^= Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(HandHashes), HandIndex(color, type, newHeld));
        }

        public static void ZobristChangeToMove(this ref ulong hash)
        {
            hash ^= BlackHash;
        }

    }
}
