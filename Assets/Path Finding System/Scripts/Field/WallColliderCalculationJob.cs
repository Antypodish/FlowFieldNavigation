using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.ShaderGraph.Internal;

[BurstCompile]
public struct WallColliderCalculationJob : IJob
{
    public NativeArray<byte> Costs;
    public NativeArray<Edge> TileEdges;
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;
    public void Execute()
    {
        SetEdgesForEachTile();

    }
    NativeArray<Edge> SetEdgesForEachTile()
    {
        float halfTileSize = TileSize / 2;
        int fieldTileAmount = FieldColAmount * FieldRowAmount;
        for(int i = 0; i < Costs.Length; i++)
        {
            if (Costs[i]!= byte.MaxValue) { continue; }
            int2 i2d = new int2(i % FieldColAmount, i / FieldColAmount);
            float2 tilePos = new float2(TileSize / 2 + i2d.x * TileSize, TileSize / 2 + i2d.y * TileSize);

            int tileEdgeIndex = i * 4;

            int n1d = i + FieldColAmount;
            int e1d = i + 1;
            int s1d = i - FieldColAmount;
            int w1d = i - 1;

            bool nOverflow = n1d >= fieldTileAmount;
            bool eOverflow = (e1d % FieldColAmount) == 0;
            bool sOverflow = s1d < 0;
            bool wOverflow = (i % FieldColAmount) == 0;

            if (!nOverflow)
            {
                byte nCost = Costs[n1d];
                if(nCost == 1)
                {
                    float2 p1 = tilePos + new float2(-halfTileSize, halfTileSize);
                    float2 p2 = tilePos + new float2(halfTileSize, halfTileSize);
                    TileEdges[tileEdgeIndex] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.S
                    };
                }
            }
            if (!eOverflow)
            {
                byte eCost = Costs[e1d];
                if (eCost == 1)
                {
                    float2 p1 = tilePos + new float2(halfTileSize, halfTileSize);
                    float2 p2 = tilePos + new float2(halfTileSize, -halfTileSize);
                    TileEdges[tileEdgeIndex + 1] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.W
                    };
                }
            }
            if (!sOverflow)
            {
                byte sCost = Costs[s1d];
                if (sCost == 1)
                {
                    float2 p1 = tilePos + new float2(halfTileSize, -halfTileSize);
                    float2 p2 = tilePos + new float2(-halfTileSize, -halfTileSize);
                    TileEdges[tileEdgeIndex + 2] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.N
                    };
                }
            }
            if (!wOverflow)
            {
                byte wCost = Costs[w1d];
                if (wCost == 1)
                {
                    float2 p1 = tilePos + new float2(-halfTileSize, -halfTileSize);
                    float2 p2 = tilePos + new float2(-halfTileSize, halfTileSize);
                    TileEdges[tileEdgeIndex + 3] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.S
                    };
                }
            }


        }
        return TileEdges;
    }
}
public struct Edge
{
    public float2 p1;
    public float2 p2;
    public WallDirection dir;
}
public enum WallDirection : byte
{
    None,
    N,
    E,
    S,
    W,
}