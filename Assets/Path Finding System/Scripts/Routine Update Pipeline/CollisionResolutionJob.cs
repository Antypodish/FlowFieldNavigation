using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine.Jobs;

[BurstCompile]
public struct CollisionResolutionJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    public float SeperationRangeAddition;
    public void Execute(int index, TransformAccess transform)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        if((agentData.Status & AgentStatus.HoldGround) == AgentStatus.HoldGround) { return; }
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalResolution = 0;
        float agentRadius = agentData.Radius;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float mateRadius = mateData.Radius;
            float desiredDistance = agentRadius + mateRadius + SeperationRangeAddition;
            float distance = math.distance(matePos, agentPos);
            float overlapping = desiredDistance - distance;
            overlapping = math.select(overlapping, 0, overlapping < 0 || (mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround || index == i);
            float2 resolutionDirection = math.normalizesafe(agentPos - matePos);
            totalResolution += resolutionDirection * overlapping;
        }
        totalResolution = math.select(totalResolution, math.normalize(totalResolution), math.length(totalResolution) > 1);
        float3 curPos = transform.position;
        curPos += new float3(totalResolution.x, 0f, totalResolution.y);
        transform.position = curPos;
    }
}