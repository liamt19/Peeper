
using Peeper.Logic.Data;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Search.History;
using Peeper.Logic.Threads;
using Peeper.Logic.Transposition;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

using static Peeper.Logic.Evaluation.MaterialCounting;
using static Peeper.Logic.Transposition.TTEntry;

namespace Peeper.Logic.Search
{
    public static unsafe class Searches
    {
        public static int Negamax<NodeType>(Position pos, SearchStack* ss, int alpha, int beta, int depth) where NodeType : SearchNodeType
        {
            bool isRoot = typeof(NodeType) == typeof(RootNode);
            bool isPV = typeof(NodeType) != typeof(NonPVNode);

            SearchThread thisThread = pos.Owner;
            TranspositionTable TT = thisThread.TT;

            if (!isRoot && thisThread.IsMain)
            {
                thisThread.CheckLimits();

                if (thisThread.ShouldStop())
                    return ScoreDraw;
            }

            if (depth == 0)
                return QSearch<NodeType>(pos, ss, alpha, beta);

            thisThread.Nodes++;

            ref HistoryTable history = ref thisThread.History;
            ref Bitboard bb = ref pos.bb;

            int us = pos.ToMove;

            Move bestMove = Move.Null;

            int score = -ScoreInfinite;
            int bestScore = -ScoreInfinite;

            short rawEval = 0;
            short eval = ss->StaticEval;
            int startingAlpha = alpha;

            if (isPV)
                thisThread.SelDepth = Math.Max(thisThread.SelDepth, ss->Ply + 1);

            if (!isRoot)
            {
                if (thisThread.ShouldStop() || ss->Ply >= MaxSearchStackPly - 1)
                {
                    return pos.Checked ? ScoreDraw : NNUE.GetEvaluation(pos);
                }

#if NO
                alpha = Math.Max(MakeMateScore(ss->Ply), alpha);
                beta = Math.Min(ScoreMate - (ss->Ply + 1), beta);
                if (alpha >= beta)
                {
                    return alpha;
                }
#endif
            }

            ss->InCheck = pos.Checked;
            ss->TTHit = TT.Probe(pos.Hash, out TTEntry* tte);
            ss->TTPV = isPV || (ss->TTHit && tte->PV);

            short ttScore = ss->TTHit ? MakeNormalScore(tte->Score, ss->Ply) : ScoreNone;
            Move ttMove = isRoot ? thisThread.CurrentMove : (ss->TTHit ? tte->BestMove : Move.Null);

            if (!isPV
                && tte->Depth >= depth
                && ttScore != ScoreNone
                && tte->IsScoreUsable(ttScore, beta))
            {
                return ttScore;
            }

            if (ss->InCheck)
            {
                //  If we are in check, don't bother getting a static evaluation or pruning.
                ss->StaticEval = eval = ScoreNone;
                goto MovesLoop;
            }
#if TODO
            else if (ss->TTHit)
            {
                rawEval = tte->StatEval != ScoreNone ? tte->StatEval : NNUE.GetEvaluation(pos);
                eval = ss->StaticEval = AdjustEval(thisThread, us, rawEval);

                //  If the ttScore isn't invalid, use that score instead of the static eval.
                if (ttScore != ScoreNone && tte->IsScoreUsable(ttScore, eval))
                    eval = ttScore;
            }
            else
            {
                rawEval = NNUE.GetEvaluation(pos);
                eval = ss->StaticEval = AdjustEval(thisThread, us, rawEval);
                tte->Update(pos.Hash, ScoreNone, BoundNone, DepthNone, Move.Null, rawEval, TT.Age, ss->TTPV);
            }
#endif

            eval = ss->StaticEval = NNUE.GetEvaluation(pos);


            if (UseRFP
                && depth <= RFPDepth
#if TODO
                && ttMove.IsNull()
                && !IsWin(eval)
                && eval >= beta
#endif
                && eval - RFPMargin(depth) >= beta)
            {
                return eval;
            }


            if (UseRazoring
                && !isPV
                && depth <= RazoringMaxDepth
                && alpha < 2000
                && ss->StaticEval + RazoringMult * depth <= alpha)
            {
                score = QSearch<NodeType>(pos, ss, alpha, alpha + 1);
                if (score <= alpha)
                    return score;
            }


            if (UseNMP
                && depth >= NMPDepth
                && ss->StaticEval >= beta
                && (ss - 1)->CurrentMove)
            {
                int reduction = NMPBaseRed + (depth / NMPDepthDiv);
                ss->CurrentMove = Move.Null;
                ss->ContinuationHistory = history.Continuations[0][0][0];

                pos.MakeNullMove();
                score = -Negamax<NonPVNode>(pos, ss + 1, -beta, -beta + 1, depth - reduction);
                pos.UnmakeNullMove();

                if (score >= beta)
                {
                    return IsWin(score) ? beta : score;
                }
            }


            MovesLoop:

            int legalMoves = 0;
            int playedMoves = 0;

            MoveList list = new();
            int size = pos.GeneratePseudoLegal(ref list);
            MoveOrdering.AssignScores(pos, ref list, ttMove);

            for (int i = 0; i < size; i++)
            {
                Move m = MoveOrdering.OrderNextMove(ref list, i);

                if (!pos.IsLegal(m))
                {
                    continue;
                }

                var (moveFrom, moveTo) = m.Unpack();

                legalMoves++;
                int R = LMR(depth, legalMoves);

                ss->CurrentMove = m;
                ss->ContinuationHistory = history.Continuations[0][0][0, 0, 0];

                ulong prevNodes = thisThread.Nodes;

                pos.MakeMove(m);

                var sennichite = pos.CheckSennichite(CuteChessWorkaround);
                if (sennichite == Sennichite.Win)
                {
                    legalMoves--;
                    pos.UnmakeMove(m);
                    continue;
                }
                else if (sennichite == Sennichite.Draw)
                {
                    score = MakeDrawScore(thisThread.Nodes);
                    goto SkipSearch;
                }
                else if (pos.IsWinningImpasse())
                {
                    score = MakeImpasseScore(ss->Ply);
                    goto SkipSearch;
                }

                playedMoves++;

                if (isPV)
                    System.Runtime.InteropServices.NativeMemory.Clear((ss + 1)->PV, (nuint)(MaxPly * sizeof(Move)));

                int newDepth = depth - 1;

                if (depth >= 2 && legalMoves >= 6)
                {
                    R += (!isPV).AsInt();

                    //  At least reduce by 1, but never enough to drop into qsearch
                    int reducedDepth = Math.Min(Math.Max(1, newDepth - R), newDepth - 1);

                    score = -Negamax<NonPVNode>(pos, ss + 1, -alpha - 1, -alpha, reducedDepth);

                    if (score > alpha && reducedDepth < newDepth)
                    {
                        score = -Negamax<NonPVNode>(pos, ss + 1, -alpha - 1, -alpha, newDepth);
                    }
                }
                else if (!isPV || legalMoves > 1)
                {
                    score = -Negamax<NonPVNode>(pos, ss + 1, -alpha - 1, -alpha, newDepth);
                }
                
                if (isPV && (playedMoves == 1 || score > alpha))
                {
                    (ss + 1)->PV[0] = Move.Null;
                    score = -Negamax<PVNode>(pos, ss + 1, -beta, -alpha, newDepth);
                }


                SkipSearch:

                pos.UnmakeMove(m);

                if (isRoot)
                    thisThread.NodeTable[moveFrom][moveTo] += thisThread.Nodes - prevNodes;

                if (thisThread.ShouldStop())
                    return ScoreDraw;

                if (isRoot)
                {
                    int rmIndex = -1;
                    for (int j = 0; j < thisThread.RootMoves.Count; j++)
                    {
                        if (thisThread.RootMoves[j].Move == m)
                        {
                            rmIndex = j;
                            break;
                        }
                    }

                    if (rmIndex == -1)
                    {
                        var sfen = pos.GetSFen();
                        var rms = string.Join(", ", thisThread.RootMoves.ToSpan().ToArray().Select(x => x.Move));
                        MoveList pseudoList = new();
                        pos.GeneratePseudoLegal(ref pseudoList);
                        var pseudo = string.Join(", ", pseudoList.ToSpan().ToArray().Select(x => x.Move));
                        FailFast($"{sfen}\tMove {m} wasn't in the RootMoves list! [{rms}]\n\npseudo: [{pseudo}]");
                    }
                    

                    ref RootMove rm = ref thisThread.RootMoves[rmIndex];
                    rm.AverageScore = (rm.AverageScore == -ScoreInfinite) ? score : ((rm.AverageScore + (score * 2)) / 3);

                    if (playedMoves == 1 || score > alpha)
                    {
                        rm.Score = score;
                        rm.Depth = thisThread.SelDepth;

                        rm.PVLength = 1;
                        for (int pvI = 1; pvI < MaxPly - rm.PVLength; pvI++)
                            rm.PV[pvI] = Move.Null;

                        for (Move* childMove = (ss + 1)->PV; *childMove != Move.Null; ++childMove)
                        {
                            rm.PV[rm.PVLength++] = *childMove;
                        }
                    }
                    else
                    {
                        rm.Score = -ScoreInfinite;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = m;

                        if (isPV && !isRoot)
                        {
                            AppendToPV(ss->PV, m, (ss + 1)->PV);
                        }

                        if (score >= beta)
                        {
                            break;
                        }

                        alpha = score;
                    }
                }

            }

            if (legalMoves == 0)
            {
                Assert(!isRoot);
                return MakeMateScore(ss->Ply);
            }

            TTNodeType bound = (bestScore >= beta) ? TTNodeType.Alpha :
                      ((bestScore > startingAlpha) ? TTNodeType.Exact :
                                                     TTNodeType.Beta);

            Move toSave = (bound == TTNodeType.Beta) ? Move.Null : bestMove;

            tte->Update(pos.Hash, MakeTTScore((short)bestScore, ss->Ply), bound, depth, toSave, rawEval, TT.Age, ss->TTPV);

            return bestScore;
        }

