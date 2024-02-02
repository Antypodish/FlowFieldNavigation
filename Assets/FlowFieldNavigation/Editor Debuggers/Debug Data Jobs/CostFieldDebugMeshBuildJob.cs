using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

internal struct CostFieldDebugMeshBuildJob : IJob
{
    internal int FieldColAmount;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int SectorTileAmount;
    internal float TileSize;
    internal int2 StartFieldIndex;
    internal int2 EndFieldIndex;
    internal float2 FieldGridStartPos;

    [ReadOnly] internal TriangleSpatialHashGrid TriangleSpatialHashGrid;
    [ReadOnly] internal NativeArray<float3> HeightMeshVerts;
    [ReadOnly] internal NativeArray<byte> Costs;
    internal NativeList<Vector3> Verts;
    internal NativeList<int> Trigs;

    public void Execute()
    {
        for(int r = StartFieldIndex.y; r <= EndFieldIndex.y; r++)
        {
            for (int c = StartFieldIndex.x; c <= EndFieldIndex.x; c++)
            {
                int2 general2d = new int2(c, r);
                LocalIndex1d local = FlowFieldUtilities.GetLocal1D(general2d, SectorColAmount, SectorMatrixColAmount);
                byte cost = Costs[local.sector * SectorTileAmount + local.index];
                if(cost != byte.MaxValue) { continue; }

                float2 bl = FlowFieldUtilities.IndexToStartPos(general2d, TileSize, FieldGridStartPos);
                float2 tl = bl + new float2(0, TileSize);
                float2 tr = bl + new float2(TileSize, TileSize);
                float2 br = bl + new float2(TileSize, 0);

                float3 bl3 = new float3(bl.x, GetHeight(bl) + 0.1f, bl.y);
                float3 tl3 = new float3(tl.x, GetHeight(tl) + 0.1f, tl.y);
                float3 tr3 = new float3(tr.x, GetHeight(tr) + 0.1f, tr.y);
                float3 br3 = new float3(br.x, GetHeight(br) + 0.1f, br.y);

                int blIndex = Verts.Length;
                int tlIndex = blIndex + 1;
                int trIndex = blIndex + 2;
                int brIndex = blIndex + 3;

                Verts.Add(bl3);
                Verts.Add(tl3);
                Verts.Add(tr3);
                Verts.Add(br3);

                Trigs.Add(blIndex);
                Trigs.Add(tlIndex);
                Trigs.Add(trIndex);
                Trigs.Add(trIndex);
                Trigs.Add(brIndex);
                Trigs.Add(blIndex);
            }
        }
    }

    float GetHeight(float2 pos)
    {
        float curHeight = float.MinValue;
        for (int i = 0; i < TriangleSpatialHashGrid.GetGridCount(); i++)
        {
            bool succesfull = TriangleSpatialHashGrid.TryGetIterator(pos, i, out TriangleSpatialHashGridIterator triangleGridIterator);
            if (!succesfull) { return 0; }
            while (triangleGridIterator.HasNext())
            {
                NativeSlice<int> triangles = triangleGridIterator.GetNextRow();
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    int v1Index = triangles[j];
                    int v2Index = triangles[j + 1];
                    int v3Index = triangles[j + 2];
                    float3 v13d = HeightMeshVerts[v1Index];
                    float3 v23d = HeightMeshVerts[v2Index];
                    float3 v33d = HeightMeshVerts[v3Index];
                    float2 v1 = new float2(v13d.x, v13d.z);
                    float2 v2 = new float2(v23d.x, v23d.z);
                    float2 v3 = new float2(v33d.x, v33d.z);

                    BarycentricCoordinates barCords = GetBarycentricCoordinatesForEachVectorInTheOrderUVW(v1, v2, v3, pos);
                    if (barCords.u < 0 || barCords.w < 0 || barCords.v < 0) { continue; }
                    float newHeight = v13d.y * barCords.u + v23d.y * barCords.v + v33d.y * barCords.w;
                    curHeight = math.select(curHeight, newHeight, newHeight > curHeight);
                }
            }
        }
        return curHeight;
    }
    BarycentricCoordinates GetBarycentricCoordinatesForEachVectorInTheOrderUVW(float2 a, float2 b, float2 c, float2 p)
    {
        float2 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);
        float d20 = math.dot(v2, v0);
        float d21 = math.dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return new BarycentricCoordinates()
        {
            v = v,
            u = u,
            w = w,
        };
    }
}