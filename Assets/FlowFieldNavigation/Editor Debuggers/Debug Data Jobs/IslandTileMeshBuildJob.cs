using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
internal struct IslandTileMeshBuildJob : IJob
{
    internal float TileSize;
    internal float2 FieldGridStartPos;
    internal int FieldColAmount;
    internal int FieldRowAmount;
    [ReadOnly] internal TriangleSpatialHashGrid TriangleSpatialHashGrid;
    [ReadOnly] internal NativeArray<float3> HeightMeshVerts;
    [ReadOnly] internal NativeSlice<int2> IndiciesToCreateMesh;
    internal NativeList<Vector3> Verts;
    internal NativeList<int> Trigs;

    public void Execute()
    {
        NativeList<float2> tempVers = new NativeList<float2>(Allocator.Temp);
        NativeList<int> tempTrigs = new NativeList<int>(Allocator.Temp);
        for(int i = 0; i < IndiciesToCreateMesh.Length; i++)
        {
            int2 fieldIndex = IndiciesToCreateMesh[i];

            float2 botLeftVert = FlowFieldUtilities.IndexToStartPos(fieldIndex, TileSize, FieldGridStartPos);
            float2 topLeftVert = botLeftVert + new float2(0, TileSize);
            float2 topRightVert = botLeftVert + new float2(TileSize, TileSize);
            float2 botRightVert = botLeftVert + new float2(TileSize, 0);

            int botLeftVertIndex = tempVers.Length;
            int topLeftVertIndex = tempVers.Length + 1;
            int topRightVertIndex = tempVers.Length + 2;
            int botRightVertIndex = tempVers.Length + 3;

            tempVers.Add(botLeftVert);
            tempVers.Add(topLeftVert);
            tempVers.Add(topRightVert);
            tempVers.Add(botRightVert);

            tempTrigs.Add(botLeftVertIndex);
            tempTrigs.Add(topLeftVertIndex);
            tempTrigs.Add(botRightVertIndex);
            tempTrigs.Add(topRightVertIndex);
            tempTrigs.Add(botRightVertIndex); 
            tempTrigs.Add(topLeftVertIndex); 
        }

        //You should optimize mesh. There are sooooo many redundant verticies

        Verts.Length = tempVers.Length;
        Trigs.Length = tempTrigs.Length;
        for(int i = 0; i < tempVers.Length; i++)
        {
            float2 vert = tempVers[i];
            float3 vert3 = new float3(vert.x, GetHeight(vert), vert.y);
            Verts[i] = vert3;
        }
        for(int i = 0; i < tempTrigs.Length; i++)
        {
            Trigs[i] = tempTrigs[i];
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
                    if (barCords.u <= 0 || barCords.w <= 0 || barCords.v <= 0) { continue; }
                    float newHeight = v13d.y * barCords.u + v23d.y * barCords.v + v33d.y * barCords.w;
                    curHeight = math.select(curHeight, newHeight, newHeight > curHeight);
                }
            }
        }
        return math.select(curHeight, 0, curHeight == float.MinValue);
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