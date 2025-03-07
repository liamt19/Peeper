namespace Peeper
{
    public static unsafe class Program
    {
        private static Position pos;
        public static void Main(string[] args)
        {
            pos = new Position();

            Log(pos.ToString());

            MoveList list = new();
            pos.AddAllMoves(ref list);
            Log(list.StringifyByType(pos.bb));

            DoPerftDivide(2);

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
                p.MakeMove(m);
                ulong result = depth > 1 ? p.Perft(depth - 1) : 1;
                p.UnmakeMove(m);
                Log(m.ToString() + ": " + result);
                total += result;
            }
            sw.Stop();

            Log($"\r\nNodes searched: {total} in {sw.Elapsed.TotalSeconds} s ({(int)(total / sw.Elapsed.TotalSeconds):N0} nps)\r\n");
        }
    }
}
