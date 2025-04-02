using Peeper.Logic.Search.History;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Peeper.Logic.Search
{
    public unsafe struct SearchStack
    {
        public Move* PV;
        public Butterfly* ContinuationHistory;
        public Move CurrentMove;
        public short Ply;
        public short StaticEval;
        public bool InCheck;
        public bool TTHit;
        public bool TTPV;

        public void Clear()
        {
            PV = null;
            ContinuationHistory = null;

            CurrentMove = Move.Null;

            Ply = 0;
            StaticEval = ScoreNone;

            InCheck = TTHit = TTPV = false;
        }
    }
}
