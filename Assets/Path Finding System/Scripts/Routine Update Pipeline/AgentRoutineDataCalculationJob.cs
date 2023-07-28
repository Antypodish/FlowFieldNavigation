using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine.Jobs;

[BurstCompile]
public struct AgentRoutineDataCalculationJob : IJobParallelForTransform
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentMovementData> AgentMovementData;

    public void Execute(int index, TransformAccess transform)
    {
        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;
        if((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
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
        AgentMovementData[index] = data;
    }
}