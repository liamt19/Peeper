using Peeper.Logic.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Core
{
    public unsafe partial class Position
    {
        public void AddPawnMoves(ref MoveList list)
        {
            int stm = ToMove;
            var up = ShiftUpDir(stm);

            var us = bb.Colors[stm];

            Bitmask ourPawns = bb.Pieces[Pawn] & us;

            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            Bitmask forcePromoMask = ForcedPromotionSquares(stm, Pawn);

            var normalPushes = ourPawns.Shift(up) & ~us;
            var promotions = normalPushes & promoSquares;

            normalPushes &= ~forcePromoMask;
            while (normalPushes != 0)
            {
                int to = PopLSB(&normalPushes);
                list.AddMove(new(to - up, to));
            }

            while (promotions != 0)
            {
                int to = PopLSB(&promotions);
                list.AddMove(new(to - up, to, Move.FlagPromotion));
            }
        }

        public void AddNormalMoves(ref MoveList list, int type)
        {
            int stm = ToMove;

            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            var forcePromoMask = ForcedPromotionSquares(stm, type);
            bool doPromos = Piece.CanPromote(type);

            var occ = bb.Occupancy;
            var us = bb.Colors[stm];
            var ourPieces = bb.Pieces[type] & us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = bb.GetPieceAttacks(stm, type, sq, occ) & ~us;
                var promos = moves & promoSquares;
                if (promoSquares.HasBit(sq))
                {
                    promos = moves;
                }

                moves &= ~forcePromoMask;
                while (moves != 0)
                {
                    int to = PopLSB(&moves);
                    list.AddMove(new(sq, to));
                }

                if (!doPromos)
                    continue;

                while (promos != 0)
                {
                    int to = PopLSB(&promos);
                    list.AddMove(new(sq, to, Move.FlagPromotion));
                }
            }
        }

        public void AddDropMoves(ref MoveList list)
        {
            int stm = ToMove;
            var occ = bb.Occupancy;

            ref Hand ourHand = ref State->Hands[stm];
            foreach (var type in Piece.DroppableTypes)
            {
                int n = ourHand.NumHeld(type);
                if (n == 0)
                    continue;

                var dropFlag = Move.DropFlagFor(type);
                var dropMask = AllMask & ~(occ | ForcedPromotionSquares(stm, type));
                while (dropMask != 0)
                {
                    int to = PopLSB(&dropMask);
                    list.AddMove(new(DropSourceSquare, to, dropFlag));
                }
            }
        }

        public void AddAllMoves(ref MoveList list)
        {
            AddPawnMoves(ref list);

            AddNormalMoves(ref list, Lance);
            AddNormalMoves(ref list, Knight);
            AddNormalMoves(ref list, Silver);
            AddNormalMoves(ref list, Bishop);
            AddNormalMoves(ref list, Rook);

            AddNormalMoves(ref list, PawnPromoted);
            AddNormalMoves(ref list, LancePromoted);
            AddNormalMoves(ref list, KnightPromoted);
            AddNormalMoves(ref list, SilverPromoted);
            AddNormalMoves(ref list, BishopPromoted);
            AddNormalMoves(ref list, RookPromoted);

            AddNormalMoves(ref list, Gold);
            AddNormalMoves(ref list, King);

            AddDropMoves(ref list);
        }

        public int GenerateLegal(ref MoveList list)
        {
            AddAllMoves(ref list);

            int curr = 0;
            int end = list.Size;

            while (curr < end)
            {
                if (!IsLegal(list[curr].Move))
                {
                    end--;
                    list[curr] = list[end];
                }
                else
                {
                    curr++;
                }
            }

            list.Resize(curr);
            return list.Size;
        }
    }
}
