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
            var emptySquares = ~bb.Occupancy;

            //Bitmask forcePromoMask = (stm == Black ? RankI_Mask : RankA_Mask);
            Bitmask forcePromoMask = ForcedPromotionSquares(stm, Pawn);

            var normalPushes = ourPawns.Shift(up) & emptySquares;
            var promotions = normalPushes & (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);

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
            var forcePromoMask = ForcedPromotionSquares(stm, type);
            bool doPromos = Piece.CanPromote(type);

            var occ = bb.Occupancy;
            var us = bb.Colors[stm];
            var ourPieces = bb.Pieces[type] & us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = bb.GetPieceAttacks(stm, type, sq, occ) & ~us;
                var promos = moves & (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);

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

        }

        public void AddAllMoves(ref MoveList list)
        {
            AddPawnMoves(ref list);

            AddNormalMoves(ref list, Lance);
            AddNormalMoves(ref list, Knight);
            AddNormalMoves(ref list, Silver);
            AddNormalMoves(ref list, Bishop);
            AddNormalMoves(ref list, Rook);

            AddNormalMoves(ref list, Gold);
            AddNormalMoves(ref list, King);

            AddDropMoves(ref list);
        }

        public void GenerateLegal(ref MoveList list)
        {
            AddAllMoves(ref list);

            int ourKing = State->KingSquares[ToMove];
            int theirKing = State->KingSquares[Not(ToMove)];
            var pinned = State->BlockingPieces[ToMove];

            var s = list.ToSpan();
            int curr = 0;
            int end = list.Size - 1;

            while (curr != end)
            {
                if (!IsLegal(list[curr].Move, ourKing, theirKing, pinned))
                {
                    list[curr] = list[--end];
                }
                else
                {
                    ++curr;
                }
            }
        }
    }
}
