using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct LocalAvoidanceJob : IJob
{
    public float SeekMultiplier;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<float2> AgentDirections;


    public void Execute()
    {
        for(int index = 0; index < AgentMovementDataArray.Length; index++)
        {
            float2 flow = AgentMovementDataArray[index].Flow;
            float2 velocity = AgentMovementDataArray[index].Velocity;
            float speed = AgentMovementDataArray[index].Speed;
            float2 seek = flow * speed - velocity;
            float seekLength = math.length(seek);
            float2 steeringToSeek = math.normalizesafe(seek) * math.select(seekLength, SeekMultiplier, SeekMultiplier < seekLength);
            AgentDirections[index] = velocity + steeringToSeek;
        }
    }
}