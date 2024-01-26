using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
[BurstCompile]
internal struct AgentLookingForPathSubmissionJob : IJobParallelFor
{
    internal float TileSize;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int SectorTileAmount;
    internal float2 FieldGridStartPos;
    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    [ReadOnly] internal NativeArray<UnsafeListReadOnly<byte>> CostFields;
    internal NativeArray<int> AgentNewPathIndicies;
    internal NativeArray<bool> AgentLookingForPathFlags;
    public void Execute(int index)
    {
        int newPathIndex = AgentNewPathIndicies[index];
        if(newPathIndex == -1) { return; }

        AgentData agentData = AgentDataArray[index];
        float2 agentPos2 = new float2(agentData.Position.x, agentData.Position.z);
        int agentOffset = FlowFieldUtilities.RadiusToOffset(agentData.Radius, TileSize);
        int2 agentIndex = FlowFieldUtilities.PosTo2D(agentPos2, TileSize, FieldGridStartPos);
        LocalIndex1d agentLocal = FlowFieldUtilities.GetLocal1D(agentIndex, SectorColAmount, SectorMatrixColAmount);
        byte agentIndexCost = CostFields[agentOffset][agentLocal.sector * SectorTileAmount + agentLocal.index];
        if(agentIndexCost == byte.MaxValue)
        {
            AgentLookingForPathFlags[index] = true;
            AgentNewPathIndicies[index] = -1;
            return;
        }
        AgentLookingForPathFlags[index] = false;
    }
}