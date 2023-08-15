using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.UIElements;

[BurstCompile]
public struct AgentRoutineDataCalculationJob : IJobParallelForTransform
{
    public float TileSize;
    public int FieldColAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentMovementData> AgentMovementData;
    public NativeArray<OutOfFieldStatus> AgentOutOfFieldStatusList;
    [ReadOnly] public UnsafeList<UnsafeList<byte>> CostFields;

    public void Execute(int index, TransformAccess transform)
    {
        Waypoint(index, transform);
    }
    void Normal(int index, TransformAccess transform)
    {/*
        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;
        if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
        {
            data.Flow = 0;
            AgentMovementData[index] = data;
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
            data.Flow = 0;
            AgentMovementData[index] = data;
            return;
        }

        FlowData flow = data.FlowField[sectorMark + local1d];
        switch (flow)
        {
            case FlowData.None:
                data.OutOfFieldFlag = true;
                data.Flow = 0;
                AgentMovementData[index] = data;
                return;
            case FlowData.LOS:
                data.Flow = data.Destination - new float2(data.Position.x, data.Position.z);
                break;
            case FlowData.N:
                data.Flow = new float2(0f, 1f);
                break;
            case FlowData.E:
                data.Flow = new float2(1f, 0f);
                break;
            case FlowData.S:
                data.Flow = new float2(0f, -1f);
                break;
            case FlowData.W:
                data.Flow = new float2(-1f, 0f);
                break;
            case FlowData.NE:
                data.Flow = new float2(1f, 1f);
                break;
            case FlowData.SE:
                data.Flow = new float2(1f, -1f);
                break;
            case FlowData.SW:
                data.Flow = new float2(-1f, -1f);
                break;
            case FlowData.NW:
                data.Flow = new float2(-1f, 1f);
                break;
        }
        data.Flow = math.normalize(data.Flow);
        AgentMovementData[index] = data;*/
    }
    void Waypoint(int index, TransformAccess transform)
    {
        float tileSize = TileSize;
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;


        AgentMovementData data = AgentMovementData[index];
        data.Position = transform.position;


        int2 sector2d = new int2((int)math.floor(data.Position.x / (SectorColAmount * TileSize)), (int)math.floor(data.Position.z / (SectorColAmount * TileSize)));
        int2 general2d = new int2((int)math.floor(data.Position.x / TileSize), (int)math.floor(data.Position.z / TileSize));
        int2 sectorStart2d = sector2d * SectorColAmount;
        int2 local2d = general2d - sectorStart2d;
        int local1d = local2d.y * SectorColAmount + local2d.x;
        int sector1d = sector2d.y * SectorMatrixColAmount + sector2d.x;

        data.Local1d = (ushort)local1d;


        if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving)
        {
            AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
            {
                PathId = data.PathId,
                IsOutOfField = false,
                Sector1d = (ushort) sector1d,
            };
            data.Flow = 0;
            AgentMovementData[index] = data;
            return;
        }
        int sectorMark = data.SectorToPicked[sector1d];
        if (sectorMark == 0)
        {
            AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
            {
                PathId = data.PathId,
                IsOutOfField = true,
                Sector1d = (ushort) sector1d,
            };
            data.Flow = 0;
            AgentMovementData[index] = data;
            return;
        }
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort) sector1d,
        };

        //DATA TABLE
        UnsafeList<byte> costField = CostFields[data.Offset];
        int targetGeneral1d = To1D(PosTo2D(data.Destination, tileSize), fieldColAmount);
        Waypoint currentWaypoint = data.Waypoint;
        int curGeneral1d = To1D(general2d, fieldColAmount);
        float2 curTilePos = IndexToPos(curGeneral1d, tileSize, fieldColAmount);
        float2 agentPos = new float2(data.Position.x, data.Position.z);

        float2 destination = data.Destination;
        //IF UNWALKABLE
        if (costField[curGeneral1d] == byte.MaxValue)
        {
            FlowData startingFlow = GetFlowAt(curGeneral1d, data.FlowField, data.SectorToPicked);
            if(startingFlow == FlowData.None)
            {
                data.Flow = 0;
                data.Waypoint = new Waypoint()
                {
                    position = agentPos,
                    index = curGeneral1d,
                };
                AgentMovementData[index] = data;
                AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
                {
                    PathId = data.PathId,
                    IsOutOfField = false,
                    Sector1d = (ushort) sector1d,
                };
                return;
            }
            else
            {
                curGeneral1d = GetNextIndex(startingFlow, curGeneral1d, fieldColAmount, targetGeneral1d);
            }
        }

        //GET WAYPOINT
        if (curGeneral1d != targetGeneral1d)
        {
            Waypoint newWayp;
            GetNextWaypoint(curGeneral1d, curTilePos, out newWayp, targetGeneral1d, data.Destination, data.FlowField, data.SectorToPicked);
            if (!currentWaypoint.Equals(new Waypoint()))
            {
                currentWaypoint = GetBestWaypoint(currentWaypoint, newWayp, curGeneral1d, targetGeneral1d);
            }
            else
            {
                currentWaypoint = newWayp;
            }
        }

        //CALCULATE FLOW
        float2 flow = math.select(math.normalize(currentWaypoint.position - agentPos), 0, currentWaypoint.position == agentPos);
        data.Flow = flow;
        data.Waypoint = currentWaypoint;
        AgentMovementData[index] = data;
        AgentOutOfFieldStatusList[index] = new OutOfFieldStatus()
        {
            PathId = data.PathId,
            IsOutOfField = false,
            Sector1d = (ushort) sector1d,
        };

        Waypoint GetBestWaypoint(Waypoint oldWaypoint, Waypoint newWaypoint, int sourceIndex, int targetGeneral1d)
        {
            if (oldWaypoint.blockedDirection == 0 || !IsWaypoint(oldWaypoint.index, sourceIndex, targetGeneral1d, out oldWaypoint, out CornerDirections placeholder))
            {
                return newWaypoint;
            }
            float2 sourcePos = IndexToPos(sourceIndex, tileSize, fieldColAmount);
            float oldDist = math.distance(oldWaypoint.position, sourcePos);
            float newDist = math.distance(newWaypoint.position, sourcePos);
            if (oldDist < newDist)
            {
                if (IsInLOS(newWaypoint.position, oldWaypoint, sourcePos))
                {
                    return newWaypoint;
                }
                return oldWaypoint;
            }
            else
            {
                if (IsInLOS(oldWaypoint.position, newWaypoint, sourcePos))
                {
                    return oldWaypoint;
                }
                return newWaypoint;
            }
        }
        FlowData GetFlowAt(int general1d, UnsafeList<FlowData> flowField, UnsafeList<int> sectorToPicked)
        {
            int2 general2d = To2D(general1d, fieldColAmount);
            int2 sector2d = GetSectorIndex(general2d, sectorColAmount);
            int2 sectorStart2d = GetSectorStartIndex(sector2d, sectorColAmount);
            int2 local2d = GetLocalIndex(general2d, sectorStart2d);
            int local1d = To1D(local2d, sectorColAmount);
            int sector1d = To1D(sector2d, sectorMatrixColAmount);

            int flowStartIndex = sectorToPicked[sector1d];
            int flowIndex = flowStartIndex + local1d;
            return flowField[flowIndex];
        }
        void GetNextWaypoint(int source1d, float2 sourcePos, out Waypoint wayp, int targetGeneral1d, float2 targetPos, UnsafeList<FlowData> flowField, UnsafeList<int> sectorToPicked)
        {
            Waypoint lastWaypoint = GetNextWaypointCandidate(source1d, source1d, targetGeneral1d, flowField, sectorToPicked);
            if (lastWaypoint.index == targetGeneral1d)
            {
                wayp = new Waypoint()
                {
                    index = targetGeneral1d,
                    position = targetPos,
                    blockedDirection = 0,
                };
                return;
            }
            Waypoint newWaypoint = GetNextWaypointCandidate(source1d, lastWaypoint.index, targetGeneral1d, flowField, sectorToPicked);

            while (newWaypoint.index != -1 && IsInLOS(newWaypoint.position, lastWaypoint, sourcePos))
            {
                if (newWaypoint.index == targetGeneral1d)
                {
                    wayp = newWaypoint;
                    return;
                }

                lastWaypoint = newWaypoint;
                newWaypoint = GetNextWaypointCandidate(source1d, lastWaypoint.index, targetGeneral1d, flowField, sectorToPicked);
            }
            wayp = lastWaypoint;
            return;

        }
        bool IsInLOS(float2 point, Waypoint wayp, float2 sourcePos)
        {
            float2 wayPos = wayp.position;
            WaypointDirection wayDir = wayp.blockedDirection;
            //M = INFINITY
            if (sourcePos.x == wayPos.x)
            {
                if ((wayDir & WaypointDirection.E) == WaypointDirection.E && point.x > wayPos.x)
                {
                    return false;
                }
                if ((wayDir & WaypointDirection.W) == WaypointDirection.W && point.x < wayPos.x)
                {
                    return false;
                }
                return true;
            }

            //NORMAL
            float2 lh = wayPos.x < sourcePos.x ? wayPos : sourcePos;
            float2 rh = wayPos.x >= sourcePos.x ? wayPos : sourcePos;

            float m = (rh.y - lh.y) / (rh.x - lh.x);

            if (m == 0f)
            {
                if ((wayDir & WaypointDirection.S) == WaypointDirection.S && point.y < wayPos.y)
                {
                    return false;
                }
                if ((wayDir & WaypointDirection.N) == WaypointDirection.N && point.y > wayPos.y)
                {
                    return false;
                }
                return true;
            }

            float c;
            c = lh.y - lh.x * m;
            float pointZOnLine = m * point.x + c;
            if ((wayDir & WaypointDirection.S) == WaypointDirection.S && point.y < pointZOnLine)
            {
                return false;
            }
            if ((wayDir & WaypointDirection.N) == WaypointDirection.N && point.y > pointZOnLine)
            {
                return false;
            }
            return true;
        }
        Waypoint GetNextWaypointCandidate(int source1d, int start1d, int targetIndex1d, UnsafeList<FlowData> flowField, UnsafeList<int> sectorToPicked)
        {
            int cur1d = start1d;
            bool waypointFound = false;
            CornerDirections corners = 0;
            Waypoint wayp = new Waypoint();
            while (!waypointFound)
            {
                FlowData incomingFlow = GetFlowAt(cur1d, flowField, sectorToPicked);
                cur1d = GetNextIndex(incomingFlow, cur1d, fieldColAmount, targetIndex1d);
                if (IsWaypoint(cur1d, source1d, targetIndex1d, out wayp, out corners))   
                {
                    return wayp;
                }
                else if (corners != 0)
                {
                    FlowData outgoindFlow = GetFlowAt(cur1d, flowField, sectorToPicked);
                    if (IsTurningCorner(incomingFlow, outgoindFlow, corners))
                    {
                        return new Waypoint()
                        {
                            index = -1
                        };
                    }
                        
                }
            }
            return wayp;
        }
        bool IsTurningCorner(FlowData incomingFlow, FlowData outgoingFlow, CornerDirections cornerDirections)
        {
            bool isTurningNe = (cornerDirections & CornerDirections.NE) == CornerDirections.NE && ((incomingFlow == FlowData.S && outgoingFlow == FlowData.E) || (incomingFlow == FlowData.W && outgoingFlow == FlowData.N));
            bool isTurningSe = (cornerDirections & CornerDirections.SE) == CornerDirections.SE && ((incomingFlow == FlowData.N && outgoingFlow == FlowData.E) || (incomingFlow == FlowData.W && outgoingFlow == FlowData.S));
            bool isTurningSw = (cornerDirections & CornerDirections.SW) == CornerDirections.SW && ((incomingFlow == FlowData.N && outgoingFlow == FlowData.W) || (incomingFlow == FlowData.E && outgoingFlow == FlowData.S));
            bool isTurningNw = (cornerDirections & CornerDirections.NW) == CornerDirections.NW && ((incomingFlow == FlowData.S && outgoingFlow == FlowData.W) || (incomingFlow == FlowData.E && outgoingFlow == FlowData.N));
            return isTurningNe || isTurningSe || isTurningSw || isTurningNw;
        }
        bool IsWaypoint(int potentialWaypoint1d, int source1d, int targetGeneral1d, out Waypoint wayp, out CornerDirections cornerDirections)
        {
            cornerDirections = 0;
            bool isWaypoint = false;
            wayp = new Waypoint();
            float2 waypPos = IndexToPos(potentialWaypoint1d, tileSize, fieldColAmount);
            wayp.position = waypPos;
            wayp.index = potentialWaypoint1d;
            float2 sourcePos = IndexToPos(source1d, tileSize, fieldColAmount);
            UnsafeList<byte> costs = costField;

            int n = potentialWaypoint1d + fieldColAmount;
            int e = potentialWaypoint1d + 1;
            int s = potentialWaypoint1d - fieldColAmount;
            int w = potentialWaypoint1d - 1;
            int ne = potentialWaypoint1d + fieldColAmount + 1;
            int se = potentialWaypoint1d - fieldColAmount + 1;
            int sw = potentialWaypoint1d - fieldColAmount - 1;
            int nw = potentialWaypoint1d + fieldColAmount - 1;

            byte curCost = costs[potentialWaypoint1d];
            byte nCost = costs[n];
            byte eCost = costs[e];
            byte sCost = costs[s];
            byte wCost = costs[w];
            byte neCost = costs[ne];
            byte seCost = costs[se];
            byte swCost = costs[sw];
            byte nwCost = costs[nw];

            int2 cur2d = To2D(potentialWaypoint1d, fieldColAmount);
            int2 source2d = To2D(source1d, fieldColAmount);
            int2 n2d = To2D(n, fieldColAmount);
            int2 e2d = To2D(e, fieldColAmount);
            int2 s2d = To2D(s, fieldColAmount);
            int2 w2d = To2D(w, fieldColAmount);
            int2 ne2d = To2D(ne, fieldColAmount);
            int2 se2d = To2D(se, fieldColAmount);
            int2 sw2d = To2D(sw, fieldColAmount);
            int2 nw2d = To2D(nw, fieldColAmount);

            int2 curDif = math.abs(cur2d - source2d);
            int2 neDif = math.abs(ne2d - source2d);
            int2 seDif = math.abs(se2d - source2d);
            int2 swDif = math.abs(sw2d - source2d);
            int2 nwDif = math.abs(nw2d - source2d);

            bool mInfinite = waypPos.x == sourcePos.x;

            if (curCost == byte.MaxValue) { return false; }
            if (potentialWaypoint1d == targetGeneral1d) { wayp.position = destination; return true; }
            if (neCost == byte.MaxValue && nCost != byte.MaxValue && eCost != byte.MaxValue)
            {
                int2 change = curDif - neDif;
                int componentChange = change.x * change.y;
                cornerDirections = change.x > 0 && change.y > 0 ? cornerDirections | CornerDirections.NE : cornerDirections;
                if (componentChange < 0)
                {
                    isWaypoint = true;
                    wayp.blockedDirection = mInfinite ? wayp.blockedDirection | WaypointDirection.E : wayp.blockedDirection | WaypointDirection.N;
                }
            }
            if (seCost == byte.MaxValue && sCost != byte.MaxValue && eCost != byte.MaxValue)
            {
                int2 change = curDif - seDif;
                int componentChange = change.x * change.y;
                cornerDirections = change.x > 0 && change.y > 0 ? cornerDirections | CornerDirections.SE : cornerDirections;
                if (componentChange < 0)
                {
                    isWaypoint = true;
                    wayp.blockedDirection = mInfinite ? wayp.blockedDirection | WaypointDirection.E : wayp.blockedDirection | WaypointDirection.S;
                }
            }
            if (swCost == byte.MaxValue && sCost != byte.MaxValue && wCost != byte.MaxValue)
            {
                int2 change = curDif - swDif;
                int componentChange = change.x * change.y;
                cornerDirections = change.x > 0 && change.y > 0 ? cornerDirections | CornerDirections.SW : cornerDirections;
                if (componentChange < 0)
                {
                    isWaypoint = true;
                    wayp.blockedDirection = mInfinite ? wayp.blockedDirection | WaypointDirection.W : wayp.blockedDirection | WaypointDirection.S;
                }
            }
            if (nwCost == byte.MaxValue && nCost != byte.MaxValue && wCost != byte.MaxValue)
            {
                int2 change = curDif - nwDif;
                int componentChange = change.x * change.y;
                cornerDirections = change.x > 0 && change.y > 0 ? cornerDirections | CornerDirections.NW : cornerDirections;
                if (componentChange < 0)
                {
                    isWaypoint = true;
                    wayp.blockedDirection = mInfinite ? wayp.blockedDirection | WaypointDirection.W : wayp.blockedDirection | WaypointDirection.N;
                }
            }

            return isWaypoint;
        }
        int To1D(int2 index2, int colAmount)
        {
            return index2.y * colAmount + index2.x;
        }
        int2 To2D(int index, int colAmount)
        {
            return new int2(index % colAmount, index / colAmount);
        }
        int2 PosTo2D(float2 pos, float tileSize)
        {
            return new int2((int)math.floor(pos.x / tileSize), (int)math.floor(pos.y / tileSize));
        }
        float2 IndexToPos(int general1d, float tileSize, int fieldColAmount)
        {
            int2 general2d = To2D(general1d, fieldColAmount);
            return new float2(general2d.x * tileSize + tileSize / 2, general2d.y * tileSize + tileSize / 2);
        }
        int2 GetSectorIndex(int2 index, int sectorColAmount)
        {
            return new int2(index.x / sectorColAmount, index.y / sectorColAmount);
        }
        int2 GetLocalIndex(int2 index, int2 sectorStartIndex)
        {
            return index - sectorStartIndex;
        }
        int2 GetSectorStartIndex(int2 sectorIndex, int sectorColAmount)
        {
            return new int2(sectorIndex.x * sectorColAmount, sectorIndex.y * sectorColAmount);
        }
        int GetGeneral1d(int2 local2d, int2 sector2d, int sectorColAmount, int fieldColAmount)
        {
            int2 sectorStart = GetSectorStartIndex(sector2d, sectorColAmount);
            int2 general2d = local2d + sectorStart;
            int general1d = To1D(general2d, fieldColAmount);
            return general1d;
        }
        int GetNextIndex(FlowData flow, int general1d, int fieldColAmount, int targetIndex)
        {
            int nextIndex = -1;
            switch (flow)
            {
                case FlowData.N:
                    nextIndex = general1d + fieldColAmount;
                    break;
                case FlowData.E:
                    nextIndex = general1d + 1;
                    break;
                case FlowData.S:
                    nextIndex = general1d - fieldColAmount;
                    break;
                case FlowData.W:
                    nextIndex = general1d - 1;
                    break;
                case FlowData.NE:
                    nextIndex = general1d + fieldColAmount + 1;
                    break;
                case FlowData.SE:
                    nextIndex = general1d - fieldColAmount + 1;
                    break;
                case FlowData.SW:
                    nextIndex = general1d - fieldColAmount - 1;
                    break;
                case FlowData.NW:
                    nextIndex = general1d + fieldColAmount - 1;
                    break;
                case FlowData.LOS:
                    nextIndex = To1D(targetIndex, fieldColAmount);
                    break;
            }
            return nextIndex;
        }
    }
}
public struct Waypoint
{
    public int index;
    public float2 position;
    public WaypointDirection blockedDirection;

    public bool Equals(Waypoint wayp)
    {
        return position.Equals(wayp.position) && blockedDirection == wayp.blockedDirection;
    }
}
[Flags]
public enum WaypointDirection : byte
{
    None = 0,
    N = 1,
    E = 2,
    S = 4,
    W = 8
}
[Flags]
public enum CornerDirections : byte
{
    None = 0,
    NE = 1,
    SE = 2,
    SW = 4,
    NW = 8
}