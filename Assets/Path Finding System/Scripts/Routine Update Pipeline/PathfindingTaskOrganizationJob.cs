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
    public NativeArray<int> AgentCurrentPathIndicies;
    public NativeArray<PathRequest> NewPaths;
    public NativeArray<PathData> CurrentPaths;

    [ReadOnly] public NativeArray<IslandFieldProcessor> IslandFieldProcessors;

    public void Execute()
    {

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
                AgentNewPathIndicies[i] = -1;
                continue;
            }

            newPath.MinOffset = math.min(agentOffset, newPath.MinOffset);
            newPath.MaxOffset = math.max(agentOffset, newPath.MaxOffset);
            NewPaths[newPathIndex] = newPath;
        }
    }
}

public struct PathRequest
{
    public float2 Destination;
    public int MinOffset;
    public int MaxOffset;

    public PathRequest(float2 destination)
    {
        Destination = destination;
        MinOffset = int.MaxValue;
        MaxOffset = int.MinValue;
    }

    public bool IsValid()
    {
        return MinOffset != int.MaxValue;
    }
}