using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct FinalPathRequestSourceSubmitJob : IJob
{
    [ReadOnly] public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<int> AgentCurPathIndicies;
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    [ReadOnly] public NativeReference<int> PathRequestSourceCount;
    [ReadOnly] public NativeReference<int> CurrentPathSourceCount;
    [ReadOnly] public NativeArray<PathTask> AgentTasks;
    public NativeList<FinalPathRequest> FinalPathRequests;
    public NativeArray<PathData> CurrentPaths;
    public NativeList<float2> Sources;

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
        for (int i = 0; i < CurrentPaths.Length; i++)
        {
            PathData curPath = CurrentPaths[i];
            bool removed = curPath.State == PathState.Removed;
            bool hasFlowRequest = curPath.FlowRequestSourceCount != 0;
            bool hasPathAdditionRequest = curPath.PathAdditionSourceCount != 0;
            if (removed) { continue; }
            if (hasFlowRequest)
            {
                curPath.Task |= PathTask.FlowRequest;
                curPath.FlowRequestSourceStart = sourceCurIndex;
                sourceCurIndex += curPath.FlowRequestSourceCount;
                curPath.FlowRequestSourceCount = 0;
                CurrentPaths[i] = curPath;
            }
            if (hasPathAdditionRequest)
            {
                curPath.Task |= PathTask.PathAdditionRequest;
                curPath.PathAdditionSourceStart = sourceCurIndex;
                sourceCurIndex += curPath.PathAdditionSourceCount;
                curPath.PathAdditionSourceCount = 0;
                CurrentPaths[i] = curPath;
            }
        }

        //SUBMIT SOURCES
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            int newPathIndex = AgentNewPathIndicies[i];
            int curPathIndex = AgentCurPathIndicies[i];

            if (newPathIndex != -1)
            {
                FinalPathRequest req = FinalPathRequests[newPathIndex];
                float3 agentPos3 = AgentDataArray[i].Position;
                float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                Sources[req.SourcePositionStartIndex + req.SourceCount] = agentPos;
                req.SourceCount = req.SourceCount + 1;
                FinalPathRequests[newPathIndex] = req;
            }
            else if (curPathIndex != -1)
            {
                PathData curPath = CurrentPaths[curPathIndex];
                PathTask agentTask = AgentTasks[i];
                bool agentFlowRequested = (agentTask & PathTask.FlowRequest) == PathTask.FlowRequest;
                bool agentPathAdditionRequested = (agentTask & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
                if (agentFlowRequested)
                {
                    float3 agentPos3 = AgentDataArray[i].Position;
                    float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                    Sources[curPath.FlowRequestSourceStart + curPath.FlowRequestSourceCount] = agentPos;
                    curPath.FlowRequestSourceCount++;
                    CurrentPaths[curPathIndex] = curPath;
                }
                if (agentPathAdditionRequested)
                {
                    float3 agentPos3 = AgentDataArray[i].Position;
                    float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                    Sources[curPath.PathAdditionSourceStart + curPath.PathAdditionSourceCount] = agentPos;
                    curPath.PathAdditionSourceCount++;
                    CurrentPaths[curPathIndex] = curPath;
                }
            }
        }
    }
}