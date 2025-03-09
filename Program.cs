namespace Peeper
{
    public static unsafe class Program
    {
        private static Position pos;
        public static void Main(string[] args)
        {
            pos = new Position();

            while (true)
            {
                string input = Console.ReadLine();
                if (input == null || input.Length == 0)
                {
                    continue;
                }
                string[] param = input.Split(' ');
                string cmd = param[0];
                param = param.Skip(1).ToArray();

                if (cmd.EqualsIgnoreCase("d"))
                {
                    Log(pos.ToString());
                }
                else if (cmd.EqualsIgnoreCase("move"))
                {
                    pos.TryMakeMove(param[0]);
                }
                else if(cmd.EqualsIgnoreCase("list"))
                {
                    DoListMoves();
                }
                else if (cmd.EqualsIgnoreCase("b"))
                {
                    DoBenchPerft();
                }
                else if (cmd.EqualsIgnoreCase("perft"))
                {
                    DoPerftDivide(int.Parse(param[0]));
                }
                else if (cmd.EqualsIgnoreCase("perftp"))
                {
                    pos.PerftParallel(int.Parse(param[0]), true);
                }
                else
                {
                    //  You can just copy paste in a FEN string rather than typing "position fen" before it.
                    if (input.Where(x => x == '/').Count() == 8)
                    {
                        if (pos.LoadFromSFen(input.Trim()))
                        {
                            Log($"Loaded fen '{pos.GetSFen()}'");
                        }
                    }
                    else
                    {
                        Log($"Unknown token '{input}'");
                    }
                }
            }

        }


        private static void DoPerftDivide(int depth)
        {
            if (depth <= 0) return;

            ulong total = 0;

            Position p = new Position(pos.GetSFen());
            Stopwatch sw = Stopwatch.StartNew();

            MoveList list = new();
            int size = p.GenerateLegal(ref list);
            for (int i = 0; i < size; i++)
            {
                Move m = list[i].Move;
                p.MakeMove(m);
                ulong result = depth > 1 ? p.Perft(depth - 1) : 1;
                p.UnmakeMove(m);
                Log($"{m}\t{result}");
                total += result;
            }
            sw.Stop();
            Log($"\r\nNodes searched: {total} in {sw.Elapsed.TotalSeconds} s ({(int)(total / sw.Elapsed.TotalSeconds):N0} nps)\r\n");
        }

        private static void DoBenchPerft()
        {
            foreach (var (sfen, depthData) in PerftData.PerftPositions)
            {
                pos.LoadFromSFen(sfen);
                Log($"{sfen}");

                int maxD = Math.Min(depthData.Length, 5);
                for (int depth = 1; depth < maxD; depth++)
                {
                    var correctNodes = depthData[depth];
                    ulong ourNodes = pos.Perft(depth);
                    if (ourNodes != depthData[depth])
                    {
                        Log($"{depth}\t{ourNodes} should be {correctNodes}");
                    }
                    else
                    {
                        Log($"{depth}\t{ourNodes} correct");
                    }
                }
            }
        }

        private static void DoListMoves()
        {
            MoveList list = new();
            pos.GenerateLegal(ref list);
            Log(list.StringifyByType(pos.bb));
        }
    }
}