        public static int QSearch<NodeType>(Position pos, SearchStack* ss, int alpha, int beta) where NodeType : SearchNodeType
        {
            bool isPV = typeof(NodeType) != typeof(NonPVNode);

            SearchThread thisThread = pos.Owner;

            if (thisThread.IsMain)
            {
                thisThread.CheckLimits();

                if (thisThread.ShouldStop())
                    return ScoreDraw;
            }

            thisThread.Nodes++;

            TranspositionTable TT = thisThread.TT;
            ref HistoryTable history = ref thisThread.History;
            ref Bitboard bb = ref pos.bb;

            int us = pos.ToMove;

            Move bestMove = Move.Null;

            int score = -ScoreInfinite;
            int bestScore = -ScoreInfinite;

            short rawEval = 0;
            int eval = ss->StaticEval;
            int startingAlpha = alpha;
            var bound = TTNodeType.Beta;

            ss->InCheck = pos.Checked;
            ss->TTHit = TT.Probe(pos.Hash, out TTEntry* tte);
            short ttScore = ss->TTHit ? MakeNormalScore(tte->Score, ss->Ply) : ScoreNone;
            Move ttMove = ss->TTHit ? tte->BestMove : Move.Null;
            bool ttPV = ss->TTHit && tte->PV;

            if (isPV)
            {
                ss->PV[0] = Move.Null;
                thisThread.SelDepth = Math.Max(thisThread.SelDepth, ss->Ply + 1);
            }

            if (ss->Ply >= MaxSearchStackPly - 1)
                return ss->InCheck ? ScoreDraw : NNUE.GetEvaluation(pos);

            if (!isPV
                && ttScore != ScoreNone
                && tte->IsScoreUsable(ttScore, beta))
            {
                return ttScore;
            }

            if (ss->InCheck)
            {
                eval = ss->StaticEval = -ScoreInfinite;
                goto MovesLoop;
            }
            if (ss->TTHit)
            {
                rawEval = tte->StatEval != ScoreNone ? tte->StatEval : NNUE.GetEvaluation(pos);

                eval = ss->StaticEval = AdjustEval(thisThread, us, rawEval);
            }
            else
            {
                rawEval = NNUE.GetEvaluation(pos);

                eval = ss->StaticEval = AdjustEval(thisThread, us, rawEval);
            }

            bestScore = eval;

            if (eval >= beta)
                return eval;

            alpha = Math.Max(alpha, eval);

        MovesLoop:

            MoveList list = new();
            int size = pos.GenerateQSearch(ref list);
            MoveOrdering.AssignQSearchScores(pos, ref list, ttMove);

            for (int i = 0; i < size; i++)
            {
                Move m = MoveOrdering.OrderNextMove(ref list, i);

                if (!pos.IsLegal(m))
                {
                    continue;
                }

                var (moveFrom, moveTo) = m.Unpack();

                if (!IsLoss(bestScore))
                {
                    if (!SEE_GE(pos, m, -200))
                        continue;
                }

                ss->CurrentMove = m;
                ss->ContinuationHistory = history.Continuations[0][0][0, 0, 0];

                pos.MakeMove(m);

                var sennichite = pos.CheckSennichite(CuteChessWorkaround);
                if (sennichite == Sennichite.Win)
                {
                    pos.UnmakeMove(m);
                    continue;
                }
                else if (sennichite == Sennichite.Draw)
                {
                    score = MakeDrawScore(thisThread.Nodes);
                    goto SkipSearch;
                }

                score = -QSearch<NodeType>(pos, ss + 1, -beta, -alpha);

                SkipSearch:

                pos.UnmakeMove(m);

                if (thisThread.ShouldStop())
                    return ScoreDraw;

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = m;

                        if (isPV)
                        {
                            AppendToPV(ss->PV, m, (ss + 1)->PV);
                        }

                        if (score >= beta)
                        {
                            bound = TTNodeType.Alpha;
                            break;
                        }

                        alpha = score;
                    }
                }
            }

