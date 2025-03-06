using Peeper.Logic.Data;
using Peeper.Logic.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Core
{
    public unsafe struct Bitboard
    {
        public PieceBuffer Pieces;
        public ColorBuffer Colors;

        public fixed int Mailbox[SquareNB];

        public void Clear()
        {
            for (int i = 0; i < PieceNB; i++)
                Pieces[i] = Bitmask.Zero;

            for (int i = 0; i < ColorNB; i++)
                Colors[i] = Bitmask.Zero;

            for (int i = 0; i < SquareNB; i++)
                Mailbox[i] = None;
        }

        public void AddPiece(int sq, int color, int type)
        {
            Assert(!Pieces[type].HasBit(sq), $"AddPiece({sq}, {color}, {type}) Piece was already set!");
            Assert(!Colors[color].HasBit(sq), $"AddPiece({sq}, {color}, {type}) Color was already set!");
            Assert(Mailbox[sq] == None, $"AddPiece({sq}, {color}, {type}) Mailbox has a {PieceToString(Mailbox[sq])} on it!");

            Pieces[type] |= SquareBB(sq);
            Colors[color] |= SquareBB(sq);
            Mailbox[sq] = type;
        }

        public void RemovePiece(int sq, int color, int type)
        {
            Assert(Pieces[type].HasBit(sq), $"RemovePiece({sq}, {color}, {type}) Piece was not set!");
            Assert(Colors[color].HasBit(sq), $"RemovePiece({sq}, {color}, {type}) Color was not set!");
            Assert(Mailbox[sq] != None, $"RemovePiece({sq}, {color}, {type}) Mailbox doesn't have a piece on it!");

            Pieces[type] ^= SquareBB(sq);
            Colors[color] ^= SquareBB(sq);
            Mailbox[sq] = None;
        }

        [MethodImpl(Inline)]
        public int GetColorAtIndex(int idx)
        {
            Assert(Mailbox[idx] != None, $"GetColorAtIndex({idx}) Called on an empty square!");
            return (Colors[Black] & SquareBB(idx)) != 0 ? White : Black;
        }

        [MethodImpl(Inline)]
        public int GetPieceAtIndex(int idx)
        {
            return Mailbox[idx];
        }

        public override string ToString()
        {
            return PrintBoard(this);
        } 
    }

    [InlineArray(PieceNB)]
    public struct PieceBuffer { Bitmask _b; }

    [InlineArray(ColorNB)]
    public struct ColorBuffer { Bitmask _b; }
}
