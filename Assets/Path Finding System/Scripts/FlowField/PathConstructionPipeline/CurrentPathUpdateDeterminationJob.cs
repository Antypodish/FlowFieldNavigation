using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
[BurstCompile]
public struct CurrentPathUpdateDeterminationJob : IJob
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    [ReadOnly] public NativeArray<UnsafeList<PathSectorState>> PathSectorStateTableArray;
    [ReadOnly] public NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] public NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<int> AgentCurrentPathIndicies;
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    [WriteOnly] public NativeReference<int> CurrentPathSourceCount;
    public NativeArray<PathRoutineData> PathRoutineDataArray;
    public NativeArray<PathTask> AgentPathTasks;
    public void Execute()
    {
        int curPathSourceCount = 0;
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            float3 agentPosition3d = AgentDataArray[i].Position;
            float2 agentPosition2d = new float2(agentPosition3d.x, agentPosition3d.z);
            int newPathIndex = AgentNewPathIndicies[i];
            int curPathIndex = AgentCurrentPathIndicies[i];
            if (newPathIndex != -1 || curPathIndex == -1) { continue; }

            UnsafeList<PathSectorState> curSectorStateTable = PathSectorStateTableArray[curPathIndex];
            PathLocationData curLocationData = PathLocationDataArray[curPathIndex];
            PathFlowData curFlowData = PathFlowDataArray[curPathIndex];
            PathRoutineData curRoutineData = PathRoutineDataArray[curPathIndex];
            int2 agentGeneral2d = FlowFieldUtilities.PosTo2D(agentPosition2d, TileSize);
            int2 agentSector2d = FlowFieldUtilities.GetSector2D(agentGeneral2d, SectorColAmount);
            int agentSector1d = FlowFieldUtilities.To1D(agentSector2d, SectorMatrixColAmount);
            int2 agentSectorStart2d = FlowFieldUtilities.GetSectorStartIndex(agentSector2d, SectorColAmount);
            int agentLocal1d = FlowFieldUtilities.GetLocal1D(agentGeneral2d, agentSectorStart2d, SectorColAmount);
            int sectorFlowStartIndex = curLocationData.SectorToPicked[agentSector1d];
            FlowData flow = curFlowData.FlowField[sectorFlowStartIndex + agentLocal1d];
            PathSectorState sectorState = curSectorStateTable[agentSector1d];
            bool sectorIncluded = sectorState != 0;
            bool sectorSource = (sectorState & PathSectorState.Source) == PathSectorState.Source;
            bool flowCalculated = (sectorState & PathSectorState.FlowCalculated) == PathSectorState.FlowCalculated;
            bool canGetFlow = flow.IsValid();
            if (!sectorSource && (!sectorIncluded || (flowCalculated && !canGetFlow)))
            {
                curRoutineData.PathAdditionSourceCount++;
                AgentPathTasks[i] |= PathTask.PathAdditionRequest;
                PathRoutineDataArray[curPathIndex] = curRoutineData;
                curPathSourceCount++;
            }
            else if (sectorIncluded && !flowCalculated && !canGetFlow)
            {
                curRoutineData.FlowRequestSourceCount++;
                AgentPathTasks[i] |= PathTask.FlowRequest;
                PathRoutineDataArray[curPathIndex] = curRoutineData;
                curPathSourceCount++;
            }

            CurrentPathSourceCount.Value = curPathSourceCount;
        }
    }
}