            tte->Update(pos.Hash, MakeTTScore((short)bestScore, ss->Ply), bound, 0, bestMove, rawEval, TT.Age, ttPV);

            return bestScore;
        }


        private static short AdjustEval(SearchThread thisThread, int us, short score)
        {
            return score;
        }


        private static bool SEE_GE(Position pos, Move m, int threshold = 1)
        {
            ref Bitboard bb = ref pos.bb;

            var (moveFrom, moveTo) = m.Unpack();

            int swap = MaterialCounting.GetPieceValue(bb.GetPieceAtIndex(moveTo)) - threshold;
            if (swap < 0)
                return false;

            var movedPiece = pos.MovedPiece(m);
            swap = MaterialCounting.GetPieceValue(movedPiece) - swap;
            if (swap <= 0)
                return true;

            var fromMask = m.IsDrop ? 0 : SquareBB(moveFrom);
            var occ = (bb.Occupancy ^ fromMask) | SquareBB(moveTo);

            var attackers = bb.AttackersTo(moveTo, occ);
            Bitmask stmAttackers;
            Bitmask temp;

            int stm = pos.ToMove;
            int res = 1;
            while (true)
            {
                stm = Not(stm);
                attackers &= occ;

                stmAttackers = attackers & bb.Colors[stm];
                if (stmAttackers == 0)
                {
                    break;
                }

                if ((pos.State->Pinners[Not(stm)] & occ) != 0)
                {
                    stmAttackers &= ~pos.State->BlockingPieces[stm];
                    if (stmAttackers == 0)
                    {
                        break;
                    }
                }

                res ^= 1;

                if ((temp = stmAttackers & bb.Pieces[Pawn]) != 0)
                {
                    if ((swap = ValuePawn - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[Lance]) != 0)
                {
                    if ((swap = ValueLance - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[Knight]) != 0)
                {
                    if ((swap = ValueKnight - swap) < res)
                        break;
                    occ ^= SquareBB(LSB(temp));

                    continue;
                }
                else if ((temp = stmAttackers & bb.Pieces[Silver]) != 0)
                {
                    if ((swap = ValueSilver - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Golds()) != 0)
                {
                    if ((swap = ValueGold - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[Bishop]) != 0)
                {
                    if ((swap = ValueBishop - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[Rook]) != 0)
                {
                    if ((swap = ValueRook - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[BishopPromoted]) != 0)
                {
                    if ((swap = ValueBishopPromoted - swap) < res)
                        break;
                }
                else if ((temp = stmAttackers & bb.Pieces[RookPromoted]) != 0)
                {
                    if ((swap = ValueRookPromoted - swap) < res)
                        break;
                }
                else
                {
                    if ((attackers & ~bb.Pieces[stm]) != 0)
                    {
                        return (res ^ 1) != 0;
                    }
                    else
                    {
                        return res != 0;
                    }
                }

                int sq = PopLSB(&temp);
                occ ^= SquareBB(sq);

                attackers |= GetBishopMoves(moveTo, occ) & bb.Bishops();
                attackers |= GetRookMoves(moveTo, occ) & bb.Rooks();
            }

            return res != 0;
        }


        private static int RFPMargin(int depth)
        {
            return (depth) * RFPMult;
        }

        private static void AppendToPV(Move* pv, Move move, Move* childPV)
        {
            for (*pv++ = move; childPV != null && *childPV != Move.Null;)
            {
                *pv++ = *childPV++;
            }
            *pv = Move.Null;
        }

        private static string Debug_GetMovesPlayed(SearchStack* ss)
        {
            StringBuilder sb = new StringBuilder();

            while (ss->Ply >= 0)
            {
                sb.Insert(0, ss->CurrentMove.ToString() + ", ");
                ss--;
            }

            if (sb.Length >= 3)
                sb.Remove(sb.Length - 2, 2);

            return sb.ToString();
        }
    }
}
