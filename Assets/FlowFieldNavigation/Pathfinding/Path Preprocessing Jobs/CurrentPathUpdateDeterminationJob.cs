using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct CurrentPathUpdateDeterminationJob : IJob
    {
        internal float TileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal float2 FieldGridStartPos;
        [ReadOnly] internal NativeArray<UnsafeList<PathSectorState>> PathSectorStateTableArray;
        [ReadOnly] internal NativeArray<PathFlowData> PathFlowDataArray;
        [ReadOnly] internal NativeArray<int> AgentNewPathIndicies;
        [ReadOnly] internal NativeArray<int> AgentCurrentPathIndicies;
        [ReadOnly] internal NativeArray<float3> AgentPositions;
        [ReadOnly] internal PathSectorToFlowStartMapper FlowStartMap;
        [WriteOnly] internal NativeReference<int> CurrentPathSourceCount;
        internal NativeArray<PathRoutineData> PathRoutineDataArray;
        internal NativeArray<PathTask> AgentPathTasks;
        public void Execute()
        {
            int curPathSourceCount = 0;
            for (int i = 0; i < AgentPositions.Length; i++)
            {
                float3 agentPosition3d = AgentPositions[i];
                float2 agentPosition2d = new float2(agentPosition3d.x, agentPosition3d.z);
                int newPathIndex = AgentNewPathIndicies[i];
                int curPathIndex = AgentCurrentPathIndicies[i];
                if (newPathIndex != -1 || curPathIndex == -1) { continue; }

                UnsafeList<PathSectorState> curSectorStateTable = PathSectorStateTableArray[curPathIndex];
                PathFlowData curFlowData = PathFlowDataArray[curPathIndex];
                PathRoutineData curRoutineData = PathRoutineDataArray[curPathIndex];
                int2 agentGeneral2d = FlowFieldUtilities.PosTo2D(agentPosition2d, TileSize, FieldGridStartPos);
                int2 agentSector2d = FlowFieldUtilities.GetSector2D(agentGeneral2d, SectorColAmount);
                int agentSector1d = FlowFieldUtilities.To1D(agentSector2d, SectorMatrixColAmount);
                int2 agentSectorStart2d = FlowFieldUtilities.GetSectorStartIndex(agentSector2d, SectorColAmount);
                int agentLocal1d = FlowFieldUtilities.GetLocal1D(agentGeneral2d, agentSectorStart2d, SectorColAmount);
                bool succesfull = FlowStartMap.TryGet(curPathIndex, agentSector1d, out int sectorFlowStartIndex);
                FlowData flow = new FlowData();
                if (succesfull)
                {
                    flow = curFlowData.FlowField[sectorFlowStartIndex + agentLocal1d];
                }
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


}