using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;

[BurstCompile]
public struct AgentMovementDataArrayPreperationJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public void Execute(int index, TransformAccess transform)
    {
        AgentData agentData = AgentDataArray[index];
        AgentMovementData agentMovementData = AgentMovementDataArray[index];
        if (agentMovementData.FlowField.Length == 0)
        {
            agentMovementData.Position = transform.position;
            agentMovementData.Radius = agentData.Radius;
            agentMovementData.Local1d = 0;
            agentMovementData.DesiredDirection = 0;
            agentMovementData.SeperationForce = 0;
            agentMovementData.CurrentDirection = agentData.Direction;
            agentMovementData.Speed = agentData.Speed;
            agentMovementData.Status = agentData.Status;
            agentMovementData.Avoidance = agentData.Avoidance;
            agentMovementData.MovingAvoidance = agentData.MovingAvoidance;
            agentMovementData.RoutineStatus = 0;
            agentMovementData.PathId = -1;
            agentMovementData.TensionPowerIndex = -1;
            agentMovementData.SplitInfo = agentData.SplitInfo;
            agentMovementData.SplitInterval = agentData.SplitInterval;
            AgentMovementDataArray[index] = agentMovementData;
        }
        else
        {
            agentMovementData.Position = transform.position;
            agentMovementData.Radius = agentData.Radius;
            agentMovementData.Local1d = 0;
            agentMovementData.DesiredDirection = 0;
            agentMovementData.SeperationForce = 0;
            agentMovementData.CurrentDirection = agentData.Direction;
            agentMovementData.Speed = agentData.Speed;
            agentMovementData.Destination = agentData.Destination;
            agentMovementData.Waypoint = agentData.waypoint;
            agentMovementData.Status = agentData.Status;
            agentMovementData.Avoidance = agentData.Avoidance;
            agentMovementData.MovingAvoidance = agentData.MovingAvoidance;
            agentMovementData.RoutineStatus = 0;
            agentMovementData.TensionPowerIndex = -1;
            agentMovementData.SplitInfo = agentData.SplitInfo;
            agentMovementData.SplitInterval = agentData.SplitInterval;
            AgentMovementDataArray[index] = agentMovementData;
        }
    }
}
