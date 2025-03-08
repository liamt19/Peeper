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
                    MoveList list = new();
                    pos.AddAllMoves(ref list);
                    Log(list.StringifyByType(pos.bb));
                }
                else if (cmd.EqualsIgnoreCase("perft"))
                {
                    int depth = param.Length != 0 ? int.Parse(param[0]) : 2;
                    DoPerftDivide(depth);
                }
            }

            Console.ReadLine();
        }


        private static void DoPerftDivide(int depth)
        {
            if (depth <= 0) return;

            ulong total = 0;

            Position p = new Position(pos.GetSFen());

            Stopwatch sw = Stopwatch.StartNew();

            MoveList list = new();
            p.GenerateLegal(ref list);
            int size = list.Size;
            for (int i = 0; i < size; i++)
            {
                Move m = list[i].Move;

                Bitboard temp = pos.bb.DebugClone();

                p.MakeMove(m);
                ulong result = depth > 1 ? p.Perft(depth - 1) : 1;
                p.UnmakeMove(m);
                Log($"{m}: {result}");
                total += result;

                pos.bb.VerifyUnchangedFrom(temp);
            }
            sw.Stop();

            Log($"\r\nNodes searched: {total} in {sw.Elapsed.TotalSeconds} s ({(int)(total / sw.Elapsed.TotalSeconds):N0} nps)\r\n");
        }
    }
}
