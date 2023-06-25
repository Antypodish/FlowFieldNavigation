using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.SocialPlatforms;

[BurstCompile]
public struct AgentMovementDataCalculationJob : IJobParallelFor
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public UnsafeList<AgentMovementData> AgentMovementData;
    [ReadOnly] public NativeList<FlowFieldSector> FlowField;
    [ReadOnly] public NativeArray<int> SectorMarks;
    public void Execute(int index)
    {
        AgentMovementData node = AgentMovementData[index];
        float3 nodePos3 = node.pos;

        int2 sector2d = new int2((int)math.floor(nodePos3.x / (SectorColAmount * TileSize)), (int)math.floor(nodePos3.z / (SectorColAmount * TileSize)));
        int2 general2d = new int2((int)math.floor(nodePos3.x / TileSize), (int)math.floor(nodePos3.z / TileSize));
        int2 sectorStart2d = sector2d * SectorColAmount;
        int2 local2d = general2d - sectorStart2d;
        int local1d = local2d.y * SectorColAmount + local2d.x;
        int sector1d = sector2d.y * SectorMatrixColAmount + sector2d.x;

        FlowData flow = FlowField[SectorMarks[sector1d]].flowfieldSector[local1d];
        switch (flow)
        {
            case FlowData.LOS:
                node.direction = 0;
                break;
            case FlowData.N:
                node.direction = new float2(0f, 1f);
                break;
            case FlowData.E:
                node.direction = new float2(1f, 0f);
                break;
            case FlowData.S:
                node.direction = new float2(0f, -1f);
                break;
            case FlowData.W:
                node.direction = new float2(-1f, 0f);
                break;
            case FlowData.NE:
                node.direction = new float2(1f, 1f);
                break;
            case FlowData.SE:
                node.direction = new float2(1f, -1f);
                break;
            case FlowData.SW:
                node.direction = new float2(-1f, -1f);
                break;
            case FlowData.NW:
                node.direction = new float2(-1f, 1f);
                break;
        }
        if(flow != FlowData.LOS)
        {
            node.direction = math.normalize(node.direction);
        }
        node.local1d = (ushort) local1d;
        node.sector1d = (ushort) sector1d;
        AgentMovementData[index] = node;
    }
}