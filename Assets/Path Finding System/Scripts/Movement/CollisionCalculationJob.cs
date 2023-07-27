using System.Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;


//ONLY WORKS FOR AGENTS WITH OFFSET 0!
public struct CollisionCalculationJob : IJobParallelForTransform
{
    public float DeltaTime;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float TileSize;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementData;
    [ReadOnly] public NativeArray<int> TileToWallObject;
    [ReadOnly] public NativeList<float2> VertexSequence;
    [ReadOnly] public NativeList<WallObject> WallObjectList;
    [ReadOnly] public NativeList<Direction> EdgeDirections;
    public NativeArray<float2> AgentDirections;
    public void Execute(int index, TransformAccess transform)
    {
        //FOR POSITION
        float3 agentPos = AgentMovementData[index].Position;
        float2 agentPos2d = new float2(agentPos.x, agentPos.z);
        int2 agentIndex = new int2((int)math.floor(agentPos2d.x / TileSize), (int)math.floor(agentPos2d.y / TileSize));
        int agentIndex1d = agentIndex.y * FieldColAmount + agentIndex.x;
        NativeList<int> wallIndiciesAround = GetWallObjectsAround(agentIndex1d);
        NativeList<Edge> collidingEdges = new NativeList<Edge>(Allocator.Temp);
        NativeList<float2> seperationForces = new NativeList<float2>(Allocator.Temp);
        for (int j = 0; j < wallIndiciesAround.Length; j++)
        {
            WallObject wall = WallObjectList[wallIndiciesAround[j]];
            collidingEdges.Clear();
            CheckCollisionsForce(wall, agentPos2d, AgentMovementData[index].Radius, collidingEdges, seperationForces);
        }
        float2 sum = 0;
        if (seperationForces.Length != 0)
        {
            sum = seperationForces[0];
            transform.position = transform.position + new Vector3(sum.x, 0f, sum.y);
        }
        

        //FOR DIRECTION
        float2 dir2d = AgentDirections[index];
        float3 dest3d = transform.position + (new Vector3(dir2d.x, 0f, dir2d.y) * DeltaTime * AgentMovementData[index].Speed);
        float2 dest2d = new float2(dest3d.x, dest3d.z);
        int2 destIndex = new int2((int)math.floor(dest2d.x / TileSize), (int)math.floor(dest2d.y / TileSize));
        int destIndex1d = destIndex.y * FieldColAmount + destIndex.x;
        NativeList<int> wallIndiciesAroundDest = GetWallObjectsAround(destIndex1d);
        NativeList<Edge> collidingEdgesDest = new NativeList<Edge>(Allocator.Temp);
        NativeList<float2> seperationForcesDest = new NativeList<float2>(Allocator.Temp);
        for (int j = 0; j < wallIndiciesAroundDest.Length; j++)
        {
            WallObject wall = WallObjectList[wallIndiciesAroundDest[j]];
            collidingEdgesDest.Clear();
            CheckCollisionsForce(wall, dest2d, AgentMovementData[index].Radius, collidingEdgesDest, seperationForcesDest);
        }
        if (seperationForcesDest.Length != 0)
        {
            sum = seperationForcesDest[0];
            dest2d += sum;
            agentPos2d = new float2(transform.position.x, transform.position.z);
            float2 newDir2d = math.normalize(dest2d - agentPos2d);
            AgentDirections[index] = newDir2d;
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
    bool CheckCollisions(WallObject wallObj, float2 point, float agentRadius, NativeList<Edge> collidingEdgesOut, NativeList<float2> seperationDirections)
    {
        bool isColliding = false;

        NativeSlice<float2> vertexSequence = GetVerteciesOf(wallObj);
        NativeSlice<Direction> edgeDirections = GetEdgeDirectionsOf(wallObj);
        for(int i = 0; i < vertexSequence.Length - 1; i++)
        {
            Edge curEdge = new Edge()
            {
                p1 = vertexSequence[i],
                p2 = vertexSequence[i + 1],
                dir = edgeDirections[i],
            };
            float2 seperationForce;
            if (IsColliding(curEdge, out seperationForce))
            {
                collidingEdgesOut.Add(curEdge); 
                seperationDirections.Add(seperationForce); 
                isColliding = true;
            }
        }
        return isColliding;

        bool IsColliding(Edge edge, out float2 seperationDirection)
        {
            if (edge.p1.y == edge.p2.y) //M = 0
            {
                float2 rh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
                float2 lh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
                bool isOutside = (edge.dir == Direction.N && point.y < lh.y) || (edge.dir == Direction.S && point.y > lh.y);
                bool isColliding;
                if (point.x < rh.x && point.x > lh.x)
                {
                    float yDistance = lh.y - point.y;
                    float2 intersection = point + new float2(0, yDistance);
                    seperationDirection = math.normalize(point - intersection);
                    isColliding = math.abs(yDistance) <= agentRadius;
                    return isColliding && isOutside;
                }
                float lhDistance = math.distance(lh, point);
                float rhDistance = math.distance(rh, point);
                if (rhDistance < lhDistance)
                {
                    isColliding = rhDistance <= agentRadius;
                    seperationDirection = math.normalize(point -rh);
                    return isColliding && isOutside;
                }
                else
                {
                    isColliding = lhDistance <= agentRadius;
                    seperationDirection = math.normalize(point - lh);
                    return isColliding && isOutside;
                }
            }
            else if (edge.p1.x == edge.p2.x) //M = INFINITY
            {
                float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
                float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
                bool isOutside = (edge.dir == Direction.E && point.x < down.x) || (edge.dir == Direction.W && point.x > down.x);
                bool isColliding;
                if (point.y < up.y && point.y > down.y)
                {
                    float xDistance = down.x - point.x;
                    float2 intersection = point + new float2(xDistance, 0);
                    seperationDirection = math.normalize(point - intersection);
                    isColliding = math.abs(xDistance) <= agentRadius;
                    return isColliding && isOutside;
                }
                float upDistance = math.distance(down, point);
                float downDistance = math.distance(up, point);
                if (downDistance < upDistance)
                {
                    isColliding = downDistance <= agentRadius;
                    seperationDirection = math.normalize(point - up);
                    return isColliding && isOutside;
                }
                else
                {
                    isColliding = upDistance <= agentRadius;
                    seperationDirection = math.normalize(point - down);
                    return isColliding && isOutside;
                }
            }
            seperationDirection = 0;
            return false;
        }
    }
    bool CheckCollisionsForce(WallObject wallObj, float2 point, float agentRadius, NativeList<Edge> collidingEdgesOut, NativeList<float2> seperationForces)
    {
        bool isColliding = false;

        NativeSlice<float2> vertexSequence = GetVerteciesOf(wallObj);
        NativeSlice<Direction> edgeDirections = GetEdgeDirectionsOf(wallObj);
        for (int i = 0; i < vertexSequence.Length - 1; i++)
        {
            Edge curEdge = new Edge()
            {
                p1 = vertexSequence[i],
                p2 = vertexSequence[i + 1],
                dir = edgeDirections[i],
            };
            float2 seperationForce;
            if (IsColliding(curEdge, out seperationForce))
            {
                collidingEdgesOut.Add(curEdge);
                seperationForces.Add(seperationForce);
                isColliding = true;
            }
        }
        return isColliding;

        bool IsColliding(Edge edge, out float2 seperationForce)
        {
            if (edge.p1.y == edge.p2.y) //M = 0
            {
                float2 rh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
                float2 lh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
                bool isOutside = (edge.dir == Direction.N && point.y < lh.y) || (edge.dir == Direction.S && point.y > lh.y);
                bool isColliding;
                if (point.x < rh.x && point.x > lh.x)
                {
                    float yDistance = lh.y - point.y;
                    float2 intersection = point + new float2(0, yDistance);
                    seperationForce = math.normalize(point - intersection) * (agentRadius - math.abs(yDistance));
                    isColliding = math.abs(yDistance) <= agentRadius;
                    return isColliding && isOutside;
                }
                float lhDistance = math.distance(lh, point);
                float rhDistance = math.distance(rh, point);
                if (rhDistance < lhDistance)
                {
                    isColliding = rhDistance <= agentRadius;
                    seperationForce = math.normalize(point - rh) * (agentRadius - math.abs(rhDistance));
                    return isColliding && isOutside;
                }
                else
                {
                    isColliding = lhDistance <= agentRadius;
                    seperationForce = math.normalize(point - lh) * (agentRadius - math.abs(lhDistance));
                    return isColliding && isOutside;
                }
            }
            else if (edge.p1.x == edge.p2.x) //M = INFINITY
            {
                float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
                float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
                bool isOutside = (edge.dir == Direction.E && point.x < down.x) || (edge.dir == Direction.W && point.x > down.x);
                bool isColliding;
                if (point.y < up.y && point.y > down.y)
                {
                    float xDistance = down.x - point.x;
                    float2 intersection = point + new float2(xDistance, 0);
                    seperationForce = math.normalize(point - intersection) * (agentRadius - math.abs(xDistance));
                    isColliding = math.abs(xDistance) <= agentRadius;
                    return isColliding && isOutside;
                }
                float upDistance = math.distance(down, point);
                float downDistance = math.distance(up, point);
                if (downDistance < upDistance)
                {
                    isColliding = downDistance <= agentRadius;
                    seperationForce = math.normalize(point - up) * (agentRadius - math.abs(downDistance));
                    return isColliding && isOutside;
                }
                else
                {
                    isColliding = upDistance <= agentRadius;
                    seperationForce = math.normalize(point - down) * (agentRadius - math.abs(upDistance));
                    return isColliding && isOutside;
                }
            }
            seperationForce = 0;
            return false;
        }
    }
}

////NORMAL
//    float2 edgelh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
//    float2 edgerh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
//    float edgeM = (edgerh.y - edgelh.y) / (edgerh.x - edgelh.x);
//    float edgeC = edgelh.y / (edgeM * edgelh.x);
//    float perpM = -edgeM;
//    float perpC = point.y / (perpM * point.x);
//    float intersectX = (perpC - perpM) / 2 * edgeM;
//    float intersectY = intersectX * edgeM + edgeC;
//    float2 intersection = new float2(intersectX, intersectY);
//    //THATS JUST A PLACE HOLDER! YOU NEED TO THINK A SOLUTION
//    seperationDirection = new float2();
//    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
//    bool isColliding = math.distance(point, edgelh) <= agentRadius || math.distance(point, edgerh) <= agentRadius || math.distance(intersection, point) <= agentRadius;
//    return isColliding;