using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct PathRequestOffsetDerivationJob : IJob
{
    public float TileSize;

    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    public NativeArray<int> NewAgentPathIndicies;
    public NativeList<PathRequest> InitialPathRequests;
    public NativeList<OffsetDerivedPathRequest> DerivedPathRequests;

    
    public void Execute()
    {
        //DETERMINE OFFSETS
        int derivedPathRequestCount = 0;
        for (int i = 0; i < NewAgentPathIndicies.Length; i++)
        {
            int requestIndex = NewAgentPathIndicies[i];
            if(requestIndex == -1) { continue; }

            float agentRadius = AgentDataArray[i].Radius;
            int offset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);
            PathRequest request = InitialPathRequests[requestIndex];
            ushort offsetMask = (ushort) (1 << offset);
            bool hasOffset = (request.OffsetMask & offsetMask) == offsetMask;

            if (!hasOffset)
            {
                request.DerivedRequestCount += 1;
                request.OffsetMask |= offsetMask;
                derivedPathRequestCount++;
                InitialPathRequests[requestIndex] = request;
            }
        }
        DerivedPathRequests.Length = derivedPathRequestCount;

        //MAKE AGENTS POINT TO DERIVED REQUESTS
        for (int i = 0; i < NewAgentPathIndicies.Length; i++)
        {
            int requestIndex = NewAgentPathIndicies[i];
            if (requestIndex == -1) { continue; }

            float agentRadius = AgentDataArray[i].Radius;
            int offset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);

            PathRequest request = InitialPathRequests[requestIndex];
            int requestIndexToPointTowards = GetDerivedPathToPointTowardsAndInitializeIfNeeded(offset, request);

            NewAgentPathIndicies[i] = requestIndexToPointTowards;
        }
    }

    int GetDerivedPathToPointTowardsAndInitializeIfNeeded(int offset, PathRequest request)
    {
        int derivedStart = request.DerivedRequestStartIndex;
        int derivedCount = request.DerivedRequestCount;
        for (int i = derivedStart; i < derivedStart + derivedCount; i++)
        {
            OffsetDerivedPathRequest derived = DerivedPathRequests[i];
            if (!derived.IsCreated())
            {
                DerivedPathRequests[i] = new OffsetDerivedPathRequest(request, offset);
                return i;
            }
            if(derived.Offset == offset)
            {
                return i;
            }
        }
        return -1;
    }
}
