using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct CollisionResolutionJob : IJobParallelFor
{
    [ReadOnly] public AgentSpatialHashGrid AgentSpatialHashGrid;
    [WriteOnly] public NativeArray<float2> AgentPositionChangeBuffer;
    public void Execute(int index)
    {
        AgentMovementData agentData = AgentSpatialHashGrid.RawAgentMovementDataArray[index];
        if((agentData.Status & AgentStatus.Moving) != AgentStatus.Moving) { return; }
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalResolution = 0;
        float agentRadius = agentData.Radius;
        float maxOverlapping = 0;
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
                    float resoltionMultiplier = mateRadius / (agentRadius + mateRadius);
                    float3 matePosition = agentsToCheck[j].Position;
                    if (mateStatus == 0) { continue; }
                    float2 matePos = new float2(matePosition.x, matePosition.z);
                    float desiredDistance = agentRadius + mateRadius;
                    float distance = math.distance(matePos, agentPos);
                    float overlapping = desiredDistance - distance;
                    if (overlapping <= 0) { continue; }
                    
                    overlapping = math.select(overlapping * resoltionMultiplier, overlapping, (mateStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround);
                    float2 resolutionDirection = (agentPos - matePos) / distance;
                    if (math.dot(agentData.CurrentDirection, matePos - agentPos) < 0 && mateRadius <= agentRadius) { continue; }
                    totalResolution += math.select(resolutionDirection * overlapping, 0f, distance == 0);
                    maxOverlapping = math.select(overlapping, maxOverlapping, maxOverlapping >= overlapping);
                }
            }
        }
        float totalResolutionLen = math.length(totalResolution);
        totalResolution = math.select(totalResolution, totalResolution / totalResolutionLen * maxOverlapping, totalResolutionLen > maxOverlapping);
        AgentPositionChangeBuffer[index] = totalResolution;
    }
}