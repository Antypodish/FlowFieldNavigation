using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SocialPlatforms;

[BurstCompile]
public struct AgentMovementDataCalculationJob : IJobParallelForTransform
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentMovementData> AgentMovementData;

    public void Execute(int index, TransformAccess transform)
    {
        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;
        if(data.SectorToPicked.Length == 0) { return; }
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
            AgentMovementData[index] = data;
            return;
        }

        FlowData flow = data.FlowField[sectorMark + local1d];
        switch (flow)
        {
            case FlowData.None:
                data.OutOfFieldFlag = true;
                AgentMovementData[index] = data;
                return;
            case FlowData.LOS:
                data.Direction = -1f;
                break;
            case FlowData.N:
                data.Direction = new float2(0f, 1f);
                break;
            case FlowData.E:
                data.Direction = new float2(1f, 0f);
                break;
            case FlowData.S:
                data.Direction = new float2(0f, -1f);
                break;
            case FlowData.W:
                data.Direction = new float2(-1f, 0f);
                break;
            case FlowData.NE:
                data.Direction = new float2(1f, 1f);
                break;
            case FlowData.SE:
                data.Direction = new float2(1f, -1f);
                break;
            case FlowData.SW:
                data.Direction = new float2(-1f, -1f);
                break;
            case FlowData.NW:
                data.Direction = new float2(-1f, 1f);
                break;
        }
        if(flow != FlowData.LOS)
        {
            data.Direction = math.normalize(data.Direction);
        }
        AgentMovementData[index] = data;
    }
}