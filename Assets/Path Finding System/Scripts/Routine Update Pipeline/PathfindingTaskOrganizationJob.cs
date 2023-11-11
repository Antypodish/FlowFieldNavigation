using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct PathfindingTaskOrganizationJob : IJob
{
    public float TileSize;

    public NativeArray<AgentData> AgentData;
    public NativeArray<int> AgentNewPathIndicies;
    //public NativeArray<int> AgentCurrentPathIndicies;
    public NativeArray<PathRequest> NewPaths;
    //public NativeArray<PathData> CurrentPaths;

    [ReadOnly] public NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    public NativeList<float2> PathRequestSources;
    public void Execute()
    {
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

        PathRequestSources.Length = pathRequestSourceLength;

        int curIndex = 0;
        //SET PATH REQUEST SOURCE START INDICIES OF PATH REQUESTS
        for(int i = 0; i < NewPaths.Length; i++)
        {
            PathRequest req = NewPaths[i];
            req.SourcePositionStartIndex = curIndex;
            curIndex += req.AgentCount;
            req.AgentCount = 0;
            NewPaths[i] = req;
        }
        //SUBMIT PATH REQ SOURCES
        for(int i = 0; i < AgentData.Length; i++)
        {
            int newPathIndex = AgentNewPathIndicies[i];
            if (newPathIndex == -1) { continue; }
            PathRequest req = NewPaths[newPathIndex];
            float3 agentPos3 = AgentData[i].Position;
            float2 agentPos = new float2(agentPos3.x, agentPos3.z);
            PathRequestSources[req.SourcePositionStartIndex + req.AgentCount] = agentPos;
            req.AgentCount = req.AgentCount + 1;
            NewPaths[newPathIndex] = req;
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