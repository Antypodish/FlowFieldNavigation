using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.UIElements;

[BurstCompile]
public struct AgentRoutineDataCalculationJob : IJobParallelFor
{
    public float TileSize;
    public int FieldColAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentMovementData> AgentMovementData;

    public void Execute(int index)
    {
        float tileSize = TileSize;
        AgentMovementData data = AgentMovementData[index];

        int2 sector2d = new int2((int)math.floor(data.Position.x / (SectorColAmount * TileSize)), (int)math.floor(data.Position.z / (SectorColAmount * TileSize)));
        int2 general2d = new int2((int)math.floor(data.Position.x / TileSize), (int)math.floor(data.Position.z / TileSize));
        int2 sectorStart2d = sector2d * SectorColAmount;
        int2 local2d = general2d - sectorStart2d;
        int local1d = local2d.y * SectorColAmount + local2d.x;

        data.Local1d = (ushort)local1d;
        data.Offset = FlowFieldUtilities.RadiusToOffset(data.Radius, tileSize);
        
        if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
        {
            data.DesiredDirection = 0;
            AgentMovementData[index] = data;
            return;
        }
        if (data.SectorFlowStride == 0 || data.SectorFlowStride >= data.FlowField.Length)
        {
            data.DesiredDirection = 0;
            AgentMovementData[index] = data;
            return;
        }

        FlowData flowData = data.FlowField[data.SectorFlowStride + local1d];
        bool isLos = data.LOSMap.IsLOS(data.SectorFlowStride + local1d);
        float2 agentPos = new float2(data.Position.x, data.Position.z);
        float2 destination = data.Destination;
        float2 fieldFlow = flowData.GetFlow(tileSize);
        fieldFlow = math.select(0f, fieldFlow, flowData.IsValid());
        float2 perfectFlow = math.normalizesafe(destination - agentPos);
        float2 flow = math.select(fieldFlow, perfectFlow, isLos);
        flow = math.select(GetSmoothFlow(data.DesiredDirection, flow, data.Speed), flow, math.dot(data.DesiredDirection, flow) < 0.7f);
        data.DesiredDirection = flow;
        AgentMovementData[index] = data;
    }
    float2 GetSmoothFlow(float2 currentDirection, float2 desiredDirection, float speed)
    {
        float2 steeringToSeek = desiredDirection - currentDirection;
        float steeringToSeekLen = math.length(steeringToSeek);
        float2 steeringForce = math.select(steeringToSeek / steeringToSeekLen, 0f, steeringToSeekLen == 0) * math.select(speed / 1000, steeringToSeekLen, steeringToSeekLen < speed / 1000);
        return math.normalizesafe(currentDirection + steeringForce);
    }
}