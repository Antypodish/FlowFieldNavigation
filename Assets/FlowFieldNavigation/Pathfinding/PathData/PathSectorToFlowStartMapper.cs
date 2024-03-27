using Unity.Collections;

namespace FlowFieldNavigation
{
    internal struct PathSectorToFlowStartMapper
    {
        internal NativeHashMap<ulong, int> Map;

        internal PathSectorToFlowStartMapper(int initialCapacity, Allocator allocator)
        {
            Map = new NativeHashMap<ulong, int>(initialCapacity, allocator);
        }
        internal void Dispose()
        {
            Map.Dispose();
        }
        internal void Add(int pathIndex, int sectorIndex, int flowIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            Map.Add(key, flowIndex);
        }
        internal bool TryGet(int pathIndex, int sectorIndex, out int sectorFlowStartIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            bool succesfull = Map.TryGetValue(key, out sectorFlowStartIndex);
            return succesfull;
        }
        internal bool Contains(int pathIndex, int sectorIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return Map.ContainsKey(key);
        }
    }
}