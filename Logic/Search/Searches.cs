
using System.Text;
using System.Xml.Linq;
using static Peeper.Logic.Evaluation.MaterialCounting;

namespace Peeper.Logic.Search
{
    public static unsafe class Searches
    {
        public static ulong Nodes = 0;
        public static Move[] PVAtHome = new Move[MaxPly];

        private static string GetPV()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var move in PVAtHome.Skip(1))
            {
                if (move.IsNull())
                    break;

                sb.Insert(0, move.ToString());
                sb.Insert(0, ' ');
            }
            sb.Remove(0, 1);
            return sb.ToString();
        }


        public static void StartSearch(Position pos, int maxDepth)
        {
            SearchStack* _ss = stackalloc SearchStack[MaxPly];
            SearchStack* ss = _ss + 10;
            for (int i = -10; i < MaxSearchStackPly; i++)
            {
                (ss + i)->Clear();
                (ss + i)->Ply = (short)i;
                (ss + i)->PV = AlignedAllocZeroed<Move>(MaxPly);
            }

            Array.Clear(PVAtHome);

            Stopwatch sw = Stopwatch.StartNew();
            Nodes = 0;

            int score;
            int depth = 0;
            while (depth++ < maxDepth)
            {
                int alpha = -ScoreMate;
                int beta = ScoreMate;

                score = Negamax<RootNode>(pos, ss, alpha, beta, depth);

                var time = Math.Max(1, Math.Round(sw.Elapsed.TotalMilliseconds));
                var nps = (ulong)((double)Nodes / (time / 1000));
                var pv = GetPV();
                Log($"info depth {depth} time {time} score cp {score} nodes {Nodes} nps {nps} pv {pv}");
            }
        }


        public static int Negamax<NodeType>(Position pos, SearchStack* ss, int alpha, int beta, int depth) where NodeType : SearchNodeType
        {
            bool isRoot = typeof(NodeType) == typeof(RootNode);
            bool isPV = typeof(NodeType) != typeof(NonPVNode);

            if (depth == 0)
            {
                return QSearch(pos, ss, alpha, beta);
            }

            int score = -ScoreInfinite;
            int bestScore = -ScoreInfinite;
            Move bestMove = Move.Null;

            MoveList list = new();
            int size = pos.GeneratePseudoLegal(ref list);
            MoveOrdering.AssignScores(pos, ref list);

            for (int i = 0; i < size; i++)
            {
                Move m = MoveOrdering.OrderNextMove(ref list, i);

                if (!pos.IsLegal(m))
                {
                    continue;
                }

                int newDepth = depth - 1;

                pos.MakeMove(m);
                Nodes++;
                score = -Negamax<NonPVNode>(pos, ss + 1, -beta, -alpha, newDepth);
                pos.UnmakeMove(m);

#if NO
                if (isRoot)
                {
                    int rmIndex = 0;
                    for (int j = 0; j < thisThread.RootMoves.Count; j++)
                    {
                        if (thisThread.RootMoves[j].Move == m)
                        {
                            rmIndex = j;
                            break;
                        }
                    }

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
#endif

                if (score > bestScore)
                {
                    bestScore = score;

                    if (score > alpha)
                    {
                        bestMove = m;

#if NO
                        if (isPV && !isRoot)
                        {
                            UpdatePV(ss->PV, m, (ss + 1)->PV);
                        }
#endif
                        PVAtHome[depth] = m;

                        if (score >= beta)
                        {
                            break;
                        }

                        alpha = score;
                    }
                }

            }

            return bestScore;
        }

        public static int QSearch(Position pos, SearchStack* ss, int alpha, int beta)
        {
            return GetMaterial(pos);
        }
    }
}
