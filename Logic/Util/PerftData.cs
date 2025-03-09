using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Util
{
    public static class PerftData
    {
        public static Dictionary<string, ulong[]> PerftPositions = new()
        {
            {"l6nl/5+P1gk/2np1S3/p1p4Pp/3P2Sp1/1PPb2P1P/P5GS1/R8/LN4bKL w GR5pnsg 1", new ulong[] { 1, 207, 28684, 4809015, 516925165 } },
            {"lnsgkgsnl/1r5b1/ppppppppp/9/9/9/PPPPPPPPP/1B5R1/LNSGKGSNL b - 1", new ulong[] { 1, 30, 900, 25470, 719731, 19861490, 547581517, 15086269607 } },
            {"9/9/9/3k5/9/5K3/9/9/9 b RB2G2S2N2L9Prb2g2s2n2l9p 1", new ulong[] { 1, 524, 248257, 112911856, 50852853772 } },
            {"8l/1l+R2P3/p2pBG1pp/kps1p4/Nn1P2G2/P1P1P2PP/1PS6/1KSG3+r1/LN2+p3L w Sbgn3p 124", new ulong[] { 1, 178, 18041, 2552846, 207741677, 24120401335 } },
        };
    }
}
