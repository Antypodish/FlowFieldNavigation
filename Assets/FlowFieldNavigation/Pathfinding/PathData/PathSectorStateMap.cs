using Unity.Collections;

namespace FlowFieldNavigation
{
    internal struct PathSectorStateMap
    {
        internal NativeHashMap<ulong, PathSectorState> Map;

        internal PathSectorStateMap(int initialCapacity, Allocator allocator)
        {
            Map = new NativeHashMap<ulong, PathSectorState>(initialCapacity, allocator);
        }
        internal void Dispose()
        {
            Map.Dispose();
        }
        internal bool TryAdd(int pathIndex, int sectorIndex, PathSectorState sectorState)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return Map.TryAdd(key, sectorState);
        }
        internal bool TryGet(int pathIndex, int sectorIndex, out PathSectorState sectorState)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return Map.TryGetValue(key, out sectorState);
        }
        internal bool Contains(int pathIndex, int sectorIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return Map.ContainsKey(key);
        }
        internal bool TryRemove(int pathIndex, int sectorIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return Map.Remove(key);
        }
        internal static void KeyToPathSector(ulong key, out int pathIndex, out int sectorIndex)
        {
            sectorIndex = (int)key;
            pathIndex = (int)(key >> 32);
        }
        internal static ulong PathSectorToKey(int pathIndex, int sectorIndex)
        {
            ulong key = 0;
            key |= (ulong)pathIndex;
            key <<= 32;
            key |= (ulong)sectorIndex;
            return key;
        }

    }
}