using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct PathRoutineDataCalculationJob : IJobParallelFor
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    
    [ReadOnly] public NativeArray<UnsafeList<DijkstraTile>> TargetSectorIntegrations;
    [ReadOnly] public NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] public NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] public NativeArray<PathState> PathStateArray;
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    public NativeArray<PathDestinationData> PathDestinationDataArray;
    public NativeArray<PathRoutineData> PathOrganizationDataArray;
    public NativeArray<UnsafeList<PathSectorState>> PathSectorStateTables;

    public void Execute(int index)
    {
        PathState pathState = PathStateArray[index];
        if (pathState == PathState.Removed)
        {
            return;
        }
        UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrations[index];
        PathDestinationData destinationData = PathDestinationDataArray[index];
        if (destinationData.DestinationType == DestinationType.DynamicDestination)
        {
            float3 targetAgentPos = AgentDataArray[destinationData.TargetAgentIndex].Position;
            float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
            int2 oldTargetIndex = destinationData.TargetIndex;
            int2 newTargetIndex = (int2)math.floor(targetAgentPos2 / TileSize);
            int oldSector = FlowFieldUtilities.GetSector1D(oldTargetIndex, SectorColAmount, SectorMatrixColAmount);
            LocalIndex1d newLocal = FlowFieldUtilities.GetLocal1D(newTargetIndex, SectorColAmount, SectorMatrixColAmount);
            bool outOfReach = oldSector != newLocal.sector;
            DijkstraTile targetTile = targetSectorIntegration[newLocal.index];
            outOfReach = outOfReach || targetTile.IntegratedCost == float.MaxValue;
            DynamicDestinationState destinationState = oldTargetIndex.Equals(newTargetIndex) ? DynamicDestinationState.None : DynamicDestinationState.Moved;
            destinationState = outOfReach ? DynamicDestinationState.OutOfReach : destinationState;
            destinationData.Destination = targetAgentPos2;
            destinationData.TargetIndex = newTargetIndex;
            PathDestinationDataArray[index] = destinationData;

            PathRoutineData organizationData = PathOrganizationDataArray[index];
            organizationData.DestinationState = destinationState;
            PathOrganizationDataArray[index] = organizationData;
        }
    }
}
