using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct FinalPathRequestSourceSubmitJob : IJob
    {
        [ReadOnly] internal NativeArray<float3> AgentPositions;
        [ReadOnly] internal NativeArray<int> AgentNewPathIndicies;
        [ReadOnly] internal NativeArray<int> AgentCurPathIndicies;
        [ReadOnly] internal NativeReference<int> PathRequestSourceCount;
        [ReadOnly] internal NativeReference<int> CurrentPathSourceCount;
        [ReadOnly] internal NativeArray<PathTask> AgentTasks;
        [ReadOnly] internal NativeArray<PathState> PathStateArray;
        internal NativeArray<PathRoutineData> PathRoutineDataArray;
        internal NativeList<FinalPathRequest> FinalPathRequests;
        internal NativeList<float2> Sources;

        public void Execute()
        {
            Sources.Length = PathRequestSourceCount.Value + CurrentPathSourceCount.Value;

            int sourceCurIndex = 0;
            //SET PATH REQUEST SOURCE START INDICIES OF PATH REQUESTS
            for (int i = 0; i < FinalPathRequests.Length; i++)
            {
                FinalPathRequest req = FinalPathRequests[i];
                req.SourcePositionStartIndex = sourceCurIndex;
                sourceCurIndex += req.SourceCount;
                req.SourceCount = 0;
                FinalPathRequests[i] = req;
            }

            //SET CUR PATH SOURCE START INDICIES
            for (int i = 0; i < PathRoutineDataArray.Length; i++)
            {
                PathRoutineData curRoutineData = PathRoutineDataArray[i];
                PathState curPathState = PathStateArray[i];
                bool removed = curPathState == PathState.Removed;
                bool hasFlowRequest = curRoutineData.FlowRequestSourceCount != 0;
                bool hasPathAdditionRequest = curRoutineData.PathAdditionSourceCount != 0;
                if (removed) { continue; }
                if (hasFlowRequest)
                {
                    curRoutineData.Task |= PathTask.FlowRequest;
                    curRoutineData.FlowRequestSourceStart = sourceCurIndex;
                    sourceCurIndex += curRoutineData.FlowRequestSourceCount;
                    curRoutineData.FlowRequestSourceCount = 0;
                    PathRoutineDataArray[i] = curRoutineData;
                }
                if (hasPathAdditionRequest)
                {
                    curRoutineData.Task |= PathTask.PathAdditionRequest;
                    curRoutineData.PathAdditionSourceStart = sourceCurIndex;
                    sourceCurIndex += curRoutineData.PathAdditionSourceCount;
                    curRoutineData.PathAdditionSourceCount = 0;
                    PathRoutineDataArray[i] = curRoutineData;
                }
            }

            //SUBMIT SOURCES
            for (int i = 0; i < AgentPositions.Length; i++)
            {
                int newPathIndex = AgentNewPathIndicies[i];
                int curPathIndex = AgentCurPathIndicies[i];

                if (newPathIndex != -1)
                {
                    FinalPathRequest req = FinalPathRequests[newPathIndex];
                    float3 agentPos3 = AgentPositions[i];
                    float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                    Sources[req.SourcePositionStartIndex + req.SourceCount] = agentPos;
                    req.SourceCount = req.SourceCount + 1;
                    FinalPathRequests[newPathIndex] = req;
                }
                else if (curPathIndex != -1)
                {
                    PathRoutineData curRoutineData = PathRoutineDataArray[curPathIndex];
                    PathTask agentTask = AgentTasks[i];
                    bool agentFlowRequested = (agentTask & PathTask.FlowRequest) == PathTask.FlowRequest;
                    bool agentPathAdditionRequested = (agentTask & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
                    if (agentFlowRequested)
                    {
                        float3 agentPos3 = AgentPositions[i];
                        float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                        Sources[curRoutineData.FlowRequestSourceStart + curRoutineData.FlowRequestSourceCount] = agentPos;
                        curRoutineData.FlowRequestSourceCount++;
                        PathRoutineDataArray[curPathIndex] = curRoutineData;
                    }
                    if (agentPathAdditionRequested)
                    {
                        float3 agentPos3 = AgentPositions[i];
                        float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                        Sources[curRoutineData.PathAdditionSourceStart + curRoutineData.PathAdditionSourceCount] = agentPos;
                        curRoutineData.PathAdditionSourceCount++;
                        PathRoutineDataArray[curPathIndex] = curRoutineData;
                    }
                }
            }
        }
    }

}