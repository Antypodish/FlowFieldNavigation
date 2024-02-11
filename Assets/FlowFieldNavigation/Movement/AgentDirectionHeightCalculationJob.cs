using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Numerics;

[BurstCompile]
internal struct AgentDirectionHeightCalculationJob : IJobParallelFor
{
    [ReadOnly] internal TriangleSpatialHashGrid TriangleSpatialHashGrid;
    [ReadOnly] internal NativeArray<float3> Verticies;
    [ReadOnly] internal NativeArray<AgentMovementData> AgentMovementDataArray;
    internal NativeArray<RoutineResult> RoutineResultArray;
    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        RoutineResult routineResult = RoutineResultArray[index];
        float3 agentPos3 = agentData.Position;
        float2 agentPos2 = new float2(agentPos3.x, agentPos3.z);
        float2 agentDir2 = routineResult.NewDirection;
        float2 agentDir2Small = agentDir2 * 0.001f;
        float2 posToCheck = agentPos2 + agentDir2Small;
        float lastHeight = float.MinValue;
        for (int i = 0; i < TriangleSpatialHashGrid.GetGridCount(); i++)
        {
            bool succesfull = TriangleSpatialHashGrid.TryGetIterator(posToCheck, i, out TriangleSpatialHashGridIterator triangleGridIterator);
            if (!succesfull) { lastHeight = 0; break; }
            while (triangleGridIterator.HasNext())
            {
                NativeSlice<int> triangles = triangleGridIterator.GetNextRow();
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    int v1Index = triangles[j];
                    int v2Index = triangles[j + 1];
                    int v3Index = triangles[j + 2];
                    float3 v13d = Verticies[v1Index];
                    float3 v23d = Verticies[v2Index];
                    float3 v33d = Verticies[v3Index];
                    float2 v1 = new float2(v13d.x, v13d.z);
                    float2 v2 = new float2(v23d.x, v23d.z);
                    float2 v3 = new float2(v33d.x, v33d.z);

                    BarycentricCoordinates barCords = GetBarycentricCoordinatesForEachVectorInTheOrderUVW(v1, v2, v3, posToCheck);
                    if (barCords.u <= 0 || barCords.w <= 0 || barCords.v <= 0) { continue; }
                    float newHeight = v13d.y * barCords.u + v23d.y * barCords.v + v33d.y * barCords.w + agentData.LandOffset;
                    bool isHigher = newHeight > lastHeight;
                    lastHeight = math.select(lastHeight, newHeight, isHigher);
                }
            }
        }
        lastHeight = math.select(lastHeight, agentPos3.y, lastHeight == float.MinValue);
        float3 posToCheck3 = new float3(posToCheck.x, lastHeight, posToCheck.y);
        float3 newDir3 = posToCheck3 - agentPos3;
        newDir3 = math.normalizesafe(newDir3) * math.length(agentDir2);
        routineResult.NewDirection3 = newDir3;
        RoutineResultArray[index] = routineResult;

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

