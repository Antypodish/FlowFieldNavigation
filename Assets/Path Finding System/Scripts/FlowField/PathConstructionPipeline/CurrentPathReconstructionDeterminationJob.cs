using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
[BurstCompile]
public struct CurrentPathReconstructionDeterminationJob : IJob
{
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    [ReadOnly] public NativeArray<PathState> PathStateArray;
    [ReadOnly] public NativeArray<PathDestinationData> PathDestinationDataArray;
    public NativeArray<PathRoutineData> PathRoutineDataArray;
    public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<int> AgentCurPathIndicies;
    public NativeList<PathRequest> PathRequests;
    public void Execute()
    {
        //CHECK IF DYNAMIC PATH TARGETS ARE MOVED
        for (int i = 0; i < PathRoutineDataArray.Length; i++)
        {
            PathState curPathState = PathStateArray[i];
            PathDestinationData curDestinationData = PathDestinationDataArray[i];
            PathRoutineData curRoutineData = PathRoutineDataArray[i];
            if (curPathState == PathState.Removed || curDestinationData.DestinationType == DestinationType.StaticDestination || curRoutineData.DestinationState != DynamicDestinationState.OutOfReach) { continue; }
            curRoutineData.ReconstructionRequestIndex = PathRequests.Length;
            PathRoutineDataArray[i] = curRoutineData;
            PathRequest reconReq = new PathRequest(curDestinationData.TargetAgentIndex);

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
            PathRoutineData curRoutineData = PathRoutineDataArray[curPathIndex];
            PathState curPathState = PathStateArray[curPathIndex];
            if (curPathState == PathState.Removed || curRoutineData.ReconstructionRequestIndex == -1) { continue; }
            AgentNewPathIndicies[i] = curRoutineData.ReconstructionRequestIndex;
        }
    }
}
