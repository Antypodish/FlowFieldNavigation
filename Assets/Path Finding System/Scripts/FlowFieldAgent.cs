using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    PathfindingManager _pathfindingManager;
    [SerializeField] float Radius;
    [SerializeField] float Speed;
    [SerializeField] float LandOffset;

    [HideInInspector] public int AgentDataIndex;
    [HideInInspector] public Transform Transform;

    Waypoint currentWaypoint;
    Path oldPath;
    private void Start()
    {
        _pathfindingManager = FindObjectOfType<PathfindingManager>();
        _pathfindingManager.Subscribe(this);
        Transform = transform;
    }
    public void SetPath(Path path)
    {
        _pathfindingManager.SetPath(AgentDataIndex, path);
    }
    public Path GetPath()
    {
        return _pathfindingManager.GetPath(AgentDataIndex);
    }
    public float GetSpeed() => Speed;
    public float GetRadius() => Radius;
    public float GetLandOffset() => LandOffset;
    public AgentStatus GetAgentStatus() => _pathfindingManager.AgentDataContainer.AgentDataList[AgentDataIndex].Status;
    public void SetSpeed(float newSpeed) { Speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }

    float lastTime = 0f;
    //WAYPOINT TRIALS
    public void OnDrawGizmos()
    {
        return;
        if (_pathfindingManager == null) { return; }
        Path path = GetPath();
        if (path == null) { return; }

        if (path != oldPath)
        {
            oldPath = path;
            currentWaypoint = new Waypoint();
        }
        float tileSize = _pathfindingManager.TileSize;
        int fieldColAmount = _pathfindingManager.ColumnAmount;
        int sectorColAmount = _pathfindingManager.SectorColAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;
        int targetGeneral1d = To1D(path.TargetIndex, fieldColAmount);
        Vector3 agentPos = Transform.position;
        Vector3 targetPos = new Vector3(path.Destination.x, agentPos.y, path.Destination.y);
        int2 general2d = PosTo2D(agentPos, tileSize);
        int general1d = To1D(general2d, fieldColAmount);


        float time = Time.realtimeSinceStartup;
        if (time - lastTime >= 0.016f)
        {
            lastTime = time;
            //ALGORITHM
            NativeList<int> waypointIndicies = new NativeList<int>(Allocator.Temp);
            waypointIndicies.Add(general1d);

            int waypIndex = general1d;
            if (path.SectorToPicked[To1D(GetSectorIndex(general2d, sectorColAmount), sectorMatrixColAmount)] == 0) { return; }
            Vector3 wayPos = IndexToPos(waypIndex, tileSize, fieldColAmount);


            if (waypIndex != targetGeneral1d)
            {
                Waypoint newWayp;
                GetNextWaypoint(waypIndex, wayPos, out newWayp);
                if (!currentWaypoint.Equals(new Waypoint()))
                {
                    currentWaypoint = GetBestWaypoint(currentWaypoint, newWayp, general1d);
                }
                else
                {
                    currentWaypoint = newWayp;
                }
            }
        }

        
        if (currentWaypoint.position == Vector3.zero) { return; }
        Gizmos.color = Color.black;
        Gizmos.DrawLine(agentPos, currentWaypoint.position);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(currentWaypoint.position, 0.3f);
        transform.position = Vector3.MoveTowards(agentPos, currentWaypoint.position, 7 * Time.deltaTime);
        /*
        Waypoint potentialWaypoint;
        while(waypIndex != targetGeneral1d)
        {
            waypIndex = GetNextWaypoint(waypIndex, wayPos, out potentialWaypoint);
            wayPos = IndexToPos(waypIndex, tileSize, fieldColAmount);
            waypointIndicies.Add(waypIndex);
        }


        //DEBUG WAYPOINTS
        if (waypointIndicies.Length == 0) { return; }
        Vector3 prevPoint = IndexToPos(waypointIndicies[0], tileSize, fieldColAmount);
        Gizmos.color = Color.red;
        for (int i = 1; i < waypointIndicies.Length; i++)
        {
            Vector3 p1 = IndexToPos(waypointIndicies[i], tileSize, fieldColAmount);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(p1, 0.3f);
            Gizmos.color = Color.black;
            Gizmos.DrawLine(prevPoint, p1);
            prevPoint = p1;
        }

        //MOVE
        if(waypointIndicies.Length <= 1) { return; }
        int waypointIndex = waypointIndicies[1];
        Vector3 start = transform.position;
        Vector3 waypointPos = IndexToPos(waypointIndex, tileSize, fieldColAmount);
        waypointPos.y = start.y;
        transform.position = Vector3.MoveTowards(start, waypointPos, 7 * Time.deltaTime);
        */
        Waypoint GetBestWaypoint(Waypoint oldWaypoint, Waypoint newWaypoint, int sourceIndex)
        {
            if (!IsWaypoint(oldWaypoint.index, sourceIndex, out oldWaypoint))
            {
                return newWaypoint;
            }
            Vector3 sourcePos = IndexToPos(sourceIndex, tileSize, fieldColAmount);
            float oldDist = Vector3.Distance(oldWaypoint.position, sourcePos);
            float newDist = Vector3.Distance(newWaypoint.position, sourcePos);
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
        FlowData GetFlowAt(int general1d, Path path)
        {
            int2 general2d = To2D(general1d, fieldColAmount);
            int2 sector2d = GetSectorIndex(general2d, sectorColAmount);
            int2 sectorStart2d = GetSectorStartIndex(sector2d, sectorColAmount);
            int2 local2d = GetLocalIndex(general2d, sectorStart2d);
            int local1d = To1D(local2d, sectorColAmount);
            int sector1d = To1D(sector2d, sectorMatrixColAmount);

            int flowStartIndex = path.SectorToPicked[sector1d];
            int flowIndex = flowStartIndex + local1d;
            return path.FlowField[flowIndex];
        }
        int GetNextWaypoint(int source1d, Vector3 sourcePos, out Waypoint wayp)
        {
            int lastWaypointIndex;
            Waypoint lastWaypoint = GetNextWaypointCandidate(source1d, source1d, out lastWaypointIndex);
            if (lastWaypointIndex == targetGeneral1d)
            {
                wayp = new Waypoint()
                {
                    index = targetGeneral1d,
                    position = targetPos,
                    blockedDirection = 0,
                };
                return lastWaypointIndex;
            }

            int newWaypointIndex;
            Waypoint newWaypoint = GetNextWaypointCandidate(source1d, lastWaypointIndex, out newWaypointIndex);

            while (IsInLOS(newWaypoint.position, lastWaypoint, sourcePos))
            {
                if (newWaypointIndex == targetGeneral1d)
                {
                    wayp = newWaypoint;
                    return newWaypointIndex;
                }

                lastWaypointIndex = newWaypointIndex;
                lastWaypoint = newWaypoint;
                newWaypoint = GetNextWaypointCandidate(source1d, lastWaypointIndex, out newWaypointIndex);
            }
            wayp = lastWaypoint;
            return lastWaypointIndex;

        }
        bool IsInLOS(Vector3 point, Waypoint wayp, Vector3 sourcePos)
        {
            Vector3 wayPos = wayp.position;
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
            Vector3 lh = wayPos.x < sourcePos.x ? wayPos : sourcePos;
            Vector3 rh = wayPos.x >= sourcePos.x ? wayPos : sourcePos;

            float m = (rh.z - lh.z) / (rh.x - lh.x);

            if (m == 0f)
            {
                if ((wayDir & WaypointDirection.S) == WaypointDirection.S && point.z < wayPos.z)
                {
                    return false;
                }
                if ((wayDir & WaypointDirection.N) == WaypointDirection.N && point.z > wayPos.z)
                {
                    return false;
                }
                return true;
            }

            float c;
            c = lh.z - lh.x * m;
            float pointZOnLine = m * point.x + c;
            if ((wayDir & WaypointDirection.S) == WaypointDirection.S && point.z < pointZOnLine)
            {
                return false;
            }
            if ((wayDir & WaypointDirection.N) == WaypointDirection.N && point.z > pointZOnLine)
            {
                return false;
            }
            return true;
        }
        Waypoint GetNextWaypointCandidate(int source1d, int start1d, out int waypointIndex)
        {
            waypointIndex = -1;
            int cur1d = start1d;
            bool waypointFound = false;
            Waypoint wayp = new Waypoint();
            while (!waypointFound)
            {
                FlowData flow = GetFlowAt(cur1d, path);
                cur1d = GetNextIndex(flow, cur1d, fieldColAmount, path);
                if (IsWaypoint(cur1d, source1d, out wayp))
                {
                    waypointIndex = cur1d;
                    return wayp;
                }
            }
            return wayp;
        }
        bool IsWaypoint(int potentialWaypoint1d, int source1d, out Waypoint wayp)
        {
            bool isWaypoint = false;
            wayp = new Waypoint();
            Vector3 waypPos = IndexToPos(potentialWaypoint1d, tileSize, fieldColAmount);
            wayp.position = waypPos;
            wayp.index = potentialWaypoint1d;
            Vector3 sourcePos = IndexToPos(source1d, tileSize, fieldColAmount);
            UnsafeList<byte> costs = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset).CostsG;

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
            if (potentialWaypoint1d == targetGeneral1d) { return true; }
            if (neCost == byte.MaxValue && nCost != byte.MaxValue && eCost != byte.MaxValue)
            {
                int2 change = curDif - neDif;
                int componentChange = change.x * change.y;
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
        int2 PosTo2D(Vector3 pos, float tileSize)
        {
            return new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
        }
        Vector3 IndexToPos(int general1d, float tileSize, int fieldColAmount)
        {
            int2 general2d = To2D(general1d, fieldColAmount);
            return new Vector3(general2d.x * tileSize + tileSize / 2, 0.5f, general2d.y * tileSize + tileSize / 2);
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
        int GetNextIndex(FlowData flow, int general1d, int fieldColAmount, Path path)
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
                    nextIndex = To1D(path.TargetIndex, fieldColAmount);
                    break;
            }
            return nextIndex;
        }
    }

    struct Waypoint
    {
        public int index;
        public Vector3 position;
        public WaypointDirection blockedDirection;

        public bool Equals(Waypoint wayp)
        {
            return position == wayp.position && blockedDirection == wayp.blockedDirection;
        }
    }
    [Flags]
    enum WaypointDirection : byte
    {
        None = 0,
        N = 1,
        E = 2,
        S = 4,
        W = 8
    }
}
