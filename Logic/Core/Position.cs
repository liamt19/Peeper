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

        public bool Checked => State->Checkers != 0;

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

            if (move.IsDrop)
            {
                bb.AddPiece(ourColor, move.DroppedPiece, moveTo);
            }
            else if (move.IsPromotion)
            {
                bb.AddPiece(ourColor, Piece.Promote(ourPiece), moveTo);
            }
            else
            {
                if (theirPiece != None) {
                    bb.RemovePiece(theirColor, theirPiece, moveTo);
                }

                bb.AddPiece(ourColor, ourPiece, moveTo);
            }

            ToMove = Not(ToMove);

            State->Checkers = bb.AttackersTo(State->KingSquares[theirColor], bb.Occupancy) & bb.Colors[ourColor];

            UpdateState();
        }

        public void UnmakeMove(Move move)
        {
            var (moveFrom, moveTo) = move.Unpack();

            MoveNumber--;

            //  Assume that "we" just made the last move, and "they" are undoing it.
            int ourPiece = bb.GetPieceAtIndex(moveTo);
            int ourColor = Not(ToMove);
            int theirColor = ToMove;

            if (move.IsDrop)
            {
                bb.RemovePiece(ourColor, ourPiece, moveTo);
            }
            else if (move.IsPromotion)
            {
                //  Remove the promotion piece and replace it with a pawn
                bb.RemovePiece(ourColor, ourPiece, moveTo);
                bb.AddPiece(ourColor, Piece.Demote(ourPiece), moveFrom);
            }
            else
            {
                if (State->CapturedPiece != Piece.None)
                {
                    bb.AddPiece(theirColor, State->CapturedPiece, moveTo);
                }

                bb.AddPiece(ourColor, ourPiece, moveFrom);
            }

            State--;
            ToMove = Not(ToMove);
        }

        [MethodImpl(Inline)]
        public bool IsLegal(in Move move) => IsLegal(move, State->KingSquares[ToMove], State->KingSquares[Not(ToMove)], State->BlockingPieces[ToMove]);

        public bool IsLegal(Move move, int ourKing, int theirKing, Bitmask pinnedPieces)
        {
            return true;
        }

        [MethodImpl(Inline)]
        public void SetState()
        {
            State->KingSquares[Black] = bb.KingIndex(Black);
            State->KingSquares[White] = bb.KingIndex(White);

            State->Checkers = bb.AttackersTo(State->KingSquares[ToMove], bb.Occupancy) & bb.Colors[Not(ToMove)];
            UpdateState();
        }

        [MethodImpl(Inline)]
        public void UpdateState()
        {
            State->BlockingPieces[White] = bb.BlockingPieces(White, &State->Pinners[Black]);
            State->BlockingPieces[Black] = bb.BlockingPieces(Black, &State->Pinners[White]);
        }

        public ulong Perft(int depth)
        {
            MoveList list = new();
            GenerateLegal(ref list);

            if (depth == 1)
            {
                return (ulong)list.Size;
            }

            ulong n = 0;
            for (int i = 0; i < list.Size; i++)
            {
                Move m = list[i].Move;
                MakeMove(m);
                n += Perft(depth - 1);
                UnmakeMove(m);
            }
            return n;
        }


        public void LoadFromSFen(string fen)
        {
            bb.Clear();
            MoveNumber = 1;

            var fields = fen.Split(' ');

            var ranks = fields[0].Split('/');
            for (int splitN = 0; splitN < RankNB; splitN++)
            {
                var rankStr = ranks[splitN];
                int rank = splitN;
                int file = -1;

                foreach (char c in rankStr)
                {
                    file++;

                    bool isPromoted = false;
                    if (char.IsDigit(c))
                    {
                        file += (int.Parse(c.ToString()) - 1);
                        continue;
                    }

                    if (c == '+')
                    {
                        isPromoted = true;
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
            if (fields.Length > 3) { }

            SetState();
        }

        public string GetSFen()
        {
            StringBuilder sb = new StringBuilder();



            //return sb.ToString();
            return null;
        }

        public override string ToString()
        {
            return $"{bb}\r\n";
        }
    }
}
