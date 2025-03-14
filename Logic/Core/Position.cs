
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

#define BULK

using Peeper.Logic.Data;
using Peeper.Logic.Evaluation;
using Peeper.Logic.Threads;
using Peeper.Logic.Transposition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Peeper.Logic.Core
{
    public unsafe partial class Position
    {
        public Bitboard bb;

        public BoardState* State;
        private readonly BoardState* _stateBlock;
        private readonly Accumulator* _accumulatorBlock;

        public int ToMove;
        public int MoveNumber;

        public readonly SearchThread Owner;
        private readonly bool UpdateNN;

        public Position(string? fen = null, bool createAccumulators = true, SearchThread? owner = null)
        {
            this.UpdateNN = createAccumulators;
            this.Owner = owner;

            bb = new Bitboard();

            State = _stateBlock = AlignedAllocZeroed<BoardState>(BoardState.StateStackSize);

            if (UpdateNN)
            {
                _accumulatorBlock = AlignedAllocZeroed<Accumulator>(BoardState.StateStackSize);
                for (int i = 0; i < BoardState.StateStackSize; i++)
                {
                    (StartingState + i)->Accumulator = &_accumulatorBlock[i];
                    *(StartingState + i)->Accumulator = new Accumulator();
                }
            }

            if (UpdateNN && Owner == null)
            {
                Debug.WriteLine($"info string Position('{fen}', {createAccumulators}, ...) has NNUE enabled and was given a nullptr for owner! " +
                                $"Assigning this Position instance to the SearchPool's MainThread, UB and other weirdness may occur...");
                Owner = GlobalSearchPool.MainThread;
            }

            LoadFromSFen(fen ?? InitialFEN);
        }


        ~Position()
        {
            if (UpdateNN)
            {
                //  Free each accumulator, then the block
                for (int i = 0; i < BoardState.StateStackSize; i++)
                {
                    var acc = *(StartingState + i)->Accumulator;
                    acc.Dispose();
                }

                NativeMemory.AlignedFree(_accumulatorBlock);
            }

            NativeMemory.AlignedFree(_stateBlock);
        }


        public BoardState* StartingState => _stateBlock;
        public BoardState* NextState => (State + 1);
        public bool InDoubleCheck => Popcount(State->Checkers) == 2;
        public bool Checked => State->Checkers != 0;
        public ulong Hash => State->Hash;


        [MethodImpl(Inline)]
        public bool IsCapture(Move m) => (bb.GetPieceAtIndex(m.To) != None);

        [MethodImpl(Inline)]
        public int MovedPiece(Move m) => (m.IsDrop ? m.DroppedPiece : bb.GetPieceAtIndex(m.From));

        public bool TryMakeMove(string moveStr)
        {
            if (TryFindMove(moveStr, out Move move))
            {
                MakeMove(move);
                return true;
            }

            return false;
        }

        public bool TryFindMove(string moveStr, out Move move)
        {
            MoveList list = new();
            int size = GenerateLegal(ref list);
            for (int i = 0; i < size; i++)
            {
                Move m = list[i].Move;
                if (m.ToString().ToLower().Equals(moveStr.ToLower()))
                {
                    move = m;
                    return true;
                }

                if (i == size - 1)
                {
                    Log($"No move '{moveStr}' found, try one of the following:");
                    Log($"{Stringify(list.ToSpan())}");
                    Log($"{StringifyFlipFormat(list.ToSpan())}");
                }
            }

            move = Move.Null;
            return false;
        }


        public void MakeMove(Move move)
        {
            Unsafe.CopyBlock(NextState, State, BoardState.StateCopySize);

            if (UpdateNN)
            {
                NNUE.MakeMove(this, move);
            }

            State++;
            MoveNumber++;

            var (moveFrom, moveTo) = move.Unpack();

            var ourPiece = move.IsDrop ? move.DroppedPiece : bb.GetPieceAtIndex(moveFrom);
            var ourColor = ToMove;

            var theirPiece = bb.GetPieceAtIndex(moveTo);
            var theirColor = Not(ourColor);

            Assert(ourPiece != None, $"Move {move} in '{GetSFen()}' doesn't have a piece on the From square!");
            Assert(theirPiece != King, $"Move {move} in '{GetSFen()}' captures a king!");
            Assert(theirPiece == None || ourColor != bb.GetColorAtIndex(moveTo), 
                $"Move {move} in '{GetSFen()}' captures our own {PieceToString(theirPiece)} on {SquareToString(moveTo)}");


            if (ourPiece == King)
            {
                State->KingSquares[ourColor] = moveTo;
            }

            State->CapturedPiece = theirPiece;

            if (move.IsDrop)
            {
                RemoveFromHand(ourColor, ourPiece);
            }
            else
            {
                RemovePiece(ourColor, ourPiece, moveFrom);
            }

            if (theirPiece != None)
            {
                RemovePiece(theirColor, theirPiece, moveTo);
                AddToHand(ourColor, theirPiece);
            }

            int typeToAdd = move.IsPromotion ? Piece.Promote(ourPiece) : ourPiece;
            AddPiece(ourColor, typeToAdd, moveTo);

            State->Hash.ZobristChangeToMove();
            ToMove = Not(ToMove);

            State->Checkers = bb.AttackersTo(State->KingSquares[theirColor], bb.Occupancy) & bb.Colors[ourColor];

            UpdateState();
            bb.VerifyOK();
        }

        public void UnmakeMove(Move move)
        {
            var (moveFrom, moveTo) = move.Unpack();

            MoveNumber--;

            //  Assume that "we" just made the last move, and "they" are undoing it.
            int ourPiece = bb.GetPieceAtIndex(moveTo);
            int ourColor = Not(ToMove);
            int theirColor = ToMove;

            bb.RemovePiece(ourColor, ourPiece, moveTo);

            if (State->CapturedPiece != Piece.None)
            {
                bb.AddPiece(theirColor, State->CapturedPiece, moveTo);
            }

            if (move.IsPromotion)
            {
                bb.AddPiece(ourColor, Piece.Demote(ourPiece), moveFrom);
            }
            else if (!move.IsDrop)
            {
                bb.AddPiece(ourColor, ourPiece, moveFrom);
            }

            State--;
            ToMove = Not(ToMove);
            bb.VerifyOK();
        }


        [MethodImpl(Inline)]
        private void UpdateHash(int color, int type, int sq)
        {
            State->Hash.ZobristToggleSquare(color, type, sq);
        }

        [MethodImpl(Inline)]
        private void RemovePiece(int color, int type, int sq)
        {
            bb.RemovePiece(color, type, sq);
            UpdateHash(color, type, sq);
        }

        [MethodImpl(Inline)]
        private void AddPiece(int color, int type, int sq)
        {
            bb.AddPiece(color, type, sq);
            UpdateHash(color, type, sq);
        }

        [MethodImpl(Inline)]
        private void RemoveFromHand(int color, int type)
        {
            int newHeld = State->Hands[color].TakeFromHand(type);
            State->Hash.ZobristUpdateHand(color, type, newHeld, newHeld + 1);
        }

        [MethodImpl(Inline)]
        private void AddToHand(int color, int type)
        {
            int newHeld = State->Hands[color].AddToHand(DemoteMaybe(type));
            State->Hash.ZobristUpdateHand(color, DemoteMaybe(type), newHeld - 1, newHeld);
        }


        [MethodImpl(Inline)]
        public bool IsLegal(in Move move) => IsLegal(move, State->KingSquares[ToMove], State->KingSquares[Not(ToMove)], State->BlockingPieces[ToMove]);

        [MethodImpl(Inline)]
        public bool IsLegal(Move move, int ourKing, int theirKing, Bitmask pinnedPieces)
        {
            var (moveFrom, moveTo) = move.Unpack();
            int type = bb.GetPieceAtIndex(moveFrom);

            if (type == None)
            {
                return false;
            }

            if (InDoubleCheck && type != King)
            {
                //  Must move king out of double check
                return false;
            }

            int ourColor = ToMove;
            int theirColor = Not(ourColor);

            if (move.IsDrop && move.DroppedPiece == Pawn)
            {
                var fileMask = GetFileBB(GetIndexFile(moveTo));
                if ((fileMask & bb.Pieces[Pawn] & bb.Colors[ourColor]) != 0)
                {
                    //  nifu
                    return false;
                }

                if (PawnMoveMask(ourColor, moveTo).HasBit(theirKing))
                {
                    var mask = bb.AttackersTo(moveTo, bb.Occupancy);
                    bool isDefended = (mask & bb.Colors[ourColor]) != 0;
                    var nonPinnedAttackers = mask & bb.Colors[theirColor] & ~State->BlockingPieces[theirColor];
                    if (isDefended && nonPinnedAttackers == 0)
                    {
                        //  If the pawn is defended, and there aren't any attackers able to capture it,
                        //  this will be uchifuzume unless their king has at least 1 safe square to move into
                        var kingRing = KingMoveMask(theirKing) & ~bb.Colors[theirColor] & ~KingMoveMask(ourKing);
                        bool canEscape = false;
                        while (kingRing != 0)
                        {
                            int escapeSquare = PopLSB(&kingRing);
                            if ((bb.AttackersTo(escapeSquare, bb.Occupancy | SquareBB(moveTo)) & bb.Colors[ourColor]) == 0)
                            {
                                canEscape = true;
                                break;
                            }
                        }

                        if (!canEscape)
                            return false;
                    }
                }
            }

            if (Checked)
            {
                //  We have 3 Options: block the check, take the piece giving check, or move our king out of it.
                if (type == Piece.King)
                {
                    //  We need to move to a square that they don't attack.
                    //  We also need to consider (NeighborsMask[moveTo] & SquareBB[theirKing]), because bb.AttackersTo does NOT include king attacks
                    //  and we can't move to a square that their king attacks.
                    return ((bb.AttackersTo(moveTo, bb.Occupancy ^ SquareBB(moveFrom)) & bb.Colors[theirColor]) | (KingMoveMask(moveTo) & SquareBB(theirKing))) == 0;
                }

                int checker = LSB(State->Checkers);
                if ((Line(ourKing, checker) & SquareBB(moveTo)) != 0)
                {
                    //  This move is another piece which has moved into the LineBB between our king and the checking piece.
                    //  This will be legal as long as it isn't pinned.

                    return pinnedPieces == 0 || (pinnedPieces & SquareBB(moveFrom)) == 0;
                }

                //  This isn't a king move and doesn't get us out of check, so it's illegal.
                return false;
            }

            if (type == Piece.King)
            {
                //  We can move anywhere as long as it isn't attacked by them.
                return ((bb.AttackersTo(moveTo, bb.Occupancy ^ SquareBB(ourKing)) & bb.Colors[theirColor]) | (KingMoveMask(moveTo) & SquareBB(theirKing))) == 0;
            }
        
            return (!State->BlockingPieces[ourColor].HasBit(moveFrom) || Ray(moveFrom, moveTo).HasBit(ourKing));
        }


        [MethodImpl(Inline)]
        public void SetState()
        {
            State->KingSquares[Black] = bb.KingIndex(Black);
            State->KingSquares[White] = bb.KingIndex(White);

            State->Hash = Zobrist.GetHash(this);

            UpdateState();
        }

        [MethodImpl(Inline)]
        public void UpdateState()
        {
            State->BlockingPieces[Black] = bb.BlockingPieces(Black, &State->Pinners[White]);
            State->BlockingPieces[White] = bb.BlockingPieces(White, &State->Pinners[Black]);

            State->Checkers = bb.AttackersTo(State->KingSquares[ToMove], bb.Occupancy) & bb.Colors[Not(ToMove)];
        }


        public bool IsDraw()
        {
            return false;
        }



        public ulong Perft(int depth)
        {
#if !BULK
            if (depth == 0)
            {
                return 1;
            }
#endif

            MoveList list = new();
            int size = GenerateLegal(ref list);

#if BULK
            if (depth == 1)
            {
                return (ulong)size;
            }
#endif

            ulong n = 0;
            for (int i = 0; i < size; i++)
            {
                Move m = list[i].Move;
                MakeMove(m);
                n += Perft(depth - 1);
                UnmakeMove(m);
            }
            return n;
        }


        private static readonly Stopwatch PerftTimer = new Stopwatch();
        public ulong PerftParallel(int depth, bool isRoot = false)
        {
            const int MinRecursiveDepth = 6;

            if (isRoot)
            {
                PerftTimer.Restart();
            }

            MoveList mlist = new();
            int size = GenerateLegal(ref mlist);
            var list = mlist.ToSpicyPointer();

            ulong n = 0;

            string rootFEN = GetSFen();
            Parallel.For(0, size, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, size) }, i =>
            {
                Position threadPosition = new Position(rootFEN);

                threadPosition.MakeMove(list[i].Move);
                ulong result = (depth > MinRecursiveDepth) ? threadPosition.PerftParallel(depth - 1) : threadPosition.Perft(depth - 1);
                if (isRoot)
                {
                    Log($"{list[i].Move}\t{result}");
                }
                n += result;
            });

            if (isRoot)
            {
                PerftTimer.Stop();
                Log($"\r\nNodes searched: {n} in {PerftTimer.Elapsed.TotalSeconds} s ({(int)(n / PerftTimer.Elapsed.TotalSeconds):N0} nps)\r\n");
                PerftTimer.Reset();
            }

            return n;
        }


        public bool LoadFromSFen(string sfen)
        {
            bb.Clear();
            State->Hands[Black].Clear();
            State->Hands[White].Clear();

            MoveNumber = 1;
            State = StartingState;
            NativeMemory.Clear(State, BoardState.StateCopySize);

            var fields = sfen.Split(' ');

            var ranks = fields[0].Split('/');
            for (int splitN = 0; splitN < RankNB; splitN++)
            {
                var rankStr = ranks[splitN];
                int rank = splitN;
                int file = -1;

                bool isPromoted = false;
                foreach (char c in rankStr)
                {
                    file++;

                    if (char.IsDigit(c))
                    {
                        file += (int.Parse(c.ToString()) - 1);
                        continue;
                    }

                    if (c == '+')
                    {
                        isPromoted = true;
                        file--;
                        continue;
                    }

                    int sq = CoordToIndex(file, rank);
                    int color = SFenToColor(c);
                    int type = SFenToPiece(c);

                    type += isPromoted ? PromotionNB : 0;

                    bb.AddPiece(color, type, sq);
                    isPromoted = false;
                }
            }

            ToMove = fields[1] == "b" ? Black : White;
            
            State->Hands[Black].SetFromSFen(fields[2], Black);
            State->Hands[White].SetFromSFen(fields[2], White);

            MoveNumber = int.Parse(fields[3]);

            SetState();

            if (UpdateNN)
            {
                NNUE.RefreshAccumulator(this);
            }

            return true;
        }

        public string GetSFen()
        {
            StringBuilder sb = new StringBuilder();

            for (int y = RankI; y <= RankA; y++)
            {
                int gap = 0;
                for (int x = File1; x <= File9; x++)
                {
                    int sq = CoordToIndex(x, y);
                    int type = bb.GetPieceAtIndex(sq);

                    if (type != None)
                    {
                        if (gap != 0)
                        {
                            sb.Append(gap);
                            gap = 0;
                        }

                        int color = bb.GetColorAtIndex(sq);
                        sb.Append(PieceToSFen(color, type));

                        continue;
                    }
                    else
                    {
                        gap++;
                    }

                    if (x == File9)
                    {
                        sb.Append(gap);
                    }
                }

                if (y != RankA)
                    sb.Append('/');
            }

            sb.Append(ToMove == Black ? " b " : " w ");
            
            if (State->Hands[Black].IsEmpty && State->Hands[White].IsEmpty)
            {
                sb.Append("-");
            }
            else
            {
                sb.Append(State->Hands[Black].ToString(Black));
                sb.Append(State->Hands[White].ToString(White));
            }

            sb.Append($" {MoveNumber}");

            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(bb.ToString());

            sb.AppendLine($"\r\nFEN: {ActiveFormatter.FormatSFen(this)}");
            sb.AppendLine($"\r\nBlack hand: {State->Hands[Black].ToString(Black)}");
            sb.AppendLine($"White hand: {State->Hands[White].ToString(White)}");
            sb.AppendLine($"\r\n{ColorToString(ToMove)} to move");

            return sb.ToString();
        }
    }
}
