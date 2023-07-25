using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;


//ONLY WORKS FOR AGENTS WITH OFFSET 0!
public struct CollisionCalculationJob : IJob
{
    public int FieldColAmount;
    public int FieldRowAmount;
    public float TileSize;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementData;
    [ReadOnly] public NativeArray<int> TileToWallObject;
    [ReadOnly] public NativeList<float2> VertexSequence;
    [ReadOnly] public NativeList<WallObject> WallObjectList;
    [ReadOnly] public NativeList<Direction> EdgeDirections;
    public void Execute()
    {
        for(int i = 0; i < AgentMovementData.Length; i++)
        {
            float3 agentPos = AgentMovementData[i].Position;
            float2 agentPos2d = new float2(agentPos.x, agentPos.z);
            int2 agentIndex = new int2((int)math.floor(agentPos2d.x / TileSize), (int) math.floor(agentPos2d.y / TileSize));
            int agentIndex1d = agentIndex.y * FieldColAmount + agentIndex.x;
            NativeList<int> wallIndiciesAround = GetWallObjectsAround(agentIndex1d);
            for(int j = 0; j < wallIndiciesAround.Length; j++)
            {
                NativeSlice<float2> vertexSequence = GetVerteciesOf(WallObjectList[wallIndiciesAround[j]]);
                NativeSlice<Direction> edgeDirections = GetEdgeDirectionsOf(WallObjectList[wallIndiciesAround[j]]);
                NativeList<Edge> collidingEdges = GetCollidingEdges(vertexSequence, edgeDirections, agentPos2d, AgentMovementData[i].Radius);
            }
        }
    }

    NativeList<int> GetWallObjectsAround(int index)
    {
        NativeList<int> walls = new NativeList<int>(Allocator.Temp);
        int fieldTileAmount = FieldColAmount * FieldRowAmount; 
        int n = index + FieldColAmount;
        int e = index + 1;
        int s = index - FieldColAmount;
        int w = index - 1;
        int ne = n + 1;
        int se = s + 1;
        int sw = s - 1;
        int nw = n - 1;
        bool nOverflow = n >= fieldTileAmount;
        bool eOverflow = (e % FieldColAmount) == 0;
        bool sOverflow = s < 0;
        bool wOverflow = (index % FieldColAmount) == 0;

        int curWallIndex = TileToWallObject[index];
        if (curWallIndex != 0 && !walls.Contains(curWallIndex)) { walls.Add(curWallIndex); }
        if (!nOverflow)
        {
            int wallIndex = TileToWallObject[n];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!eOverflow)
        {
            int wallIndex = TileToWallObject[e];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!sOverflow)
        {
            int wallIndex = TileToWallObject[s];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!wOverflow)
        {
            int wallIndex = TileToWallObject[w];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!(nOverflow || eOverflow))
        {
            int wallIndex = TileToWallObject[ne];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!(sOverflow || eOverflow))
        {
            int wallIndex = TileToWallObject[se];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!(sOverflow || wOverflow))
        {
            int wallIndex = TileToWallObject[sw];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        if (!(nOverflow || wOverflow))
        {
            int wallIndex = TileToWallObject[nw];
            if (wallIndex != 0 && !walls.Contains(wallIndex)) { walls.Add(wallIndex); }
        }
        return walls;
    }
    NativeSlice<float2> GetVerteciesOf(WallObject wall) => new NativeSlice<float2>(VertexSequence, wall.vertexStart, wall.vertexLength);
    NativeSlice<Direction> GetEdgeDirectionsOf(WallObject wall) => new NativeSlice<Direction>(EdgeDirections, wall.vertexStart, wall.vertexLength - 1);
    NativeList<Edge> GetCollidingEdges(NativeSlice<float2> vertexSequence, NativeSlice<Direction> edgeDirections, float2 agentPos, float agentRadius)
    {
        NativeList<Edge> edges = new NativeList<Edge>(Allocator.Temp);
        for(int i = 0; i < vertexSequence.Length - 1; i++)
        {
            Edge curEdge = new Edge()
            {
                p1 = vertexSequence[i],
                p2 = vertexSequence[i + 1],
                dir = edgeDirections[i],
            };
            if (IsColliding(curEdge)) { edges.Add(curEdge); }
        }
        return edges;

        bool IsColliding(Edge edge)
        {
            //M = INFINITY
            if (edge.p1.x == edge.p2.x)
            {
                float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
                float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
                float xDistance = math.abs(agentPos.x - up.x);
                return math.distance(agentPos, up) <= agentRadius || math.distance(agentPos, down) <= agentRadius || (agentPos.y <= up.y && agentPos.y >= down.y && xDistance <= agentRadius);
            }
            //M = 0
            else if(edge.p1.y == edge.p2.y)
            {
                float2 rh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
                float2 lh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
                float yDistance = math.abs(agentPos.y - lh.y);
                return math.distance(agentPos, lh) <= agentRadius || math.distance(agentPos, rh) <= agentRadius || (agentPos.x <= rh.x && agentPos.x >= lh.x && yDistance <= agentRadius);
            }

            //NORMAL
            float2 edgelh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
            float2 edgerh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
            float edgeM = (edgerh.y - edgelh.y) / (edgerh.x - edgelh.x);
            float edgeC = edgelh.y / (edgeM * edgelh.x);
            float perpM = -edgeM;
            float perpC = agentPos.y / (perpM * agentPos.x);
            float intersectX = (perpC - perpM) / 2 * edgeM;
            float intersectY = intersectX * edgeM + edgeC;
            float2 intersection = new float2(intersectX, intersectY);
            return math.distance(agentPos, edgelh) <= agentRadius || math.distance(agentPos, edgerh) <= agentRadius || math.distance(intersection, agentPos) <= agentRadius;
        }
    }
}