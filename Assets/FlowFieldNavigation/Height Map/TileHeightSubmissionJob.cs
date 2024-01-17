using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct TileHeightSubmissionJob : IJob
{
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;
    [ReadOnly] public NativeArray<float3> Verticies;
    [ReadOnly] public NativeArray<int> Triangles;
    public NativeList<int> TileTrianglePointers;
    public NativeArray<TileTriangleSpan> TileTrianglePointerSpans;

    public void Execute()
    {
        for(int r = 0; r < FieldRowAmount; r++)
        {
            for(int c = 0; c < FieldColAmount; c++)
            {
                float2 tileMins = new float2(c * TileSize, r * TileSize);
                float2 tileMaxes = new float2(tileMins.x + TileSize, tileMins.y + TileSize);
                int tileTrianglePointerStartIndex = TileTrianglePointers.Length;
                int tileTrianglePointerLength = 0;
                for (int i = 0; i < Triangles.Length; i += 3)
                {
                    int v1Index = Triangles[i];
                    int v2Index = Triangles[i + 1];
                    int v3Index = Triangles[i + 2];

                    float3 v13d = Verticies[v1Index];
                    float3 v23d = Verticies[v2Index];
                    float3 v33d = Verticies[v3Index];
                    float2 v1 = new float2(v13d.x, v13d.z);
                    float2 v2 = new float2(v23d.x, v23d.z);
                    float2 v3 = new float2(v33d.x, v33d.z);

                    //Does triangle have any point inside tile
                    bool2 v1AboveMin = v1 >= tileMins;
                    bool2 v1BelowMax = v1 <= tileMaxes;
                    bool2 v2AboveMin = v2 >= tileMins;
                    bool2 v2BelowMax = v2 <= tileMaxes;
                    bool2 v3AboveMin = v3 >= tileMins;
                    bool2 v3BelowMax = v3 <= tileMaxes;
                    bool2 v1result = (v1AboveMin & v1BelowMax);
                    bool2 v2result = (v2AboveMin & v2BelowMax);
                    bool2 v3result = (v3AboveMin & v3BelowMax);
                    bool hasPointWithinTile = (v1result.x && v1result.y) || (v2result.x && v2result.y) || (v3result.x && v3result.y);
                    if (hasPointWithinTile)
                    {
                        TileTrianglePointers.Add(i);
                        tileTrianglePointerLength++;
                        continue;
                    }

                    //Does triangle intersect with tile
                    float2x2 line1 = new float2x2(v1, v2);
                    float2x2 line2 = new float2x2(v2, v3);
                    float2x2 line3 = new float2x2(v3, v1);
                    bool xMinIntersectsLine1 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line1.c0, line1.c1);
                    bool xMaxIntersectsLine1 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line1.c0, line1.c1);
                    bool xMinIntersectsLine2 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line2.c0, line2.c1);
                    bool xMaxIntersectsLine2 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line2.c0, line2.c1);
                    bool xMinIntersectsLine3 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line3.c0, line3.c1);
                    bool xMaxIntersectsLine3 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line3.c0, line3.c1);

                    bool yMinIntersectsLine1 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line1.c0, line1.c1);
                    bool yMaxIntersectsLine1 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line1.c0, line1.c1);
                    bool yMinIntersectsLine2 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line2.c0, line2.c1);
                    bool yMaxIntersectsLine2 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line2.c0, line2.c1);
                    bool yMinIntersectsLine3 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line3.c0, line3.c1);
                    bool yMaxIntersectsLine3 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line3.c0, line3.c1);

                    bool intersectsX = xMinIntersectsLine1 || xMaxIntersectsLine1 || xMinIntersectsLine2 || xMaxIntersectsLine2 || xMinIntersectsLine3 || xMaxIntersectsLine3;
                    bool intersectsY = yMinIntersectsLine1 || yMaxIntersectsLine1 || yMinIntersectsLine2 || yMaxIntersectsLine2 || yMinIntersectsLine3 || yMaxIntersectsLine3;
                    if (intersectsX || intersectsY)
                    {
                        TileTrianglePointers.Add(i);
                        tileTrianglePointerLength++;
                        continue;
                    }
                    if (IsPointInsideTriangle(tileMins, v1, v2, v3))
                    {
                        TileTrianglePointers.Add(i);
                        tileTrianglePointerLength++;
                        continue;
                    }
                }
                TileTriangleSpan newSpan = new TileTriangleSpan()
                {
                    TrianglePointerStartIndex = tileTrianglePointerStartIndex,
                    TrianglePointerCount = tileTrianglePointerLength,
                };
                TileTrianglePointerSpans[r * FieldColAmount + c] = newSpan;
            }
        }
    }
    bool DoesIntersectAtX(float xToCheck, float yMin, float yMax, float2 v1, float2 v2)
    {
        float2 vLeft = math.select(v2, v1, v1.x < v2.x);
        float2 vRight = math.select(v1, v2, v1.x < v2.x);
        if (xToCheck <= vLeft.x || xToCheck >= vRight.x) { return false; }

        float t = (xToCheck - vLeft.x) / (vRight.x - vLeft.x);
        float y = vLeft.y + (vRight.y - vLeft.y) * t;
        return y > yMin && y < yMax;
    }
    bool DoesIntersectAtY(float yToCheck, float xmin, float xmax, float2 v1, float2 v2)
    {
        float2 vDown = math.select(v2, v1, v1.y < v2.y);
        float2 vUp = math.select(v1, v2, v1.y < v2.y);
        if (yToCheck <= vDown.y || yToCheck >= vUp.y) { return false; }

        float t = (yToCheck - vDown.y) / (vUp.y - vDown.y);
        float x = vDown.x + (vUp.x - vDown.x) * t;
        return x > xmin && x < xmax;
    }
    bool IsPointInsideTriangle(float2 point, float2 v1, float2 v2, float2 v3)
    {
        float trigMinX = math.min(math.min(v1.x, v2.x), v3.x);
        float trigMaxX = math.max(math.max(v1.x, v2.x), v3.x);
        if (point.x < trigMinX || point.x > trigMaxX) { return false; }

        float2x2 line1 = new float2x2()
        {
            c0 = math.select(v2, v1, v1.x < v2.x),
            c1 = math.select(v1, v2, v1.x < v2.x),
        };
        bool line1ContainsX = point.x >= line1.c0.x && point.x <= line1.c1.x;
        float2x2 line2 = new float2x2()
        {
            c0 = math.select(v3, v2, v2.x < v3.x),
            c1 = math.select(v2, v3, v2.x < v3.x),
        };
        bool line2ContainsX = point.x >= line2.c0.x && point.x <= line2.c1.x;
        float2x2 line3 = new float2x2()
        {
            c0 = math.select(v1, v3, v3.x < v1.x),
            c1 = math.select(v3, v1, v3.x < v1.x),
        };
        bool line3ContainsX = point.x >= line3.c0.x && point.x <= line3.c1.x;

        float tLine1 = (point.x - line1.c0.x) / (line1.c1.x - line1.c0.x);
        float tLine2 = (point.x - line2.c0.x) / (line2.c1.x - line2.c0.x);
        float tLine3 = (point.x - line3.c0.x) / (line3.c1.x - line3.c0.x);
        float yLine1 = line1.c0.y + (line1.c1.y - line1.c0.y) * tLine1;
        float yLine2 = line2.c0.y + (line2.c1.y - line2.c0.y) * tLine2;
        float yLine3 = line3.c0.y + (line3.c1.y - line3.c0.y) * tLine3;

        float yMin = float.MaxValue;
        float yMax = float.MinValue;
        yMin = math.select(yMin, yLine1, yLine1 < yMin && line1ContainsX);
        yMin = math.select(yMin, yLine2, yLine2 < yMin && line2ContainsX);
        yMin = math.select(yMin, yLine3, yLine3 < yMin && line3ContainsX);
        yMax = math.select(yMax, yLine1, yLine1 > yMax && line1ContainsX);
        yMax = math.select(yMax, yLine2, yLine2 > yMax && line2ContainsX);
        yMax = math.select(yMax, yLine3, yLine3 > yMax && line3ContainsX);
        return point.y <= yMax && point.y >= yMin;
    }
}
