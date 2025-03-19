
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using Peeper.Logic.Datagen;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Search;
using Peeper.Logic.Threads;
using Peeper.Logic.USI;
using System.Text;

namespace Peeper
{
    public static unsafe class Program
    {
        private static Position pos;
        private static SearchInformation info;
        public static void Main(string[] args)
        {
            if (args.Length != 0 && args[0] == "bench")
            {
                SearchBench.Go(openBench: true);
                Environment.Exit(0);
            }

            pos = new Position(owner: GlobalSearchPool.MainThread);
            info = new SearchInformation(pos);

            InitializeAll();

            DoInputLoop();
        }

        private static void DoInputLoop()
        {
            ThreadSetup setup = new ThreadSetup();

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

                if (cmd.EqualsIgnoreCase("usi"))
                {
                    SetUSIFormatter();
                    USIClient.Run(pos);
                }
                else if (cmd.EqualsIgnoreCase("uci"))
                {
                    SetUCIFormatter();
                    USIClient.Run(pos);
                }
                else if (cmd.EqualsIgnoreCase("position"))
                {
                    HandlePositionCommand(param, setup);
                }
                else if (cmd.StartsWithIgnoreCase("go"))
                {
                    HandleGoCommand(param, setup);
                }
                else if (cmd.EqualsIgnoreCase("stop"))
                {
                    GlobalSearchPool.StopThreads = true;
                }
                else if (cmd.EqualsIgnoreCase("perft"))
                {
                    DoPerftDivide(int.Parse(param[0]));
                }
                else if (cmd.EqualsIgnoreCase("perftp"))
                {
                    pos.PerftParallel(int.Parse(param[0]), true);
                }
                else if (cmd.EqualsIgnoreCase("d"))
                {
                    Log(pos.ToString());
                    DoEvaluate();
                }
                else if (cmd.EqualsIgnoreCase("eval"))
                {
                    DoEvaluate();
                }
                else if (cmd.EqualsIgnoreCase("move"))
                {
                    pos.TryMakeMove(param[0]);
                }
                else if (cmd.EqualsIgnoreCase("list"))
                {
                    DoListMoves();
                }
                else if (input.StartsWithIgnoreCase("bench"))
                {
                    HandleBenchCommand(input);
                }
                else if (input.EqualsIgnoreCase("pgn"))
                {
                    PGNToKIF.ParseFromSTDIn();
                }
                else if (input.StartsWith("datagen"))
                {
                    var splits = input.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    HandleDatagenCommand(splits);
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

        public static void InitializeAll()
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                AppDomain.CurrentDomain.UnhandledException += ExceptionHandling.CurrentDomain_UnhandledException;
            }

            Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) {
                if (!GlobalSearchPool.StopThreads)
                {
                    //  If a search is ongoing, stop it instead of closing the console.
                    GlobalSearchPool.StopThreads = true;
                    e.Cancel = true;
                }

                //  Otherwise, e.Cancel == false and the program exits normally
            };

            //  Quadruple the amount that Console.ReadLine() can handle.
            Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8, false, 4096 * 4));

            //  Give the VS debugger a friendly name for the main program thread
            Thread.CurrentThread.Name = "MainThread";
        }


        private static void HandleGoCommand(string[] param, ThreadSetup setup)
        {
            if (info.SearchActive)
                return;

            Utilities.ParseGoCommand(param, ref info, setup);
            GlobalSearchPool.StartSearch(info.Position, ref info, setup);
        }

        private static void HandlePositionCommand(string[] param, ThreadSetup setup)
        {
            ParsePositionCommand(param, pos, setup);
            Log($"Loaded fen '{pos.GetSFen()}'");
        }

        private static void DoEvaluate()
        {
            var ev = NNUE.GetEvaluation(pos);
            Log($"Evaluation: {ev}");
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


        private static void HandleBenchCommand(string input)
        {
            if (input.ContainsIgnoreCase("perft"))
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
            else
            {
                int depth = SearchBench.DefaultDepth;

                try
                {
                    if (input.Length > 5 && int.TryParse(input.AsSpan(input.IndexOf("bench") + 6), out int newDepth))
                    {
                        depth = newDepth;
                    }
                }
                catch (Exception e)
                {
                    Log("Couldn't parse the bench depth from the input!");
                    Log(e.ToString());
                    return;
                }

                SearchBench.Go(depth);
            }
        }

        private static void DoListMoves()
        {
            MoveList list = new();
            pos.GenerateLegal(ref list);
            Log(list.StringifyByType(pos.bb));
        }

        private static void HandleDatagenCommand(string[] args)
        {
            ulong nodes = DatagenParameters.SoftNodeLimit;
            ulong depth = DatagenParameters.DepthLimit;
            ulong numGames = 1000000;
            ulong threads = 1;

            args = args.Skip(1).ToArray();

            if (ulong.TryParse(args.Where(x => x.EndsWith('n')).FirstOrDefault()?[..^1], out ulong selNodeLimit)) nodes = selNodeLimit;
            if (ulong.TryParse(args.Where(x => x.EndsWith('d')).FirstOrDefault()?[..^1], out ulong selDepthLimit)) depth = selDepthLimit;
            if (ulong.TryParse(args.Where(x => x.EndsWith('g')).FirstOrDefault()?[..^1], out ulong selNumGames)) numGames = selNumGames;
            if (ulong.TryParse(args.Where(x => x.EndsWith('t')).FirstOrDefault()?[..^1], out ulong selThreads)) threads = selThreads;

            Log($"Threads:      {threads}");
            Log($"Games/thread: {numGames:N0}");
            Log($"Total games:  {numGames * threads:N0}");
            Log($"Node limit:   {nodes:N0}");
            Log($"Depth limit:  {depth}");
            Log($"Hit enter to begin...");
            _ = Console.ReadLine();

            ProgressBroker.StartMonitoring();
            if (threads == 1)
            {
                //  Let this run on the main thread to allow for debugging
                Selfplay.RunGames(numGames, 0, softNodeLimit: nodes, depthLimit: depth);
            }
            else
            {
                Parallel.For(0, (int)threads, new() { MaxDegreeOfParallelism = (int)threads }, (int i) =>
                {
                    Selfplay.RunGames(numGames, i, softNodeLimit: nodes, depthLimit: depth);
                });
            }
            ProgressBroker.StopMonitoring();

            Environment.Exit(0);
        }
    }
}
