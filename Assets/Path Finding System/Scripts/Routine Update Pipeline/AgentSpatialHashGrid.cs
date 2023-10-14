using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[BurstCompile]
public struct AgentSpatialHashGrid
{
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<UnsafeList<HashTile>> HashGrids;

    public NativeSlice<AgentMovementData> Get(float2 position, float size)
    {
        int startIndex = 0;
        int length = 0;


        return new NativeSlice<AgentMovementData>(AgentMovementDataArray, startIndex, length);
    }
}
