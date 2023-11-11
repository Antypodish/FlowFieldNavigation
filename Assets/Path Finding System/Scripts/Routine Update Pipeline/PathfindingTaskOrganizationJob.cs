using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct PathfindingTaskOrganizationJob : IJob
{
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<AgentData> AgentData;
    public NativeArray<int> AgentNewPathIndicies;
    public NativeArray<int> AgentCurrentPathIndicies;
    public NativeArray<PathRequest> NewPaths;
    public NativeArray<PathData> CurrentPaths;
    public NativeArray<int> PathSubscribers;

    [ReadOnly] public NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    public NativeList<float2> PathfindingSources;
    public void Execute()
    {
        NativeArray<PathTask> agentPathfindingState = new NativeArray<PathTask>(AgentData.Length, Allocator.Temp);

        int pathRequestSourceLength = 0;
        //EVALUATE PATH REQUEST
        for (int i = 0; i < AgentData.Length; i++)
        {
            float agentRadius = AgentData[i].Radius;
            float3 agentPosition3d = AgentData[i].Position;
            int newPathIndex = AgentNewPathIndicies[i];

            if(newPathIndex == -1) { continue; }

            float2 agentPosition2d = new float2(agentPosition3d.x, agentPosition3d.z);
            int agentOffset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);
            PathRequest newPath = NewPaths[newPathIndex];
            int agentIsland = IslandFieldProcessors[agentOffset].GetIsland(agentPosition2d);
            int destinationIsland = IslandFieldProcessors[agentOffset].GetIsland(newPath.Destination);
            
            bool differentIsland = agentIsland != destinationIsland;
            bool agentUnwalkable = agentIsland == int.MaxValue;
            bool destinationUnwalkable = destinationIsland == int.MaxValue;
            if(differentIsland || agentUnwalkable || destinationUnwalkable)
            {
                UnityEngine.Debug.Log("hi");
                AgentNewPathIndicies[i] = -1;
                continue;
            }

            newPath.MinOffset = math.min(agentOffset, newPath.MinOffset);
            newPath.MaxOffset = math.max(agentOffset, newPath.MaxOffset);
            newPath.AgentCount++;
            NewPaths[newPathIndex] = newPath;
            pathRequestSourceLength++;
        }
        
        //EVALUATE OUT-OF-FIELD AGENTS
        for(int i = 0; i < AgentData.Length; i++)
        {
            float3 agentPosition3d = AgentData[i].Position;
            float2 agentPosition2d = new float2(agentPosition3d.x, agentPosition3d.z);
            int newPathIndex = AgentNewPathIndicies[i];
            int curPathIndex = AgentCurrentPathIndicies[i];
            if(newPathIndex != -1 || curPathIndex == -1) { continue; }
            PathData curPathData = CurrentPaths[curPathIndex];
            int2 agentGeneral2d = FlowFieldUtilities.PosTo2D(agentPosition2d, TileSize);
            int2 agentSector2d = FlowFieldUtilities.GetSector2D(agentGeneral2d, SectorColAmount);
            int agentSector1d = FlowFieldUtilities.To1D(agentSector2d, SectorMatrixColAmount);
            int2 agentSectorStart2d = FlowFieldUtilities.GetSectorStartIndex(agentSector2d, SectorColAmount);
            int agentLocal1d = FlowFieldUtilities.GetLocal1D(agentGeneral2d, agentSectorStart2d, SectorColAmount);
            int sectorFlowStartIndex = curPathData.SectorToPicked[agentSector1d];
            FlowData flow = curPathData.FlowField[sectorFlowStartIndex + agentLocal1d];
            if (flow.IsValid()) { continue; }
            PathSectorState sectorState = curPathData.SectorStateTable[agentSector1d];
            if((sectorState & PathSectorState.FlowCalculated) == PathSectorState.FlowCalculated || sectorState == 0) { continue; }
            curPathData.FlowRequestSourceCount++;
            CurrentPaths[curPathIndex] = curPathData;
            agentPathfindingState[i] |= PathTask.FlowRequest;
            pathRequestSourceLength++;
        }
        PathfindingSources.Length = pathRequestSourceLength;

        int sourceCurIndex = 0;
        //SET PATH REQUEST SOURCE START INDICIES OF PATH REQUESTS
        for(int i = 0; i < NewPaths.Length; i++)
        {
            PathRequest req = NewPaths[i];
            req.SourcePositionStartIndex = sourceCurIndex;
            sourceCurIndex += req.AgentCount;
            req.AgentCount = 0;
            NewPaths[i] = req;
        }

        for (int i = 0; i < CurrentPaths.Length; i++)
        {
            PathData curPath = CurrentPaths[i];
            if(curPath.State == PathState.Removed || curPath.FlowRequestSourceCount == 0) { continue; }
            curPath.Task |= PathTask.FlowRequest; 
            curPath.FlowRequestSourceStart = sourceCurIndex;
            sourceCurIndex += curPath.FlowRequestSourceCount;
            curPath.FlowRequestSourceCount = 0;
            CurrentPaths[i] = curPath;
        }
        //SUBMIT PATH REQ SOURCES
        for (int i = 0; i < AgentData.Length; i++)
        {
            int newPathIndex = AgentNewPathIndicies[i];
            int curPathIndex = AgentCurrentPathIndicies[i];

            if (newPathIndex != -1)
            {
                PathRequest req = NewPaths[newPathIndex];
                float3 agentPos3 = AgentData[i].Position;
                float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                PathfindingSources[req.SourcePositionStartIndex + req.AgentCount] = agentPos;
                req.AgentCount = req.AgentCount + 1;
                NewPaths[newPathIndex] = req;
            }
            else if(curPathIndex != -1)
            {
                bool agentFlowRequested = (agentPathfindingState[i] & PathTask.FlowRequest) == PathTask.FlowRequest;
                if (!agentFlowRequested) { continue; }
                PathData curPath = CurrentPaths[curPathIndex];
                bool pathFlowRequested = (curPath.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
                if (!pathFlowRequested) { continue; }
                float3 agentPos3 = AgentData[i].Position;
                float2 agentPos = new float2(agentPos3.x, agentPos3.z);
                PathfindingSources[curPath.FlowRequestSourceStart + curPath.FlowRequestSourceCount] = agentPos;
                curPath.FlowRequestSourceCount++;
                CurrentPaths[curPathIndex] = curPath;
            }
        }
    }
}

public struct PathRequest
{
    public float2 Destination;
    public int MinOffset;
    public int MaxOffset;
    public int AgentCount;
    public int SourcePositionStartIndex;
    public int PathIndex;

    public PathRequest(float2 destination)
    {
        Destination = destination;
        MinOffset = int.MaxValue;
        MaxOffset = int.MinValue;
        AgentCount = 0;
        SourcePositionStartIndex = 0;
        PathIndex = 0;
    }

    public bool IsValid() => AgentCount != 0;
}