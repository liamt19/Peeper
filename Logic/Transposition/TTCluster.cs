using System.Runtime.InteropServices;

namespace Peeper.Logic.Transposition
{
    /// <summary>
    /// Contains 3 TTEntry's, all of which map to a particular index within the Transposition Table.
    /// <br></br>
    /// A pointer to this (TTCluster*) can be casted to a TTEntry* and indexed from 0 to 2 to access
    /// the individual TTEntry's since the offsets of the entries do not change.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct TTCluster
    {
        [FieldOffset( 0)] private TTEntry _elem0;
#if NO
        [FieldOffset(10)] private TTEntry _elem1;
        [FieldOffset(20)] private TTEntry _elem2;
        [FieldOffset(30)] private fixed byte _pad0[2];
#endif


        public TTCluster()
        {
            _elem0 = new TTEntry();
#if NO
            _elem1 = new TTEntry();
            _elem2 = new TTEntry();

            _pad0[0] = (byte)':';
            _pad0[1] = (byte)')';
#endif
        }

        /// <summary>
        /// Zeroes the memory for each of the TTEntry's in this cluster.
        /// </summary>
        public void Clear()
        {
            fixed (void* ptr = &_elem0)
            {
                NativeMemory.Clear((void*)ptr, (nuint)sizeof(TTEntry) * TranspositionTable.EntriesPerCluster);
            }
        }
    }
}
