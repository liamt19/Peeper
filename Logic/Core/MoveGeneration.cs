using Peeper.Logic.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Core
{
    public unsafe partial class Position
    {
        public void AddPawnMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var up = ShiftUpDir(stm);
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            var forcePromoMask = (stm == Black ? RankA_Mask : RankI_Mask);

            var us = bb.Colors[stm];
            var ourPieces = bb.Pieces[Pawn] & us;
            var targets = targetSquares ?? ~us;

            var moves = ourPieces.Shift(up) & targets;
            var promos = moves & promoSquares;

            moves &= ~forcePromoMask;
            while (moves != 0)
            {
                int to = PopLSB(&moves);
                list.AddMove(new(to - up, to));
            }

            while (promos != 0)
            {
                int to = PopLSB(&promos);
                list.AddMove(Move.MakePromo(to - up, to));
            }
        }

        public void AddLanceMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            var forcePromoMask = (stm == Black ? RankA_Mask : RankI_Mask);

            Bitmask occ = bb.Occupancy, us = bb.Colors[stm];
            var ourPieces = bb.Pieces[Lance] & us;
            var targets = targetSquares ?? ~us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);

                var moves = GetLanceMoves(stm, sq, occ) & targets;
                var promos = moves & promoSquares;
                
                //  Every move can be a promotion if the move begins within the promo zone
                if (promoSquares.HasBit(sq))
                    promos = moves;

                moves &= ~forcePromoMask;
                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));

                while (promos != 0)
                    list.AddMove(Move.MakePromo(sq, PopLSB(&promos)));
            }
        }

        public void AddKnightMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            var forcePromoMask = (stm == Black ? RankAB_Mask : RankHI_Mask);

            Bitmask us = bb.Colors[stm];
            var ourPieces = bb.Pieces[Knight] & us;
            var targets = targetSquares ?? ~us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);

                var moves = KnightMoveMask(stm, sq) & targets;
                var promos = moves & promoSquares;

                moves &= ~forcePromoMask;
                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));

                while (promos != 0)
                    list.AddMove(Move.MakePromo(sq, PopLSB(&promos)));
            }
        }

        public void AddSilverMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);

            Bitmask us = bb.Colors[stm];
            var ourPieces = bb.Pieces[Silver] & us;
            var targets = targetSquares ?? ~us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);

                var moves = SilverMoveMask(stm, sq) & targets;
                var promos = moves & promoSquares;

                if (promoSquares.HasBit(sq))
                    promos = moves;

                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));

                while (promos != 0)
                    list.AddMove(Move.MakePromo(sq, PopLSB(&promos)));
            }
        }

        public void AddGoldMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;

            Bitmask us = bb.Colors[stm];
            var ourPieces = bb.Golds() & us;
            var targets = targetSquares ?? ~us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = GoldMoveMask(stm, sq) & targets;
                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));
            }
        }

        public void AddBishopMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);

            Bitmask occ = bb.Occupancy, us = bb.Colors[stm];
            var targets = targetSquares ?? ~us;

            var ourPieces = bb.Pieces[Bishop] & us;
            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = GetBishopMoves(sq, occ) & targets;
                var promos = moves & promoSquares;

                if (promoSquares.HasBit(sq))
                    promos = moves;

                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));

                while (promos != 0)
                    list.AddMove(Move.MakePromo(sq, PopLSB(&promos)));
            }

            ourPieces = bb.Pieces[BishopPromoted] & us;
            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = GetPromotedBishopMoves(sq, occ) & targets;

                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));
            }
        }

        public void AddRookMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            int stm = ToMove;
            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);

            Bitmask occ = bb.Occupancy, us = bb.Colors[stm];
            var targets = targetSquares ?? ~us;

            var ourPieces = bb.Pieces[Rook] & us;
            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = GetRookMoves(sq, occ) & targets;
                var promos = moves & promoSquares;

                if (promoSquares.HasBit(sq))
                    promos = moves;

                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));

                while (promos != 0)
                    list.AddMove(Move.MakePromo(sq, PopLSB(&promos)));
            }

            ourPieces = bb.Pieces[RookPromoted] & us;
            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = GetPromotedRookMoves(sq, occ) & targets;

                while (moves != 0)
                    list.AddMove(new(sq, PopLSB(&moves)));
            }
        }

        public void AddKingMoves(ref MoveList list, Bitmask? targetSquares = null)
        {
            Bitmask us = bb.Colors[ToMove];
            int sq = State->KingSquares[ToMove];

            var targets = targetSquares ?? ~us;
            var moves = KingMoveMask(sq) & targets;
            while (moves != 0)
                list.AddMove(new(sq, PopLSB(&moves)));
        }

        public void AddDropMoves(ref MoveList list)
        {
            int stm = ToMove;
            var occ = bb.Occupancy;

            var ourHand = State->Hands[stm];
            foreach (var type in Piece.DroppableTypes)
            {
                if (ourHand.NumHeld(type) == 0)
                    continue;

                var dropMask = AllMask & ~(occ | ForcedPromotionSquares(stm, type));
                while (dropMask != 0)
                {
                    int to = PopLSB(&dropMask);
                    list.AddMove(Move.MakeDrop(type, to));
                }
            }
        }


        public void AddAllMoves(ref MoveList list)
        {
            AddPawnMoves(ref list);
            AddLanceMoves(ref list);
            AddKnightMoves(ref list);
            AddSilverMoves(ref list);
            AddGoldMoves(ref list);
            AddBishopMoves(ref list);
            AddRookMoves(ref list);
            AddKingMoves(ref list);

            AddDropMoves(ref list);
        }

        public int AddCaptures(ref MoveList list)
        {
            Bitmask targets = bb.Colors[Not(ToMove)];
            int prevSize = list.Size;

            AddPawnMoves(ref list, targets);
            AddLanceMoves(ref list, targets);
            AddKnightMoves(ref list, targets);
            AddSilverMoves(ref list, targets);
            AddGoldMoves(ref list, targets);
            AddBishopMoves(ref list, targets);
            AddRookMoves(ref list, targets);
            AddKingMoves(ref list, targets);

            return list.Size - prevSize;
        }

        public int AddQuiets(ref MoveList list)
        {
            Bitmask targets = AllMask ^ bb.Occupancy;
            int prevSize = list.Size;

            AddPawnMoves(ref list, targets);
            AddLanceMoves(ref list, targets);
            AddKnightMoves(ref list, targets);
            AddSilverMoves(ref list, targets);
            AddGoldMoves(ref list, targets);
            AddBishopMoves(ref list, targets);
            AddRookMoves(ref list, targets);
            AddKingMoves(ref list, targets);

            AddDropMoves(ref list);

            return list.Size - prevSize;
        }


        public int GenerateLegal(ref MoveList list)
        {
            list.Clear();
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


        public int GeneratePseudoLegal(ref MoveList list)
        {
            list.Clear();
            AddAllMoves(ref list);
            return list.Size;
        }

        public int GenerateCaptures(ref MoveList list)
        {
            list.Clear();
            AddCaptures(ref list);
            return list.Size;
        }

        public int GenerateQSearch(ref MoveList list)
        {
            return Checked ? GeneratePseudoLegal(ref list) : GenerateCaptures(ref list);
        }
    }
}
