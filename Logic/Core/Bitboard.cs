using Peeper.Logic.Data;
using Peeper.Logic.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Core
{
    public unsafe struct Bitboard
    {
        public BitmaskBuffer14 Pieces;
        public BitmaskBuffer2 Colors;

        public fixed int Mailbox[SquareNB];

        public Bitmask Occupancy => Colors[Black] | Colors[White];

        public void Clear()
        {
            for (int i = 0; i < PieceNB; i++)
                Pieces[i] = Bitmask.Zero;

            for (int i = 0; i < ColorNB; i++)
                Colors[i] = Bitmask.Zero;

            for (int i = 0; i < SquareNB; i++)
                Mailbox[i] = None;
        }

        [MethodImpl(Inline)]
        public int GetColorAtIndex(int sq)
        {
            Assert(Mailbox[sq] != None, $"GetColorAtIndex({sq}) Called on an empty square!");
            return (Colors[Black] & SquareBB(sq)) != 0 ? Black : White;
        }

        [MethodImpl(Inline)]
        public int GetPieceAtIndex(int sq)
        {
            Assert(sq < SquareNB);
            return Mailbox[sq];
        }

        [MethodImpl(Inline)]
        public int KingIndex(int pc)
        {
            Assert(Popcount(Colors[pc] & Pieces[Piece.King]) != 0, $"{ColorToString(pc)}'s king was removed!");
            return LSB(Colors[pc] & Pieces[Piece.King]);
        }

        [MethodImpl(Inline)]
        public void AddPiece(int color, int type, int sq)
        {
            Assert(!Pieces[type].HasBit(sq), $"AddPiece({color}, {type}, {sq}) Piece was already set!");
            Assert(!Colors[color].HasBit(sq), $"AddPiece({color}, {type}, {sq}) Color was already set!");
            Assert(Mailbox[sq] == None, $"AddPiece({color}, {type}, {sq}) Mailbox has a {PieceToString(Mailbox[sq])} on it!");

            Pieces[type] |= SquareBB(sq);
            Colors[color] |= SquareBB(sq);
            Mailbox[sq] = type;
        }

        [MethodImpl(Inline)]
        public void RemovePiece(int color, int type, int sq)
        {
            Assert(Pieces[type].HasBit(sq), $"RemovePiece({color}, {type}, {sq}) Piece was not set!");
            Assert(Colors[color].HasBit(sq), $"RemovePiece({color}, {type}, {sq}) Color was not set!");
            Assert(Mailbox[sq] != None, $"RemovePiece({color}, {type}, {sq}) Mailbox doesn't have a piece on it!");

            Pieces[type] ^= SquareBB(sq);
            Colors[color] ^= SquareBB(sq);
            Mailbox[sq] = None;
        }


        public Bitmask BlockingPieces(int pc, Bitmask* pinners)
        {
            Bitmask blockers = 0UL;
            *pinners = 0;

            Bitmask temp;
            Bitmask us = Colors[pc];
            Bitmask them = Colors[Not(pc)];

            int ourKing = KingIndex(pc);

            Bitmask candidates = them & (
                (RookRay(ourKing) & (Pieces[Rook] | Pieces[RookPromoted])) | 
                (BishopRay(ourKing) & (Pieces[Bishop] | Pieces[BishopPromoted])) | 
                (LanceRay(pc, ourKing) & Pieces[Lance]));

            var occ = us | them;

            while (candidates != 0)
            {
                int sq = PopLSB(&candidates);

                temp = Between(ourKing, sq) & occ;

                if (temp != 0 && !MoreThanOne(temp))
                {
                    //  If there is one and only one piece between the candidate and our king, that piece is a blocker
                    blockers |= temp;

                    if ((temp & us) != 0)
                    {
                        //  If the blocker is ours, then the candidate on the square "sq" is a pinner
                        *pinners |= SquareBB(sq);
                    }
                }
            }

            return blockers;
        }


        [MethodImpl(Inline)]
        public Bitmask GetPieceAttacks(int color, int type, int sq, Bitmask occ)
        {
            return type switch
            {
                Pawn   => PawnMoveMask(color, sq),
                Lance  => GetLanceMoves(color, sq, occ),
                Knight => KnightMoveMask(color, sq),
                Silver => SilverMoveMask(color, sq),
                Bishop => GetBishopMoves(sq, occ),
                Rook   => GetRookMoves(sq, occ),
                King   => KingMoveMask(sq),

                PawnPromoted or 
                LancePromoted or 
                KnightPromoted or 
                SilverPromoted or 
                Gold    => GoldMoveMask(color, sq),

                RookPromoted   => KingMoveMask(sq) | GetRookMoves(sq, occ),
                BishopPromoted => KingMoveMask(sq) | GetBishopMoves(sq, occ),

                _ => 0
            };
        }

        public Bitmask AttackersTo(int sq, Bitmask occ)
        {
            Bitmask v = 0;

            v |= (Pieces[Pawn] & PawnMoveMask(Black, sq) & Colors[White]);
            v |= (Pieces[Pawn] & PawnMoveMask(White, sq) & Colors[Black]);

            v |= (Pieces[Lance] & GetLanceMoves(Black, sq, occ) & Colors[White]);
            v |= (Pieces[Lance] & GetLanceMoves(White, sq, occ) & Colors[Black]);

            v |= (Pieces[Knight] & KnightMoveMask(Black, sq) & Colors[White]);
            v |= (Pieces[Knight] & KnightMoveMask(White, sq) & Colors[Black]);

            v |= (Pieces[Silver] & SilverMoveMask(Black, sq) & Colors[White]);
            v |= (Pieces[Silver] & SilverMoveMask(White, sq) & Colors[Black]);

            v |= ((Pieces[Bishop] | Pieces[BishopPromoted]) & GetBishopMoves(sq, occ));
            v |= ((Pieces[Rook] | Pieces[RookPromoted]) & GetRookMoves(sq, occ));

            v |= (GoldMoveMask(Black, sq) & (Pieces[PawnPromoted] | Pieces[LancePromoted] | Pieces[KnightPromoted] | Pieces[SilverPromoted] | Pieces[Gold]) & Colors[White]);
            v |= (GoldMoveMask(White, sq) & (Pieces[PawnPromoted] | Pieces[LancePromoted] | Pieces[KnightPromoted] | Pieces[SilverPromoted] | Pieces[Gold]) & Colors[Black]);

            v |= (KingMoveMask(sq) & (Pieces[BishopPromoted] | Pieces[RookPromoted]));

            return v;
        }

        public override string ToString()
        {
            return ActiveFormatter.DisplayBoard(this);
        }

        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d, int e, int f, int g, int h, int i) => PieceMask(a, b, c, d, e, f, g, h) | Pieces[i];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d, int e, int f, int g, int h) => PieceMask(a, b, c, d, e, f, g) | Pieces[h];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d, int e, int f, int g) => PieceMask(a, b, c, d, e, f) | Pieces[g];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d, int e, int f) => PieceMask(a, b, c, d, e) | Pieces[f];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d, int e) => PieceMask(a, b, c, d) | Pieces[e];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c, int d) => PieceMask(a, b, c) | Pieces[d];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b, int c) => PieceMask(a, b) | Pieces[c];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a, int b) => PieceMask(a) | Pieces[b];
        [MethodImpl(Inline)] public Bitmask PieceMask(int a) => Pieces[a];

        [MethodImpl(Inline)] public Bitmask Golds() => PieceMask(PawnPromoted, LancePromoted, KnightPromoted, SilverPromoted, Gold);
        [MethodImpl(Inline)] public Bitmask Bishops() => PieceMask(Bishop, BishopPromoted);
        [MethodImpl(Inline)] public Bitmask Rooks() => PieceMask(Rook, RookPromoted);
    }
}
