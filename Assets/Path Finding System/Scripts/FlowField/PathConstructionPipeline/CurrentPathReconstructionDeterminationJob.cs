using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
[BurstCompile]
public struct CurrentPathReconstructionDeterminationJob : IJob
{
    [ReadOnly] public NativeArray<AgentData> AgentDataArray; 
    public NativeArray<PathData> CurrentPaths;
    public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<int> AgentCurPathIndicies;
    public NativeList<PathRequest> PathRequests;
    public void Execute()
    {
        //CHECK IF DYNAMIC PATH TARGETS ARE MOVED
        for (int i = 0; i < CurrentPaths.Length; i++)
        {
            PathData curPath = CurrentPaths[i];
            if (curPath.State == PathState.Removed || curPath.Type == DestinationType.StaticDestination || curPath.DestinationState != DynamicDestinationState.OutOfReach) { continue; }
            curPath.ReconstructionRequestIndex = PathRequests.Length;
            CurrentPaths[i] = curPath;
            PathRequest reconReq = new PathRequest(curPath.TargetAgentIndex);

            float3 targetAgentPos = AgentDataArray[reconReq.TargetAgentIndex].Position;
            float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
            reconReq.Destination = targetAgentPos2;

            PathRequests.Add(reconReq);
        }

        //SET NEW PATHS OF AGENTS WHOSE PATHS ARE RECONSTRUCTED
        for (int i = 0; i < AgentCurPathIndicies.Length; i++)
        {
            int curPathIndex = AgentCurPathIndicies[i];
            if (curPathIndex == -1) { continue; }
            PathData curPath = CurrentPaths[curPathIndex];
            if (curPath.State == PathState.Removed || curPath.ReconstructionRequestIndex == -1) { continue; }
            AgentNewPathIndicies[i] = curPath.ReconstructionRequestIndex;
        }
    }
}
