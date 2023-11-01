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
        Waypoint(index);
    }
    void Normal(int index, TransformAccess transform)
    {/*
        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;
        if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
        {
            data.Flow = 0;
            AgentMovementData[index] = data;
            return;
        }
        int2 sector2d = new int2((int)math.floor(data.Position.x / (SectorColAmount * TileSize)), (int)math.floor(data.Position.z / (SectorColAmount * TileSize)));
        int2 general2d = new int2((int)math.floor(data.Position.x / TileSize), (int)math.floor(data.Position.z / TileSize));
        int2 sectorStart2d = sector2d * SectorColAmount;
        int2 local2d = general2d - sectorStart2d;
        int local1d = local2d.y * SectorColAmount + local2d.x;
        int sector1d = sector2d.y * SectorMatrixColAmount + sector2d.x;

        data.Local1d = (ushort)local1d;
        data.Sector1d = (ushort)sector1d;

        int sectorMark = data.SectorToPicked[sector1d];
        if (sectorMark == 0)
        {
            data.OutOfFieldFlag = true;
            data.Flow = 0;
            AgentMovementData[index] = data;
            return;
        }

        FlowData flow = data.FlowField[sectorMark + local1d];
        switch (flow)
        {
            case FlowData.None:
                data.OutOfFieldFlag = true;
                data.Flow = 0;
                AgentMovementData[index] = data;
                return;
            case FlowData.LOS:
                data.Flow = data.Destination - new float2(data.Position.x, data.Position.z);
                break;
            case FlowData.N:
                data.Flow = new float2(0f, 1f);
                break;
            case FlowData.E:
                data.Flow = new float2(1f, 0f);
                break;
            case FlowData.S:
                data.Flow = new float2(0f, -1f);
                break;
            case FlowData.W:
                data.Flow = new float2(-1f, 0f);
                break;
            case FlowData.NE:
                data.Flow = new float2(1f, 1f);
                break;
            case FlowData.SE:
                data.Flow = new float2(1f, -1f);
                break;
            case FlowData.SW:
                data.Flow = new float2(-1f, -1f);
                break;
            case FlowData.NW:
                data.Flow = new float2(-1f, 1f);
                break;
        }
        data.Flow = math.normalize(data.Flow);
        AgentMovementData[index] = data;*/
    }
    void Waypoint(int index)
    {
        float tileSize = TileSize;
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;


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
                Sector1d = (ushort) sector1d,
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
                Sector1d = (ushort) sector1d,
            };
            data.DesiredDirection = 0;
            AgentMovementData[index] = data;
            return;
        }
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort) sector1d,
        };

        FlowData flowData = data.FlowField[sectorMark + local1d];
        float2 agentPos = new float2(data.Position.x, data.Position.z);
        float2 destination = data.Destination;
        float2 flow = math.select(flowData.GetFlow(tileSize), math.normalizesafe(destination - agentPos), flowData.IsLOS());
        data.DesiredDirection = flow;
        AgentMovementData[index] = data;
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort) sector1d,
        };


    }
}
public struct Waypoint
{
    public int index;
    public float2 position;
    public WaypointDirection blockedDirection;

    public bool Equals(Waypoint wayp)
    {
        return position.Equals(wayp.position) && blockedDirection == wayp.blockedDirection;
    }
}
[Flags]
public enum WaypointDirection : byte
{
    None = 0,
    N = 1,
    E = 2,
    S = 4,
    W = 8
}
[Flags]
public enum CornerDirections : byte
{
    None = 0,
    NE = 1,
    SE = 2,
    SW = 4,
    NW = 8
}