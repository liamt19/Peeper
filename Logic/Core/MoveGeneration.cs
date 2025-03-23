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

            var us = bb.Colors[stm];

            Bitmask ourPawns = bb.Pieces[Pawn] & us;
            var targets = targetSquares ?? ~us;

            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            Bitmask forcePromoMask = ForcedPromotionSquares(stm, Pawn);

            var normalPushes = ourPawns.Shift(up) & targets;
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
                list.AddMove(Move.MakePromo(to - up, to));
            }
        }

        public void AddNormalMoves(ref MoveList list, int type, Bitmask? targetSquares = null)
        {
            int stm = ToMove;

            var promoSquares = (stm == Black ? BlackPromotionSquares : WhitePromotionSquares);
            var forcePromoMask = ForcedPromotionSquares(stm, type);
            bool doPromos = Piece.CanPromote(type);

            var occ = bb.Occupancy;
            var us = bb.Colors[stm];
            var ourPieces = bb.Pieces[type] & us;
            var targets = targetSquares ?? ~us;

            while (ourPieces != 0)
            {
                int sq = PopLSB(&ourPieces);
                var moves = bb.GetPieceAttacks(stm, type, sq, occ) & targets;
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
                    list.AddMove(Move.MakePromo(sq, to));
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

            for (int type = Pawn + 1; type < PieceNB; type++)
            {
                AddNormalMoves(ref list, type);
            }

            AddDropMoves(ref list);
        }

        public int AddCaptures(ref MoveList list)
        {
            Bitmask targets = bb.Colors[Not(ToMove)];
            int prevSize = list.Size;

            AddPawnMoves(ref list, targets);
            for (int type = Pawn + 1; type < PieceNB; type++)
            {
                AddNormalMoves(ref list, type, targets);
            }

            return list.Size - prevSize;
        }

        public int AddQuiets(ref MoveList list)
        {
            Bitmask targets = AllMask ^ bb.Occupancy;
            int prevSize = list.Size;

            AddPawnMoves(ref list, targets);
            for (int type = Pawn + 1; type < PieceNB; type++)
            {
                AddNormalMoves(ref list, type, targets);
            }
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

        public int GenerateQuiets(ref MoveList list)
        {
            list.Clear();
            AddQuiets(ref list);
            return list.Size;
        }

        public int GenerateQSearch(ref MoveList list)
        {
            return Checked ? GeneratePseudoLegal(ref list) : GenerateCaptures(ref list);
        }
    }
}
