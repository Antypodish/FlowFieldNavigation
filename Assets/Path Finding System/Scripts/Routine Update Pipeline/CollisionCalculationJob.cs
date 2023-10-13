using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;


//ONLY WORKS FOR AGENTS WITH OFFSET 0!
[BurstCompile]
public struct CollisionCalculationJob : IJobParallelFor
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
    [WriteOnly] public NativeArray<float2> AgentPositionChangeBuffer;
    public NativeArray<RoutineResult> RoutineResultArray;
    public void Execute(int index)
    {
        //FOR POSITION
        float3 agentPos = AgentMovementData[index].Position;
        float2 agentPos2d = new float2(agentPos.x, agentPos.z);
        int2 agentIndex = new int2((int)math.floor(agentPos2d.x / TileSize), (int)math.floor(agentPos2d.y / TileSize));
        int agentIndex1d = agentIndex.y * FieldColAmount + agentIndex.x;
        NativeList<int> wallIndiciesAround = GetWallObjectsAround(agentIndex1d);
        if(wallIndiciesAround.Length > 0)
        {
            Collision collisionOutpu;
            if (GetCollision(WallObjectList[wallIndiciesAround[0]], agentPos2d, AgentMovementData[index].Radius, out collisionOutpu))
            {
                float2 seperationForce = SolveCollision(collisionOutpu, agentPos2d, AgentMovementData[index].Radius);
                AgentPositionChangeBuffer[index] = new float2(seperationForce.x, seperationForce.y);
                agentPos = AgentMovementData[index].Position + new float3(seperationForce.x, 0, seperationForce.y);
                agentPos2d.x = agentPos.x;
                agentPos2d.y = agentPos.z;
            }
        }


        //FOR DIRECTION
        RoutineResult routineResult = RoutineResultArray[index];
        float2 dir2d = routineResult.NewDirection;
        float3 dest3d = agentPos + (new float3(dir2d.x, 0f, dir2d.y) * DeltaTime * AgentMovementData[index].Speed);
        float2 dest2d = new float2(dest3d.x, dest3d.z);
        int2 destIndex = new int2((int)math.floor(dest2d.x / TileSize), (int)math.floor(dest2d.y / TileSize));
        int destIndex1d = destIndex.y * FieldColAmount + destIndex.x;
        NativeList<int> wallIndiciesAroundDest = GetWallObjectsAround(destIndex1d);
        if (wallIndiciesAroundDest.Length > 0)
        {
            Collision collisionOutput;
            if (GetCollision(WallObjectList[wallIndiciesAroundDest[0]], dest2d, AgentMovementData[index].Radius, out collisionOutput))
            {
                float2 seperationForce = SolveCollision(collisionOutput, dest2d, AgentMovementData[index].Radius);
                float2 newDest2d = dest2d + seperationForce;
                float2 newDirection = math.select(math.normalize(newDest2d - agentPos2d), 0f, agentPos2d.Equals(newDest2d));
                routineResult.NewDirection = newDirection;
                RoutineResultArray[index] = routineResult;
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
                float2 seperationDirection = math.select(new float2(0, 1), new float2(0, -1), edge.dir == Direction.N);
                if (point.x < rh.x && point.x > lh.x)
                {
                    float yDistance = lh.y - point.y;
                    seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(yDistance)) : seperationDirection * (agentRadius + math.abs(yDistance));
                    isColliding = math.abs(yDistance) <= agentRadius;
                    return isColliding;
                }
                float lhDistance = math.distance(lh, point);
                float rhDistance = math.distance(rh, point);
                if (rhDistance < lhDistance)
                {
                    isColliding = rhDistance <= agentRadius;
                    float2 cornerSeperationDirection = isOutside ? math.normalize(point - rh) : math.normalize(rh - point);
                    cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                    seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(rhDistance)) : cornerSeperationDirection * (agentRadius + math.abs(rhDistance));
                    return isColliding;
                }
                else
                {
                    isColliding = lhDistance <= agentRadius;
                    float2 cornerSeperationDirection = isOutside ? math.normalize(point - lh) : math.normalize(lh - point);
                    cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                    seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(lhDistance)) : cornerSeperationDirection * (agentRadius + math.abs(lhDistance));
                    return isColliding;
                }
            }
            else if (edge.p1.x == edge.p2.x) //M = INFINITY
            {
                float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
                float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
                bool isOutside = (edge.dir == Direction.E && point.x < down.x) || (edge.dir == Direction.W && point.x > down.x);
                bool isColliding;
                float2 seperationDirection = math.select(new float2(1, 0), new float2(-1, 0), edge.dir == Direction.E);
                if (point.y < up.y && point.y > down.y)
                {
                    float xDistance = down.x - point.x;
                    float2 intersection = point + new float2(xDistance, 0);
                    seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(xDistance)) : seperationDirection * (agentRadius + math.abs(xDistance));
                    isColliding = math.abs(xDistance) <= agentRadius;
                    return isColliding;
                }
                float upDistance = math.distance(down, point);
                float downDistance = math.distance(up, point);
                if (downDistance < upDistance)
                {
                    isColliding = downDistance <= agentRadius;
                    seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(downDistance)) : seperationDirection * (agentRadius + math.abs(downDistance));
                    return isColliding;
                }
                else
                {
                    isColliding = upDistance <= agentRadius;
                    seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(upDistance)) : seperationDirection * (agentRadius + math.abs(upDistance));
                    return isColliding;
                }
            }
            seperationForce = 0;
            return false;
        }
    }
    bool GetCollision(WallObject wallObj, float2 point, float agentRadius, out Collision collision)
    {
        collision = new Collision()
        {
            edge = new Edge()
            {
                dir = Direction.None,
                p1 = 0,
                p2 = 0,
            },
            distance = float.MaxValue,
        };
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
            float colDistance = 0;
            if (IsColliding(curEdge, out colDistance))
            {
                if(colDistance < collision.distance)
                {
                    collision.edge = curEdge;
                    collision.distance = colDistance;
                }
                isColliding = true;
            }
        }
        return isColliding;

        bool IsColliding(Edge edge, out float colDistance)
        {
            if (edge.p1.y == edge.p2.y) //M = 0
            {
                float2 rh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
                float2 lh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
                bool isOutside = (edge.dir == Direction.N && point.y < lh.y) || (edge.dir == Direction.S && point.y > lh.y);
                float2 seperationDirection = math.select(new float2(0, 1), new float2(0, -1), edge.dir == Direction.N);
                if (point.x < rh.x && point.x > lh.x)
                {
                    float yDistance = math.abs(lh.y - point.y);
                    colDistance = yDistance;
                    return yDistance <= agentRadius;
                }
                float lhDistance = math.distance(lh, point);
                float rhDistance = math.distance(rh, point);
                if (rhDistance < lhDistance)
                {
                    colDistance = rhDistance;
                    return rhDistance <= agentRadius;
                }
                else
                {
                    colDistance = lhDistance;
                    return lhDistance <= agentRadius;
                }
            }
            else if (edge.p1.x == edge.p2.x) //M = INFINITY
            {
                float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
                float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
                bool isOutside = (edge.dir == Direction.E && point.x < down.x) || (edge.dir == Direction.W && point.x > down.x);
                float2 seperationDirection = math.select(new float2(1, 0), new float2(-1, 0), edge.dir == Direction.E);
                if (point.y < up.y && point.y > down.y)
                {
                    float xDistance = math.abs(down.x - point.x);
                    colDistance = xDistance;
                    return xDistance <= agentRadius;
                }
                float upDistance = math.distance(down, point);
                float downDistance = math.distance(up, point);
                if (downDistance < upDistance)
                {
                    colDistance = downDistance;
                    return downDistance <= agentRadius;
                }
                else
                {
                    colDistance = upDistance;
                    return upDistance <= agentRadius;
                }
            }
            colDistance = float.MaxValue;
            return false;
        }

    }
    float2 SolveCollision(Collision collision, float2 point, float agentRadius)
    {
        Edge edge = collision.edge;
        float2 seperationForce = 0;
        if (edge.p1.y == edge.p2.y) //M = 0
        {
            float2 rh = math.select(edge.p2, edge.p1, edge.p1.x > edge.p2.x);
            float2 lh = math.select(edge.p2, edge.p1, edge.p1.x < edge.p2.x);
            bool isOutside = (edge.dir == Direction.N && point.y < lh.y) || (edge.dir == Direction.S && point.y > lh.y);
            float2 seperationDirection = math.select(new float2(0, 1), new float2(0, -1), edge.dir == Direction.N);
            if (point.x < rh.x && point.x > lh.x)
            {
                float yDistance = lh.y - point.y;
                seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(yDistance)) : seperationDirection * (agentRadius + math.abs(yDistance));
                return seperationForce;
            }
            float lhDistance = math.distance(lh, point);
            float rhDistance = math.distance(rh, point);
            if (rhDistance < lhDistance)
            {
                float2 cornerSeperationDirection = isOutside ? math.normalize(point - rh) : math.normalize(rh - point);
                cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(rhDistance)) : cornerSeperationDirection * (agentRadius + math.abs(rhDistance));
                return seperationForce;
            }
            else
            {
                float2 cornerSeperationDirection = isOutside ? math.normalize(point - lh) : math.normalize(lh - point);
                cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(lhDistance)) : cornerSeperationDirection * (agentRadius + math.abs(lhDistance));
                return seperationForce;
            }
        }
        else if (edge.p1.x == edge.p2.x) //M = INFINITY
        {
            float2 up = math.select(edge.p2, edge.p1, edge.p1.y > edge.p2.y);
            float2 down = math.select(edge.p2, edge.p1, edge.p1.y < edge.p2.y);
            bool isOutside = (edge.dir == Direction.E && point.x < down.x) || (edge.dir == Direction.W && point.x > down.x);
            float2 seperationDirection = math.select(new float2(1, 0), new float2(-1, 0), edge.dir == Direction.E);
            if (point.y < up.y && point.y > down.y)
            {
                float xDistance = down.x - point.x;
                float2 intersection = point + new float2(xDistance, 0);
                seperationForce = isOutside ? seperationDirection * (agentRadius - math.abs(xDistance)) : seperationDirection * (agentRadius + math.abs(xDistance));
                return seperationForce;
            }
            float upDistance = math.distance(down, point);
            float downDistance = math.distance(up, point);
            if (downDistance < upDistance)
            {
                float2 cornerSeperationDirection = isOutside ? math.normalize(point - down) : math.normalize(down - point);
                cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(downDistance)) : cornerSeperationDirection * (agentRadius + math.abs(downDistance));
                return seperationForce;
            }
            else
            {
                float2 cornerSeperationDirection = isOutside ? math.normalize(point - up) : math.normalize(up - point);
                cornerSeperationDirection = math.select(cornerSeperationDirection, seperationDirection, cornerSeperationDirection.Equals(0));
                seperationForce = isOutside ? cornerSeperationDirection * (agentRadius - math.abs(upDistance)) : cornerSeperationDirection * (agentRadius + math.abs(upDistance));
                return seperationForce;
            }
        }
        return seperationForce;
    }
}
public struct Collision
{
    public Edge edge;
    public float distance;
}