using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct CollisionResolutionJob : IJobParallelFor
    {
        [ReadOnly] internal AgentSpatialHashGrid AgentSpatialHashGrid;
        [ReadOnly] internal NativeArray<RoutineResult> RoutineResultArray;
        internal NativeArray<float3> AgentPositionChangeBuffer;
        public void Execute(int index)
        {
            AgentMovementData agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[index];
            bool agentStopped = agentData.Status == 0;
            bool hasForeignInFront = RoutineResultArray[index].HasForeignInFront;
            float2 agentPos2 = new float2(agentData.Position.x, agentData.Position.z);
            float2 totalResolution = 0;
            float agentRadius = agentData.Radius;
            float maxResolutionLength = 0;
            float checkRange = agentRadius;
            for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
            {
                SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos2, checkRange, i);
                while (iterator.HasNext())
                {
                    NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                    for (int j = 0; j < agentsToCheck.Length; j++)
                    {
                        if (j + sliceStart == index) { continue; }

                        AgentMovementData mateData = agentsToCheck[j];
                        AgentStatus mateStatus = mateData.Status;
                        float mateRadius = mateData.Radius;
                        float2 matePos2 = new float2(mateData.Position.x, mateData.Position.z);
                        float desiredDistance = checkRange + mateRadius;
                        float distance = math.distance(agentPos2, matePos2);
                        float overlapping = desiredDistance - distance;
                        if (overlapping <= 0) { continue; }
                        bool mateInFront = math.dot(agentData.CurrentDirection, matePos2 - agentPos2) > 0;
                        float resoltionMultiplier = GetMultiplier(agentData.Status, mateStatus, mateInFront, hasForeignInFront);
                        if (resoltionMultiplier == 0f) { continue; }
                        resoltionMultiplier *= overlapping;

                        float2 resolutionForce = math.normalizesafe(agentPos2 - matePos2) * resoltionMultiplier;
                        resolutionForce = math.select(resolutionForce, new float2(sliceStart + j, 1), distance == 0 && index < sliceStart + j);
                        totalResolution += resolutionForce;
                        maxResolutionLength = math.select(resoltionMultiplier, maxResolutionLength, maxResolutionLength >= resoltionMultiplier);
                    }
                }
            }
            float totalResolutionLen = math.length(totalResolution);

            //Enable if you want speed to be considered while calculating maxResolutionLength. More realistic, but increased overlapping.
            //maxResolutionLength = math.select(agentData.Speed * 0.02f, maxResolutionLength, maxResolutionLength < agentData.Speed * 0.016f || !agentStopped);
            totalResolution = math.select(totalResolution, totalResolution / totalResolutionLen * maxResolutionLength, totalResolutionLen > maxResolutionLength);
            AgentPositionChangeBuffer[index] += new float3(totalResolution.x, 0f, totalResolution.y);
        }
        float GetMultiplier(AgentStatus agentStatus, AgentStatus mateStatus, bool mateInFront, bool hasForeignAgentInFront)
        {
            bool agentMoving = (agentStatus & AgentStatus.Moving) == AgentStatus.Moving;
            bool agentHoldGround = (agentStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround;
            bool agentStopped = agentStatus == 0;
            bool mateMoving = (mateStatus & AgentStatus.Moving) == AgentStatus.Moving;
            bool mateHoldGround = (mateStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround;
            bool mateStopped = mateStatus == 0;

            if ((agentStopped && mateStopped) || (agentHoldGround && mateHoldGround)) { return 0.5f; }
            if(mateHoldGround && !agentHoldGround) { return 1; }
            if (agentHoldGround && !mateHoldGround) { return 0; }
            if(mateMoving && agentStopped) { return 0.9f; }
            if(agentMoving && mateStopped) { return 0.1f; }
            if (hasForeignAgentInFront) { return 0.2f; }
            return math.select(0.4f, 0.5f, mateInFront);
        }
    }

}
