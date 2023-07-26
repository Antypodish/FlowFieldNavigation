using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine.Jobs;

[BurstCompile]
public struct AgentMovementDataCalculationJob : IJobParallelForTransform
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentMovementData> AgentMovementData;
    public NativeArray<float2> Directions;

    public void Execute(int index, TransformAccess transform)
    {
        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;
        if(data.SectorToPicked.Length == 0)
        {
            AgentMovementData[index] = data;
            Directions[index] = 0;
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
            AgentMovementData[index] = data;
            Directions[index] = 0;
            return;
        }

        FlowData flow = data.FlowField[sectorMark + local1d];
        switch (flow)
        {
            case FlowData.None:
                data.OutOfFieldFlag = true;
                AgentMovementData[index] = data;
                Directions[index] = 0;
                return;
            case FlowData.LOS:
                Directions[index] = -1f;
                break;
            case FlowData.N:
                Directions[index] = new float2(0f, 1f);
                break;
            case FlowData.E:
                Directions[index] = new float2(1f, 0f);
                break;
            case FlowData.S:
                Directions[index] = new float2(0f, -1f);
                break;
            case FlowData.W:
                Directions[index] = new float2(-1f, 0f);
                break;
            case FlowData.NE:
                Directions[index] = new float2(1f, 1f);
                break;
            case FlowData.SE:
                Directions[index] = new float2(1f, -1f);
                break;
            case FlowData.SW:
                Directions[index] = new float2(-1f, -1f);
                break;
            case FlowData.NW:
                Directions[index] = new float2(-1f, 1f);
                break;
        }
        if(flow != FlowData.LOS)
        {
            Directions[index] = math.normalize(Directions[index]);
        }
        AgentMovementData[index] = data;
    }
}