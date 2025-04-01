
using Peeper.Logic.Search;
using Peeper.Logic.Threads;
using Peeper.Logic.USI;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Peeper.Logic.Util
{
    public static class Utilities
    {
        public const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

        /// <summary> These inlinings might be dubious </summary>
        public const MethodImplOptions InlineMaybe = MethodImplOptions.AggressiveInlining;

        public const string EngineBuildVersion = "0.0.1";
        public const int MoveListSize = 600;

        public const int MaxPly = 256;
        public const int MaxDepth = 255;

        public const int MaxSearchStackPly = MaxPly - 10;

        public static readonly Bitmask AllMask = new(0x1ffff, 0xffffffffffffffff);
        public static readonly Bitmask EmptyMask = new();

        public static readonly Bitmask RankI_Mask = new(0, 0x1ff);
        public static readonly Bitmask RankH_Mask = new(0, 0x3fe00);
        public static readonly Bitmask RankG_Mask = new(0, 0x7fc0000);
        public static readonly Bitmask RankF_Mask = new(0, 0xff8000000);
        public static readonly Bitmask RankE_Mask = new(0, 0x1ff000000000);
        public static readonly Bitmask RankD_Mask = new(0, 0x3fe00000000000);
        public static readonly Bitmask RankC_Mask = new(0, 0x7fc0000000000000);
        public static readonly Bitmask RankB_Mask = new(0xff, 0x8000000000000000);
        public static readonly Bitmask RankA_Mask = new(0x1ff00, 0);
        public static readonly Bitmask File9_Mask = new(0x100, 0x8040201008040201);
        public static readonly Bitmask File8_Mask = new(0x201, 0x80402010080402);
        public static readonly Bitmask File7_Mask = new(0x402, 0x100804020100804);
        public static readonly Bitmask File6_Mask = new(0x804, 0x201008040201008);
        public static readonly Bitmask File5_Mask = new(0x1008, 0x402010080402010);
        public static readonly Bitmask File4_Mask = new(0x2010, 0x804020100804020);
        public static readonly Bitmask File3_Mask = new(0x4020, 0x1008040201008040);
        public static readonly Bitmask File2_Mask = new(0x8040, 0x2010080402010080);
        public static readonly Bitmask File1_Mask = new(0x10080, 0x4020100804020100);

        public static readonly Bitmask BlackPromotionSquares = new(0x1FFFF, 0xFFC0000000000000);
        public static readonly Bitmask WhitePromotionSquares = new(0, 0x7FFFFFF);

        public static readonly Bitmask RankAB_Mask = RankA_Mask | RankB_Mask;
        public static readonly Bitmask RankHI_Mask = RankH_Mask | RankI_Mask;

        public const string InitialFEN_UCI = @"lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL[-] w - 1";
        public const string InitialFEN = @"lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL b - 1";
        public const string DropsFEN = @"4k4/9/9/9/9/9/9/9/4K4 b RBGSNLP 1";


        public static void Log(string s)
        {
            Console.WriteLine(s);
            Debug.WriteLine(s);
        }

        public static void Log(Bitmask s)
        {
            Log(s.FormattedString());
            Log($"0x{s:X}");
        }

        public static int Not(int color) => color ^ 1;

        public static Bitmask SquareBB(int sq) => (Bitmask)1 << sq;

        public static int CoordToIndex(int file, int rank) => 80 - (8 - file + 9 * rank);
        public static (int, int) IndexToCoord(int index) => (index % 9, index / 9);

        public static Bitmask GetRankBB(int rank) => RankI_Mask << 9 * GetIndexRank(rank);
        public static Bitmask GetFileBB(int file) => File9_Mask << GetIndexFile(file);

        public static Bitmask ForcedPromotionSquares(int color, int type)
        {   
            if (type == Pawn || type == Lance)
            {
                return RankA_Mask >> ((SquareNB - 9) * color);
            }
            else if (type == Knight)
            {
                return (RankA_Mask | RankB_Mask) >> ((SquareNB - 9 - 9) * color);
            }

            return 0;
        }

        public static (ulong, ulong) Unpack(this Bitmask b) => (b.Upper(), b.Lower());
        public static ulong Upper(this Bitmask b) => (ulong)(b >> 64);
        public static ulong Lower(this Bitmask b) => (ulong)b;


        [MethodImpl(Inline)]
        public static int AsInt(this bool b) => b ? 1 : 0;

        public static int CeilToMultiple(int val, int mult) => (val + mult - 1) / mult * mult;


        [MethodImpl(Inline)]
        public static Bitmask Shift(this Bitmask b, int dir)
        {
            return dir switch
            {
                Direction.North      => b << 9,
                Direction.South      => b >> 9,
                Direction.NorthNorth => b << 18,
                Direction.SouthSouth => b >> 18,
                Direction.East       => (b & ~File1_Mask) << 1,
                Direction.West       => (b & ~File9_Mask) >> 1,
                Direction.NorthEast  => (b & ~File1_Mask) << 10,
                Direction.NorthWest  => (b & ~File9_Mask) << 8,
                Direction.SouthEast  => (b & ~File1_Mask) >> 8,
                Direction.SouthWest  => (b & ~File9_Mask) >> 10,
                _ => 0
            };
        }

        [MethodImpl(Inline)]
        public static int ShiftUpDir(int color) => color == Black ? Direction.North : Direction.South;

        [MethodImpl(Inline)]
        public static int GetIndexFile(int index) => index % 9;

        [MethodImpl(Inline)]
        public static int GetIndexRank(int index) => index / 9;

        public static bool SquareOK(int sq)
        {
            return sq >= I9 && sq <= A1;
        }

        public static bool DirectionOK(int sq, int dir)
        {
            if (!SquareOK(sq + dir))
            {
                return false;
            }

            int rankDistance = Math.Abs(GetIndexRank(sq) - GetIndexRank(sq + dir));
            int fileDistance = Math.Abs(GetIndexFile(sq) - GetIndexFile(sq + dir));
            return Math.Max(rankDistance, fileDistance) <= 2;
        }

        public static ulong NextUlong(this Random random)
        {
            Span<byte> arr = new byte[8];
            random.NextBytes(arr);

            return BitConverter.ToUInt64(arr);
        }

        public static string ColorToString(int color)
        {
            return color switch
            {
                White => nameof(White),
                Black => nameof(Black),
                _ => "None"
            };
        }

        public static int StringToColor(string colorName)
        {
            return colorName.ToLower() switch
            {
                "white" => White,
                "gote" => White,
                "black" => Black,
                "sente" => Black,
                _ => ColorNB
            };
        }

        public static int SFenToColor(char c) => char.IsUpper(c) ? Black : White;

        public static int SFenToPiece(char c)
        {
            return char.ToLower(c) switch
            {
                'p' => Pawn,
                'l' => Lance,
                'n' => Knight,
                's' => Silver,
                'b' => Bishop,
                'r' => Rook,
                'g' => Gold,
                'k' => King,
                _ => None
            };
        }

        public static char PieceToSFenChar(int type)
        {
            return type switch
            {
                Pawn or PawnPromoted => 'P',
                Lance or LancePromoted => 'L',
                Knight or KnightPromoted => 'N',
                Silver or SilverPromoted => 'S',
                Bishop or BishopPromoted => 'B',
                Rook or RookPromoted => 'R',
                Gold => 'G',
                King => 'K',
                _ => ' '
            };
        }

        public static char PieceToSFenChar(int color, int type)
        {
            char c = PieceToSFenChar(type);
            return (color == Black) ? c : char.ToLower(c);
        }

        public static string PieceToSFen(int color, int type)
        {
            char c = PieceToSFenChar(color, type);
            return $"{(IsPromoted(type) ? "+" : "")}{c}";
        }

        public static string PieceToString(int type)
        {
            return type switch
            {
                Pawn => nameof(Pawn),
                Lance => nameof(Lance),
                Knight => nameof(Knight),
                Silver => nameof(Silver),
                Bishop => nameof(Bishop),
                Rook => nameof(Rook),

                PawnPromoted => nameof(PawnPromoted),
                LancePromoted => nameof(LancePromoted),
                KnightPromoted => nameof(KnightPromoted),
                SilverPromoted => nameof(SilverPromoted),
                BishopPromoted => nameof(BishopPromoted),
                RookPromoted => nameof(RookPromoted),

                Gold => nameof(Gold),
                King => nameof(King),
                _ => "None"
            };
        }

        public static string PieceToString(int color, int type) => $"{ColorToString(color)} {PieceToString(type)}";
        public static string SquareToString(int sq) => $"{GetIndexFileName(sq)}{GetIndexRankName(sq)}";

        public static int GetIndexFileName(int sq) => 9 - sq % 9;
        public static char GetIndexRankName(int sq) => (char)('i' - sq / 9);

        public static int StringToSquare(string str)
        {
            if (str.Length != 2)
                return SquareNB;

            if (str[0] < '1' || str[0] > '9' || str[1] < 'a' || str[1] > 'i')
                return SquareNB;

            int rank = 'a' + 8 - str[1];
            int file = '1' + 8 - str[0];

            return rank * 9 + file;
        }

        public static bool HasBit(this Bitmask b, int sq) => (b & SquareBB(sq)) != Bitmask.Zero;


        public static string PrintBoard(Bitboard bb)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            char[] fileNames = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i'];

            sb.AppendLine($"   {string.Join(" |  ", Enumerable.Range(1, 9).Reverse())}  ");
            sb.AppendLine($"+----+----+----+----+----+----+----+----+----+");
            for (int y = 0; y < 9; y++)
            {
                sb.Append("|");
                for (int x = 0; x < 9; x++)
                {
                    int sq = CoordToIndex(x, y);
                    int type = bb.GetPieceAtIndex(sq);

                    sb.Append(' ');

                    if (type != None)
                    {
                        int color = bb.GetColorAtIndex(sq);
                        var c = PieceToSFen(color, type);
                        sb.Append(c.Length == 1 ? $" {c}" : c);
                    }
                    else
                    {
                        sb.Append("  ");
                    }

                    sb.Append(" |");
                }
                sb.Append($" {char.ToUpper(fileNames[y])}");

                sb.AppendLine();
                sb.AppendLine("+----+----+----+----+----+----+----+----+----+  ");
            }

            return sb.ToString();
        }


        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
                action(item);
        }

        public static bool EqualsIgnoreCase(this string s, string other) => s.Equals(other, StringComparison.OrdinalIgnoreCase);
        public static bool StartsWithIgnoreCase(this string s, string other) => s.StartsWith(other, StringComparison.OrdinalIgnoreCase);
        public static bool ContainsIgnoreCase(this string s, string other) => s.Contains(other, StringComparison.OrdinalIgnoreCase);

        public static string FormattedString(this Bitmask b)
        {
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    int sq = CoordToIndex(x, y);
                    sb.Append(b.HasBit(sq) ? '1' : '\u00B7');
                }

                if (y != 8)
                    sb.AppendLine();
            }
            return sb.ToString();
        }


        public static unsafe string Stringify(ScoredMove* list, int listSize = 0) => Stringify(new Span<ScoredMove>(list, MoveListSize), listSize);

        public static string Stringify(Span<ScoredMove> list, int listSize = 0)
        {
            StringBuilder sb = new StringBuilder();
            int loopMax = (listSize > 0) ? Math.Min(list.Length, listSize) : list.Length;
            for (int i = 0; i < loopMax; i++)
            {
                if (listSize == 0 && list[i].Move.Equals(Move.Null))
                    break;
                sb.Append($"{list[i].Move}, ");
            }

            if (sb.Length > 3)
                sb.Remove(sb.Length - 2, 2);

            return sb.ToString();
        }

        public static string StringifyFlipFormat(Span<ScoredMove> list, int listSize = 0)
        {
            if (IsFormatterUSI)
                SetUCIFormatter();
            else
                SetUSIFormatter();

            StringBuilder sb = new StringBuilder();
            int loopMax = (listSize > 0) ? Math.Min(list.Length, listSize) : list.Length;
            for (int i = 0; i < loopMax; i++)
            {
                if (listSize == 0 && list[i].Move.Equals(Move.Null))
                    break;
                sb.Append($"{list[i].Move}, ");
            }

            if (sb.Length > 3)
                sb.Remove(sb.Length - 2, 2);

            if (IsFormatterUSI)
                SetUCIFormatter();
            else
                SetUSIFormatter();

            return sb.ToString();
        }


        public static void ParsePositionCommand(string[] input, Position pos, ThreadSetup setup)
        {
            //  Skip the "position fen" part, and slice until hitting the end of the input or "moves ..."
            input = input.SkipWhile(x => x is "position" || x.EndsWith("fen")).ToArray();

            string fen = string.Join(" ", input.TakeWhile(x => x != "moves"));

            if (fen is "startpos")
                fen = InitialFEN;

            fen = ActiveFormatter.ParseSFen(fen);

            setup.StartFEN = fen;
            pos.LoadFromSFen(setup.StartFEN);

            setup.SetupMoves.Clear();
            var moves = input.SkipWhile(x => x != "moves").Skip(1).ToArray();

            for (int i = 0; i < moves.Length; i++)
            {
                if (pos.TryFindMove(moves[i], out Move m))
                {
                    pos.MakeMove(m);
                }
                else
                {
                    if (USIClient.Active)
                    {
                        var sfen = pos.GetSFen();
                        var cmd = string.Join(" ", input);

                        MoveList legalList = new();
                        pos.GenerateLegal(ref legalList);
                        var legal = string.Join(", ", legalList.ToSpan().ToArray().Select(x => x.Move));

                        MoveList pseudoList = new();
                        pos.GeneratePseudoLegal(ref pseudoList);

                        StringBuilder sb = new();
                        sb.AppendLine($"Move {m} wasn't found! at {i} / {moves.Length}");
                        sb.AppendLine($"cmd: {cmd}");
                        sb.AppendLine($"CurrentFEN: {sfen}");
                        sb.AppendLine($"StartFEN: {setup.StartFEN}");
                        sb.AppendLine($"legal: [{Stringify(legalList.ToSpan())}]/[{StringifyFlipFormat(legalList.ToSpan())}]");
                        sb.AppendLine($"pseudo: [{Stringify(pseudoList.ToSpan())}]/[{StringifyFlipFormat(pseudoList.ToSpan())}]");

                        FailFast(sb.ToString());
                    }
                }
                    

                setup.SetupMoves.Add(m);
            }
        }


        public static void ParseGoCommand(string[] param, ref SearchInformation info, ThreadSetup setup)
        {
            int stm = info.Position.ToMove;
            var stmChar = stm == Black ? ActiveFormatter.GetGoBlackChar() : ActiveFormatter.GetGoWhiteChar();

            info.SearchFinishedCalled = false;

            TimeManager.Reset();

            setup.UCISearchMoves = new List<Move>();

            //  Assume that we can search infinitely, and let the parameters constrain us accordingly.
            int movetime = MaximumSearchTime;
            ulong nodeLimit = MaximumSearchNodes;
            int depthLimit = MaxDepth;
            int playerTime = 0;
            int increment = 0;

            for (int i = 0; i < param.Length - 1; i++)
            {
                if (param[i] == "movetime" && int.TryParse(param[i + 1], out int reqMovetime))
                {
                    movetime = reqMovetime;
                }
                else if (param[i] == "depth" && int.TryParse(param[i + 1], out int reqDepth))
                {
                    depthLimit = reqDepth;
                }
                else if (param[i] == "nodes" && ulong.TryParse(param[i + 1], out ulong reqNodes))
                {
                    nodeLimit = reqNodes;
                }
                else if (param[i].StartsWith(stmChar) && param[i].EndsWith("time") && int.TryParse(param[i + 1], out int reqPlayerTime))
                {
                    playerTime = reqPlayerTime;
                }
                else if (param[i].StartsWith(stmChar) && param[i].EndsWith("inc") && int.TryParse(param[i + 1], out int reqPlayerIncrement))
                {
                    increment = reqPlayerIncrement;
                }
                else if (param[i] == "searchmoves")
                {
                    i++;

                    while (i <= param.Length - 1)
                    {
                        if (info.Position.TryFindMove(param[i], out Move m))
                        {
                            setup.UCISearchMoves.Add(m);
                        }

                        i++;
                    }
                }
            }

            info.DepthLimit = depthLimit;
            info.HardNodeLimit = nodeLimit;

            bool useSoftTM = param.Any(x => x.EndsWith("time") && x.StartsWith(stmChar)) && !param.Any(x => x == "movetime");
            if (useSoftTM)
            {
                TimeManager.UpdateTimeLimits(playerTime, increment);
            }
            else
            {
                TimeManager.SetHardLimit(movetime);
            }


        }


        public static void PrintSearchInfo(ref SearchInformation info)
        {
            SearchThread thisThread = info.Position.Owner;

            var rootMoves = thisThread.RootMoves;
            int multiPV = Math.Min(MultiPV, rootMoves.Count);

            double time = Math.Max(1, Math.Round(TimeManager.GetSearchTime()));
            ulong nodes = thisThread.AssocPool.GetNodeCount();
            int nodesPerSec = (int)((double)nodes / (time / 1000));

            int lastValidScore = 0;

            for (int i = 0; i < multiPV; i++)
            {
                ref RootMove rm = ref rootMoves[i];
                bool moveSearched = rm.Score != -ScoreInfinite;

                int depth = moveSearched ? thisThread.RootDepth : Math.Max(1, thisThread.RootDepth - 1);
                int moveScore = moveSearched ? rm.Score : rm.PreviousScore;

                if (!moveSearched && i > 0)
                {
                    if (depth == 1)
                    {
                        continue;
                    }

                    if (moveScore == -ScoreInfinite)
                    {
                        moveScore = Math.Min(lastValidScore - 1, rm.AverageScore);
                    }
                }

                if (moveScore != -ScoreInfinite)
                {
                    lastValidScore = moveScore;
                }

                var score = FormatMoveScore(moveScore);
                var hashfull = thisThread.TT.GetHashFull();

                Console.Write($"info depth {depth} seldepth {rm.Depth} multipv {i + 1} time {time} score {score} nodes {nodes} nps {nodesPerSec} hashfull {hashfull} pv");

                for (int j = 0; j < MaxPly; j++)
                {
                    if (rm.PV[j] == Move.Null) break;

                    string s = $" {rm.PV[j].ToString()}";
                    Console.Write(s);
                }

                Console.WriteLine();
            }
        }

        private static string FormatMoveScore(int score)
        {
            const int NormalizeEvalFactor = 100;

            if (IsScoreMate(score))
            {
                return $"mate {ActiveFormatter.FormatMateDistance(score)}";
            }

            var ev = ((double)score * 100 / NormalizeEvalFactor);
            return $"cp {(int)ev}";
        }


        public static void StableSort(RootMoveList items, int offset = 0, int end = -1)
        {
            if (end == -1)
            {
                end = items.Count;
            }

            for (int i = offset; i < end; i++)
            {
                int best = i;

                for (int j = i + 1; j < end; j++)
                {
                    if (items[j].CompareTo(items[best]) > 0)
                    {
                        best = j;
                    }
                }

                if (best != i)
                {
                    (items[i], items[best]) = (items[best], items[i]);
                }
            }
        }

    }
}
