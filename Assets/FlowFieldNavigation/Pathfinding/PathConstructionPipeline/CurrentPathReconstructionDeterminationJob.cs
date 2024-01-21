﻿using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
[BurstCompile]
internal struct CurrentPathReconstructionDeterminationJob : IJob
{
    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    [ReadOnly] internal NativeArray<PathState> PathStateArray;
    [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] internal NativeArray<int> PathFlockIndexArray;
    internal NativeArray<PathRoutineData> PathRoutineDataArray;
    internal NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] internal NativeArray<int> AgentCurPathIndicies;
    internal NativeList<PathRequest> PathRequests;
    public void Execute()
    {
        //CHECK IF DYNAMIC PATH TARGETS ARE MOVED
        for (int i = 0; i < PathRoutineDataArray.Length; i++)
        {
            PathState curPathState = PathStateArray[i];
            PathDestinationData curDestinationData = PathDestinationDataArray[i];
            PathRoutineData curRoutineData = PathRoutineDataArray[i];
            bool shouldNotConsider = curPathState == PathState.Removed;
            bool shouldNotReconstruct = curRoutineData.DestinationState != DynamicDestinationState.OutOfReach && (curRoutineData.Task & PathTask.Reconstruct) != PathTask.Reconstruct;
            if (shouldNotConsider || shouldNotReconstruct) { continue; }
            curRoutineData.ReconstructionRequestIndex = PathRequests.Length;
            PathRoutineDataArray[i] = curRoutineData;

            if(curDestinationData.DestinationType == DestinationType.DynamicDestination)
            {
                PathRequest reconReq = new PathRequest(curDestinationData.TargetAgentIndex);
                float3 targetAgentPos = AgentDataArray[reconReq.TargetAgentIndex].Position;
                float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
                reconReq.Destination = targetAgentPos2;
                reconReq.FlockIndex = PathFlockIndexArray[i];
                PathRequests.Add(reconReq);
            }
            else
            {
                PathRequest reconReq = new PathRequest(curDestinationData.DesiredDestination);
                reconReq.FlockIndex = PathFlockIndexArray[i];
                PathRequests.Add(reconReq);

            }
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
