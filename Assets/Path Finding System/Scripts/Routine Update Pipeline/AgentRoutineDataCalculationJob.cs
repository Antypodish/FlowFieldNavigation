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
    public NativeArray<OutOfFieldStatus> AgentOutOfFieldStatusList;
    [ReadOnly] public UnsafeList<UnsafeList<byte>> CostFields;

    public void Execute(int index)
    {
        float tileSize = TileSize;


        AgentMovementData data = AgentMovementData[index];


        int2 sector2d = new int2((int)math.floor(data.Position.x / (SectorColAmount * TileSize)), (int)math.floor(data.Position.z / (SectorColAmount * TileSize)));
        int2 general2d = new int2((int)math.floor(data.Position.x / TileSize), (int)math.floor(data.Position.z / TileSize));
        int2 sectorStart2d = sector2d * SectorColAmount;
        int2 local2d = general2d - sectorStart2d;
        int local1d = local2d.y * SectorColAmount + local2d.x;
        int sector1d = sector2d.y * SectorMatrixColAmount + sector2d.x;

        data.Local1d = (ushort)local1d;


        if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
        {
            AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
            {
                PathId = data.PathId,
                IsOutOfField = false,
                Sector1d = (ushort)sector1d,
            };
            data.DesiredDirection = 0;
            AgentMovementData[index] = data;
            return;
        }
        int sectorMark = data.SectorToPicked[sector1d];
        if (sectorMark == 0)
        {
            AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
            {
                PathId = data.PathId,
                IsOutOfField = true,
                Sector1d = (ushort)sector1d,
            };
            data.DesiredDirection = 0;
            AgentMovementData[index] = data;
            return;
        }
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort)sector1d,
        };

        FlowData flowData = data.FlowField[sectorMark + local1d];
        float2 agentPos = new float2(data.Position.x, data.Position.z);
        float2 destination = data.Destination;
        float2 flow = math.select(flowData.GetFlow(tileSize), math.normalizesafe(destination - agentPos), flowData.IsLOS());
        flow = math.select(GetSmoothFlow(data.DesiredDirection, flow, data.Speed), flow, math.dot(data.DesiredDirection, flow) < 0.7f);
        data.DesiredDirection = flow;
        AgentMovementData[index] = data;
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort)sector1d,
        };
    }
    float2 GetSmoothFlow(float2 currentDirection, float2 desiredDirection, float speed)
    {
        float2 steeringToSeek = desiredDirection - currentDirection;
        float steeringToSeekLen = math.length(steeringToSeek);
        float2 steeringForce = math.select(steeringToSeek / steeringToSeekLen, 0f, steeringToSeekLen == 0) * math.select(speed / 1000, steeringToSeekLen, steeringToSeekLen < speed / 1000);
        return math.normalizesafe(currentDirection + steeringForce);
    }
}