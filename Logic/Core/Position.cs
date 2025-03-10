
#define BULK

using Peeper.Logic.Data;
using System;
using System.Collections.Generic;
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
        public BoardState* NextState => (State + 1);
        private BoardState* _stateBlock;

        public bool InCheck => State->Checkers != 0;

        public int ToMove;
        public int MoveNumber;

        [MethodImpl(Inline)]
        public bool IsCapture(Move m) => (bb.GetPieceAtIndex(m.To) != None);

        public Position(string? fen = null)
        {
            bb = new Bitboard();

            _stateBlock = AlignedAllocZeroed<BoardState>(3072);
            State = _stateBlock;

            LoadFromSFen(fen ?? InitialFEN);
        }

        ~Position()
        {
            NativeMemory.AlignedFree(_stateBlock);
        }

        public bool TryMakeMove(string moveStr)
        {
            MoveList list = new();
            int size = GenerateLegal(ref list);
            for (int i = 0; i < size; i++)
            {
                Move m = list[i].Move;
                if (m.ToString().ToLower().Equals(moveStr.ToLower()))
                {
                    MakeMove(m);
                    return true;
                }

                if (i == size - 1)
                {
                    Log($"No move '{moveStr}' found, try one of the following: ");
                    Log($"{Stringify(list.ToSpan())}");
                }
            }
            return false;
        }

        public void MakeMove(Move move)
        {
            Unsafe.CopyBlock(NextState, State, (uint)sizeof(BoardState));
            State++;
            MoveNumber++;

            var (moveFrom, moveTo) = move.Unpack();

            var ourPiece = bb.GetPieceAtIndex(moveFrom);
            var ourColor = ToMove;

            var theirPiece = bb.GetPieceAtIndex(moveTo);
            var theirColor = Not(ourColor);

            Assert(ourPiece != None, $"Move {move} in '{GetSFen()}' doesn't have a piece on the From square!");
            Assert(theirPiece != King, $"Move {move} in '{GetSFen()}' captures a king!");
            Assert(theirPiece == None || ourColor != bb.GetColorAtIndex(moveTo), 
                $"Move {move} in '{GetSFen()}' captures our own {PieceToString(theirPiece)} on {IndexToString(moveTo)}");


            if (ourPiece == King)
            {
                State->KingSquares[ourColor] = moveTo;
            }

            State->CapturedPiece = theirPiece;

            if (!move.IsDrop)
            {
                bb.RemovePiece(ourColor, ourPiece, moveFrom);
            }
            else
            {
                State->Hands[ourColor].TakeFromHand(move.DroppedPiece);
            }

            if (theirPiece != None)
            {
                bb.RemovePiece(theirColor, theirPiece, moveTo);
                State->Hands[ourColor].AddToHand(IsPromoted(theirPiece) ? Demote(theirPiece) : theirPiece);
            }

            int typeToAdd = move.IsPromotion ? Piece.Promote(ourPiece)
                          : move.IsDrop      ? move.DroppedPiece
                          :                    ourPiece;

            bb.AddPiece(ourColor, typeToAdd, moveTo);

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
        public bool IsLegal(in Move move) => IsLegal(move, State->KingSquares[ToMove], State->KingSquares[Not(ToMove)], State->BlockingPieces[ToMove]);

        [MethodImpl(Inline)]
        public bool IsLegal(Move move, int ourKing, int theirKing, Bitmask pinnedPieces)
        {
            var (moveFrom, moveTo) = move.Unpack();
            int pt = bb.GetPieceAtIndex(moveFrom);

            if (pt == None)
            {
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

            if (InCheck)
            {
                //  We have 3 Options: block the check, take the piece giving check, or move our king out of it.
                if (pt == Piece.King)
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

            if (pt == Piece.King)
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

            UpdateState();
        }

        [MethodImpl(Inline)]
        public void UpdateState()
        {
            State->BlockingPieces[Black] = bb.BlockingPieces(Black, &State->Pinners[White]);
            State->BlockingPieces[White] = bb.BlockingPieces(White, &State->Pinners[Black]);

            State->Checkers = bb.AttackersTo(State->KingSquares[ToMove], bb.Occupancy) & bb.Colors[Not(ToMove)];
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

            sb.AppendLine($"\r\nBlack hand: {State->Hands[Black].ToString(Black)}");
            sb.AppendLine($"White hand: {State->Hands[White].ToString(White)}");
            sb.AppendLine($"\r\n{ColorToString(ToMove)} to move");

            return sb.ToString();
        }
    }
}
