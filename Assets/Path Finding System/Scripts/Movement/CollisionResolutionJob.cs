using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct CollisionResolutionJob : IJobParallelFor
{
    [ReadOnly] public AgentSpatialHashGrid AgentSpatialHashGrid;
    [ReadOnly] public NativeArray<RoutineResult> RoutineResultArray;
    [WriteOnly] public NativeArray<float2> AgentPositionChangeBuffer;
    public void Execute(int index)
    {
        AgentMovementData agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[index];
        bool hasForeignInFront = RoutineResultArray[index].HasForeignInFront;
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalResolution = 0;
        float agentRadius = agentData.Radius;
        float maxResolutionLength = 0;
        float checkRange = agentRadius;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    if (j + sliceStart == index) { continue; }

                    AgentStatus mateStatus = agentsToCheck[j].Status;
                    float mateRadius = agentsToCheck[j].Radius;
                    float3 matePosition = agentsToCheck[j].Position;
                    float2 matePos = new float2(matePosition.x, matePosition.z);
                    float desiredDistance = checkRange + mateRadius;
                    float distance = math.distance(matePos, agentPos);
                    float overlapping = desiredDistance - distance;
                    if (overlapping <= 0) { continue; }
                    bool mateInFront = math.dot(agentData.CurrentDirection, matePos - agentPos) > 0;
                    float resoltionMultiplier = GetMultiplier(agentData.Status, mateStatus, agentRadius, mateRadius, hasForeignInFront, mateInFront);
                    if (resoltionMultiplier == 0f) { continue; }
                    resoltionMultiplier *= overlapping;
                    totalResolution += math.normalizesafe(agentPos - matePos) * resoltionMultiplier;
                    maxResolutionLength = math.select(resoltionMultiplier, maxResolutionLength, maxResolutionLength >= resoltionMultiplier);
                }
            }
        }
        float totalResolutionLen = math.length(totalResolution);
        totalResolution = math.select(totalResolution, totalResolution / totalResolutionLen * maxResolutionLength, totalResolutionLen > maxResolutionLength);
        AgentPositionChangeBuffer[index] = totalResolution;
    }
    float GetMultiplier(AgentStatus agentStatus, AgentStatus mateStatus, float agentRadius, float mateRadius, bool hasForeignInFront, bool agentInFront)
    {
        bool agentMoving = (agentStatus & AgentStatus.Moving) == AgentStatus.Moving;
        bool agentHoldGround = (agentStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround;
        bool agentStopped = agentStatus == 0;
        bool mateMoving = (mateStatus & AgentStatus.Moving) == AgentStatus.Moving;
        bool mateHoldGround = (mateStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround;
        bool mateStopped = mateStatus == 0;
        const float fullMultiplier = 1f;
        const float noneMultiplier = 0f;
        const float stoppedMultiplier = 0.4f;
        if(agentMoving && mateMoving)
        {
            float multiplier = math.select(1f, 0.2f, !agentInFront);
            multiplier = math.select(multiplier, 0.55f, hasForeignInFront);
            return mateRadius / (agentRadius + mateRadius) * multiplier;
        }
        if(agentStopped && mateStopped)
        {
            return mateRadius / (agentRadius + mateRadius);
        }
        if (agentStopped && (mateMoving || mateHoldGround))
        {
            return stoppedMultiplier;
        }
        if (agentMoving && mateHoldGround)
        {
            return fullMultiplier;
        }
        if (agentMoving && mateStopped)
        {
            return 0.05f;
        }
        return noneMultiplier;
    }
}