using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Search.History
{
    public unsafe struct HistoryTable
    {
        public const int NormalClamp = 16384;

        public readonly ContinuationHistory** Continuations;

        public HistoryTable()
        {

            Continuations = (ContinuationHistory**)AlignedAllocZeroed((nuint)(sizeof(ContinuationHistory*) * 2));
            ContinuationHistory* cont0 = (ContinuationHistory*)AlignedAllocZeroed((nuint)(sizeof(ContinuationHistory*) * 2));
            ContinuationHistory* cont1 = (ContinuationHistory*)AlignedAllocZeroed((nuint)(sizeof(ContinuationHistory*) * 2));

            cont0[0] = new ContinuationHistory();
            cont0[1] = new ContinuationHistory();

            cont1[0] = new ContinuationHistory();
            cont1[1] = new ContinuationHistory();

            Continuations[0] = cont0;
            Continuations[1] = cont1;
        }

        public void Clear()
        {

        }

        public void Dispose()
        {

        }
    }
}
