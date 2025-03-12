
using Peeper.Logic.Data;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Search.History;
using Peeper.Logic.Threads;
using Peeper.Logic.Transposition;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using static Peeper.Logic.Transposition.TTEntry;
using static Peeper.Logic.Evaluation.MaterialCounting;

namespace Peeper.Logic.Search
{
    public static unsafe class Searches
    {

        public static int Negamax<NodeType>(Position pos, SearchStack* ss, int alpha, int beta, int depth) where NodeType : SearchNodeType
        {
            bool isRoot = typeof(NodeType) == typeof(RootNode);
            bool isPV = typeof(NodeType) != typeof(NonPVNode);

            if (depth == 0)
            {
                return QSearch(pos, ss, alpha, beta);
            }

            SearchThread thisThread = pos.Owner;
            TranspositionTable TT = thisThread.TT;

            ref HistoryTable history = ref thisThread.History;
            ref Bitboard bb = ref pos.bb;

            int us = pos.ToMove;

            Move bestMove = Move.Null;

            int score = -ScoreInfinite;
            int bestScore = -ScoreInfinite;

            const short rawEval = 0;
            short eval = ss->StaticEval;
            int startingAlpha = alpha;

            if (thisThread.IsMain)
            {
                bool check = (thisThread.Nodes & 1023) == 1023;
                if (check)
                {
                    //  If we are out of time, stop now.
                    if (TimeManager.CheckHardTime())
                    {
                        thisThread.AssocPool.StopThreads = true;
                    }
                }

                if ((SearchOptions.Threads == 1 && thisThread.Nodes >= thisThread.AssocPool.SharedInfo.HardNodeLimit) ||
                    (check && thisThread.AssocPool.GetNodeCount() >= thisThread.AssocPool.SharedInfo.HardNodeLimit))
                {
                    thisThread.AssocPool.StopThreads = true;
                }
            }

            if (isPV)
            {
                thisThread.SelDepth = Math.Max(thisThread.SelDepth, ss->Ply + 1);
            }

            thisThread.Nodes++;

            if (!isRoot)
            {
                if (pos.IsDraw())
                {
                    return MakeDrawScore(thisThread.Nodes);
                }

                if (thisThread.AssocPool.StopThreads || ss->Ply >= MaxSearchStackPly - 1)
                {
                    return pos.InCheck ? ScoreDraw : GetMaterial(pos);
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

            ss->InCheck = pos.InCheck;
            ss->TTHit = TT.Probe(pos.Hash, out TTEntry* tte);
            ss->TTPV = isPV || (ss->TTHit && tte->PV);

            short ttScore = ss->TTHit ? MakeNormalScore(tte->Score, ss->Ply) : ScoreNone;
            Move ttMove = isRoot ? thisThread.CurrentMove : (ss->TTHit ? tte->BestMove : Move.Null);


            if (ss->InCheck)
            {
                //  If we are in check, don't bother getting a static evaluation or pruning.
                ss->StaticEval = eval = ScoreNone;
                goto MovesLoop;
            }


        MovesLoop:

            int legalMoves = 0;     //  Number of legal moves that have been encountered so far in the loop.
            int playedMoves = 0;    //  Number of moves that have been MakeMove'd so far.

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

                legalMoves++;
                var (moveFrom, moveTo) = m.Unpack();

                ss->CurrentMove = m;
                ss->ContinuationHistory = history.Continuations[0][0][0, 0, 0];

                pos.MakeMove(m);

                playedMoves++;
                ulong prevNodes = thisThread.Nodes;

                if (isPV)
                    System.Runtime.InteropServices.NativeMemory.Clear((ss + 1)->PV, (nuint)(MaxPly * sizeof(Move)));

                int newDepth = depth - 1;

                score = -Negamax<NonPVNode>(pos, ss + 1, -beta, -alpha, newDepth);
                
                pos.UnmakeMove(m);

                if (isRoot)
                {
                    //  Update the NodeTM table with the number of nodes that were searched in this subtree.
                    thisThread.NodeTable[moveFrom][moveTo] += thisThread.Nodes - prevNodes;
                }

                if (thisThread.AssocPool.StopThreads)
                {
                    return ScoreDraw;
                }

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

                    Assert(rmIndex != -1);

                    RootMove rm = thisThread.RootMoves[rmIndex];
                    rm.AverageScore = (rm.AverageScore == -ScoreInfinite) ? score : ((rm.AverageScore + (score * 2)) / 3);

                    if (playedMoves == 1 || score > alpha)
                    {
                        rm.Score = score;
                        rm.Depth = thisThread.SelDepth;

                        rm.PVLength = 1;
                        Array.Fill(rm.PV, Move.Null, 1, MaxPly - rm.PVLength);
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

        public static int QSearch(Position pos, SearchStack* ss, int alpha, int beta)
        {
            SearchThread thisThread = pos.Owner;
            TranspositionTable TT = thisThread.TT;

            thisThread.Nodes++;

            return NNUE.GetEvaluation(pos);
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
