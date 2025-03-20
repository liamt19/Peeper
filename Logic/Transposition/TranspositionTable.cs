using Peeper.Logic.Search;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Peeper.Logic.Transposition
{
    public unsafe class TranspositionTable
    {
        public const int TT_BOUND_MASK = 0x3;
        public const int TT_PV_MASK = 0x4;
        public const int TT_AGE_MASK = 0xF8;
        public const int TT_AGE_INC = 0x8;
        public const int TT_AGE_CYCLE = 255 + TT_AGE_INC;

        private const int MinTTClusters = 1000;

        public const int EntriesPerCluster = 3;

        private TTCluster* Clusters;
        public ulong ClusterCount { get; private set; }

        public byte Age = 0;

        public TranspositionTable(int mb)
        {
            Initialize(mb);
        }

        /// <summary>
        /// Allocates <paramref name="mb"/> megabytes of memory for the Transposition Table, and zeroes out each entry.
        /// <para></para>
        /// 1 mb fits 32,768 clusters, which is 98,304 TTEntry's.
        /// </summary>
        public unsafe void Initialize(int mb)
        {
            if (Clusters != null)
                NativeMemory.AlignedFree(Clusters);

            ClusterCount = (ulong)mb * 0x100000UL / (ulong)sizeof(TTCluster);
            nuint allocSize = ((nuint)sizeof(TTCluster) * (nuint)ClusterCount);

            Clusters = AlignedAllocZeroed<TTCluster>((nuint)ClusterCount, (1024 * 1024));
        }

        /// <summary>
        /// Reinitializes each <see cref="TTCluster"/> within the table.
        /// </summary>
        public void Clear()
        {
            int numThreads = SearchOptions.Threads;
            ulong clustersPerThread = (ClusterCount / (ulong)numThreads);

            Parallel.For(0, numThreads, new ParallelOptions { MaxDegreeOfParallelism = numThreads }, (int i) =>
            {
                ulong start = clustersPerThread * (ulong)i;

                //  Only clear however many remaining clusters there are if this is the last thread
                ulong length = (i == numThreads - 1) ? ClusterCount - start : clustersPerThread;

                NativeMemory.Clear(&Clusters[start], ((nuint)sizeof(TTCluster) * (nuint)length));
            });

            Age = 0;
        }

        /// <summary>
        /// Returns a pointer to the <see cref="TTCluster"/> that the <paramref name="hash"/> maps to.
        /// </summary>
        [MethodImpl(Inline)]
        public TTCluster* GetCluster(ulong hash)
        {
            return Clusters + ((ulong)(((UInt128)hash * (UInt128)ClusterCount) >> 64));
        }



        public bool Probe(ulong hash, out TTEntry* tte)
        {
            TTCluster* cluster = GetCluster(hash);
            tte = (TTEntry*)cluster;

            var key = (ushort)hash;

            for (int i = 0; i < EntriesPerCluster; i++)
            {
                if (tte[i].Key == key || tte[i].IsEmpty)
                {
                    tte = &tte[i];

                    return !tte[0].IsEmpty;
                }
            }

            TTEntry* replace = tte;
            for (int i = 1; i < EntriesPerCluster; i++)
            {
                if ((replace->RawDepth - replace->RelAge(Age)) >
                    (  tte[i].RawDepth -   tte[i].RelAge(Age)))
                {
                    replace = &tte[i];
                }
            }

            tte = replace;
            return false;
        }

        public void TTUpdate()
        {
            Age += TT_AGE_INC;
        }

        public int GetHashFull()
        {
            int entries = 0;

            for (int i = 0; i < MinTTClusters; i++)
            {
                TTEntry* cluster = (TTEntry*)&Clusters[i];

                for (int j = 0; j < EntriesPerCluster; j++)
                {
                    if (!cluster[j].IsEmpty && (cluster[j].Age) == (Age & TT_AGE_MASK))
                    {
                        entries++;
                    }
                }
            }
            return entries / EntriesPerCluster;
        }

        public void PrintClusterStatus()
        {
            int recentEntries = 0;
            int Beta = 0;
            int Alpha = 0;
            int Exact = 0;
            int Invalid = 0;

            int NullMoves = 0;

            int[] slots = new int[EntriesPerCluster];

            for (ulong i = 0; i < ClusterCount; i++)
            {
                TTEntry* cluster = (TTEntry*)&Clusters[i];
                for (int j = 0; j < EntriesPerCluster; j++)
                {
                    var tt = cluster[j];
                    if (tt.NodeType == TTNodeType.Beta)
                    {
                        Beta++;
                    }
                    else if (tt.NodeType == TTNodeType.Alpha)
                    {
                        Alpha++;
                    }
                    else if (tt.NodeType == TTNodeType.Exact)
                    {
                        Exact++;
                    }
                    else
                    {
                        Invalid++;
                    }

                    if (tt.NodeType != TTNodeType.Invalid)
                    {
                        slots[j]++;
                    }

                    if (tt.BestMove.IsNull() && tt.NodeType != TTNodeType.Invalid)
                    {
                        NullMoves++;
                    }

                    if (tt.Age == Age)
                    {
                        recentEntries++;
                    }
                }
            }

            int entries = Beta + Alpha + Exact;

            //  "Full" is the total number of entries of any age in the TT.
            Log($"Full:\t {entries} / {ClusterCount * EntriesPerCluster} = {(double)entries / (ClusterCount * EntriesPerCluster) * 100}%");

            //  "Recent" is the number of entries that have the same age as the TT's Age.
            Log($"Recent:\t {recentEntries} /{ClusterCount * EntriesPerCluster} = {(double)recentEntries / (ClusterCount * EntriesPerCluster) * 100}%");

            //  "Slots[0,1,2]" are the number of entries that exist in each TTCluster slot
            Log($"Slots:\t {slots[0]} / {slots[1]} / {slots[2]}");
            Log($"Alpha:\t {Alpha}");
            Log($"Beta:\t {Beta}");
            Log($"Exact:\t {Exact}");
            Log($"Invalid: {Alpha}");
            Log($"Null:\t {NullMoves}");
        }
    }
}
