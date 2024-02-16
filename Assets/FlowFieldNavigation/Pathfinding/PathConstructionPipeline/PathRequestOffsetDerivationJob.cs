using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PathRequestOffsetDerivationJob : IJob
    {
        internal float TileSize;

        [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
        [ReadOnly] internal NativeArray<float3> AgentPositions;
        internal NativeArray<int> NewAgentPathIndicies;
        internal NativeList<PathRequest> InitialPathRequests;
        internal NativeList<OffsetDerivedPathRequest> DerivedPathRequests;


        public void Execute()
        {
            //SET DESTINATIONS OF DYNAMIC PATH REQUESTS
            for (int i = 0; i < InitialPathRequests.Length; i++)
            {
                PathRequest request = InitialPathRequests[i];
                if (request.Type == DestinationType.DynamicDestination)
                {
                    float3 targetAgentPos = AgentPositions[request.TargetAgentIndex];
                    float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
                    request.Destination = targetAgentPos2;
                    InitialPathRequests[i] = request;
                }
            }

            //DETERMINE OFFSETS
            int derivedPathRequestCount = 0;
            for (int i = 0; i < NewAgentPathIndicies.Length; i++)
            {
                int requestIndex = NewAgentPathIndicies[i];
                if (requestIndex == -1) { continue; }

                float agentRadius = AgentDataArray[i].Radius;
                int offset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);
                PathRequest request = InitialPathRequests[requestIndex];
                ushort offsetMask = (ushort)(1 << offset);
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

            //SET DERIVER REQUEST STARTS
            int counter = 0;
            for (int i = 0; i < InitialPathRequests.Length; i++)
            {
                PathRequest request = InitialPathRequests[i];
                request.DerivedRequestStartIndex = counter;
                InitialPathRequests[i] = request;
                counter += request.DerivedRequestCount;
            }

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
                if (derived.Offset == offset)
                {
                    return i;
                }
            }
            return -1;
        }
    }


}
