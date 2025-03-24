
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using Peeper.Logic.Search;
using Peeper.Logic.Threads;
using Peeper.Logic.UCI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Peeper.Logic.USI
{
    public static class USIClient
    {
        private static Position pos;
        private static SearchInformation info;
        private static ThreadSetup setup;

        private static Dictionary<string, USIOption> Options;

        public static bool Active = false;

        public static string[] ReservedNames => [nameof(SearchOptions.Hash)];



        static USIClient()
        {
            ProcessUSIOptions();
            setup = new ThreadSetup();
        }


        public static void Run(Position pos)
        {
            Active = true;

            USIClient.pos = pos;

            info = new SearchInformation(pos);
            info.OnDepthFinish = OnDepthDone;
            info.OnSearchFinish = OnSearchDone;

            Console.WriteLine($"id name Peeper {EngineBuildVersion}");
            Console.WriteLine("id author Liam McGuire");

            PrintUSIOptions();
            ActiveFormatter.SendInitialReadyResponse();

            HandleNewGame(pos.Owner.AssocPool);
            InputLoop();
        }

        private static void InputLoop()
        {
            while (true)
            {
                string[] param = ReceiveString(out string cmd);

                if (cmd == "quit")
                {
                    Environment.Exit(0);
                }
                else if (cmd == "position")
                {
                    info = new SearchInformation(pos);
                    info.OnDepthFinish = OnDepthDone;
                    info.OnSearchFinish = OnSearchDone;

                    ParsePositionCommand(param, pos, setup);
                }
                else if (cmd == "go")
                {
                    if (info.SearchActive)
                        return;

                    Utilities.ParseGoCommand(param, ref info, setup);
                    GlobalSearchPool.StartSearch(info.Position, ref info, setup);
                }
                else if (cmd == "stop")
                {
                    GlobalSearchPool.StopAllThreads();
                }
                else if (cmd == "leave")
                {
                    Active = false;
                    return;
                }
                else if (cmd == "d")
                {
                    Log(pos.ToString());
                }
                else if (cmd == "setoption")
                {
                    try
                    {
                        //     "name" == param[0]
                        string optName = param[1];
                        //    "value" == param[2]
                        string optValue = string.Join(' ', param.Skip(3)).Trim();

                        HandleSetOption(optName, optValue);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"info string failed parsing setoption command, got {param} -> {e}");
                    }
                }
                else if (cmd == "ping")
                {
                    Console.WriteLine("pong");
                }
                else if (cmd == "tune")
                {

                }
                else if (cmd == "eval")
                {

                }

                //  Important to check this first!
                //  In USI, "isready" does both the resetting and responding with "readyok"
                if (cmd == ActiveFormatter.SetupNewGameCommand())
                {
                    HandleNewGame(GlobalSearchPool);
                }

                if (cmd == ActiveFormatter.RespondReadyCommand())
                {
                    Console.WriteLine("readyok");
                }
            }
        }


        private static string[] ReceiveString(out string cmd)
        {
            string? input = Console.ReadLine();
            if (input == null || input.Length == 0)
            {
                cmd = ":(";
                return Array.Empty<string>();
            }

            string[] splits = input.Split(" ");
            cmd = splits[0].ToLower();
            string[] param = splits.Skip(1).ToArray();

            return param;
        }


        private static void OnDepthDone(ref SearchInformation info)
        {
            PrintSearchInfo(ref info);
        }


        private static void OnSearchDone(ref SearchInformation info)
        {
            info.SearchActive = false;

            if (info.SearchFinishedCalled)
            {
                return;
            }

            info.SearchFinishedCalled = true;
            var bestThread = info.Position.Owner.AssocPool.GetBestThread();
            if (bestThread.RootMoves.Count == 0)
            {
                Console.WriteLine("bestmove 0000");
                return;
            }

            Move bestThreadMove = bestThread.RootMoves[0].Move;
            if (bestThreadMove.IsNull())
            {
                MoveList legal = new();
                info.Position.GenerateLegal(ref legal);
                bestThreadMove = legal[0].Move;
            }

            Console.WriteLine($"bestmove {bestThreadMove}");
        }

        private static void HandleNewGame(SearchThreadPool pool)
        {
            pool.MainThread.WaitForThreadFinished();
            pool.TTable.Clear();
            pool.Clear();
        }

        private static void HandleSetOption(string optName, string optValue)
        {
            optName = optName.Replace(" ", string.Empty);

            try
            {
                foreach (var key in Options.Keys)
                {
                    USIOption opt = Options[key];
                    if (!opt.DisplayName.EqualsIgnoreCase(optName))
                    {
                        continue;
                    }

                    object? prevValue = opt.FieldHandle.GetValue(null);
                    if (opt.FieldHandle.FieldType == typeof(bool) && bool.TryParse(optValue, out bool newBool))
                    {
                        opt.FieldHandle.SetValue(null, newBool);
                    }
                    else if (opt.FieldHandle.FieldType == typeof(int) && int.TryParse(optValue, out int newValue))
                    {
                        if (newValue >= opt.MinValue && newValue <= opt.MaxValue)
                        {
                            opt.FieldHandle.SetValue(null, newValue);

                            if (opt.Name == nameof(Threads))
                            {
                                GlobalSearchPool.Resize(SearchOptions.Threads);
                            }

                            if (opt.Name == nameof(Hash))
                            {
                                GlobalSearchPool.TTable.Initialize(SearchOptions.Hash);
                            }
                        }
                    }

                    break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR]: Failed handling setoption command for '{optName}' -> {optValue}! {e}");
            }
        }


        private static void ProcessUSIOptions()
        {
            Options = new Dictionary<string, USIOption>();

            //  Get all "public static" fields, and specifically exclude constant fields (which have field.IsLiteral == true)
            List<FieldInfo> fields = typeof(SearchOptions).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => !x.IsLiteral).ToList();

            foreach (FieldInfo field in fields)
            {
                string fieldName = field.Name;

                //  Most options are numbers and are called "spin"
                //  If they are true/false, they are called "check"
                string fieldType = field.FieldType == typeof(bool) ? "check" : "spin";
                string defaultValue = field.GetValue(null).ToString().ToLower();

                USIOption opt = new(fieldName, fieldType, defaultValue, field);

                Options.Add(fieldName, opt);
            }

            Options[nameof(Threads)].SetMinMax(1, 512);
            Options[nameof(MultiPV)].SetMinMax(1, 256);
            Options[nameof(Hash)].SetMinMax(1, 1048576);

            Options[nameof(MoveOverhead)].SetMinMax(0, 2000);

            Options[nameof(AspWindow)].SetMinMax(0, 30);

            Options[nameof(RFPDepth)].SetMinMax(1, 10);
            Options[nameof(RFPMult)].SetMinMax(50, 140);

            Options[nameof(RazoringMaxDepth)].SetMinMax(1, 10);
            Options[nameof(RazoringMult)].AutoMinMax();

            Options[nameof(NMPDepth)].SetMinMax(1, 10);
            Options[nameof(NMPBaseRed)].AutoMinMax();
            Options[nameof(NMPDepthDiv)].AutoMinMax();

            foreach (var optName in Options.Keys)
            {
                var opt = Options[optName];
                if (opt.FieldHandle.FieldType != typeof(int))
                {
                    continue;
                }

                //  Ensure values are within [Min, Max] and Max > Min
                int currValue = int.Parse(opt.DefaultValue);
                if (currValue < opt.MinValue || currValue > opt.MaxValue || opt.MaxValue < opt.MinValue)
                {
                    Log($"Option '{optName}' has an invalid range! -> [{opt.MinValue} <= {opt.DefaultValue} <= {opt.MaxValue}]!");
                }
            }
        }

        private static void PrintUSIOptions()
        {
            List<string> blacklist =
            [

            ];

            foreach (string k in Options.Keys.Where(x => !blacklist.Contains(x)))
            {
                Console.WriteLine(Options[k].ToString());
            }
        }
    }
}
