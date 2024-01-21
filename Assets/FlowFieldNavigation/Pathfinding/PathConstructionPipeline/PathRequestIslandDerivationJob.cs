using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct PathRequestIslandDerivationJob : IJob
{
    internal float TileSize;

    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    internal NativeArray<int> NewAgentPathIndicies;
    internal NativeList<OffsetDerivedPathRequest> DerivedPathRequests;
    internal NativeList<FinalPathRequest> FinalPathRequests;
    internal NativeArray<IslandFieldProcessor> IslandFieldProcesorsPerOffset;
    [WriteOnly] internal NativeReference<int> PathRequestSourceCount;
    public void Execute()
    {
        //INITIALIZE ISLAND FIELDS OF DERIVED PATH REQUESTS
        UnsafeList<UnsafeList<int>> islandFieldsOfDerivedPathRequests = new UnsafeList<UnsafeList<int>>(DerivedPathRequests.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        islandFieldsOfDerivedPathRequests.Length = DerivedPathRequests.Length;
        for (int i = 0; i < islandFieldsOfDerivedPathRequests.Length; i++)
        {
            islandFieldsOfDerivedPathRequests[i] = new UnsafeList<int>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        }


        //SET ISLAND DERIVATIONS FOR EACH REQUEST
        int finalPathRequestCount = 0;
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            int pathRequestIndex = NewAgentPathIndicies[i];
            if(pathRequestIndex == -1) { continue; }
            
            AgentData agentData = AgentDataArray[i];
            float agentRadius = agentData.Radius;
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            int offset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);
            int island = IslandFieldProcesorsPerOffset[offset].GetIsland(agentPos);
            if(island == int.MaxValue)
            {
                NewAgentPathIndicies[i] = -1;
                continue;
            }

            UnsafeList<int> pointedRequestIslands = islandFieldsOfDerivedPathRequests[pathRequestIndex];
            if(!Contains(island, pointedRequestIslands))
            {
                pointedRequestIslands.Add(island);
                finalPathRequestCount++;
                islandFieldsOfDerivedPathRequests[pathRequestIndex] = pointedRequestIslands;
            }
        }

        FinalPathRequests.Length = finalPathRequestCount;

        int initializedFinalRequestCount = 0;
        //SET FINAL PATH REQUESTS
        for(int i = 0; i < DerivedPathRequests.Length; i++)
        {
            OffsetDerivedPathRequest derivedReq = DerivedPathRequests[i];
            int derivedReqStartIncluding = initializedFinalRequestCount;
            UnsafeList<int> derivedReqIslands = islandFieldsOfDerivedPathRequests[i];
            for(int j = 0; j < derivedReqIslands.Length; j++)
            {
                FinalPathRequests[initializedFinalRequestCount] = new FinalPathRequest(derivedReq, derivedReqIslands[j]);
                initializedFinalRequestCount++;
            }
            int derivedReqEndExcluding = initializedFinalRequestCount;
            int derivedReqCount = derivedReqEndExcluding - derivedReqStartIncluding;
            derivedReq.DerivedFialRequestStartIndex = derivedReqStartIncluding;
            derivedReq.DerivedFinalRequestCount = derivedReqCount;
            DerivedPathRequests[i] = derivedReq;
        }

        //POINT AGENTS TO FINAL PATH REQUESTS
        int totalSourceCount = 0;
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            int pathRequestIndex = NewAgentPathIndicies[i];
            if (pathRequestIndex == -1) { continue; }

            AgentData agentData = AgentDataArray[i];
            float agentRadius = agentData.Radius;
            float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
            int offset = FlowFieldUtilities.RadiusToOffset(agentRadius, TileSize);
            int island = IslandFieldProcesorsPerOffset[offset].GetIsland(agentPos);

            OffsetDerivedPathRequest derivedReq = DerivedPathRequests[pathRequestIndex];
            int finalRequestIndex = GetIndexOfFinalRequestToPointTowards(island, derivedReq.DerivedFialRequestStartIndex, derivedReq.DerivedFinalRequestCount);
            NewAgentPathIndicies[i] = finalRequestIndex;
            
            if(finalRequestIndex != -1)
            {
                FinalPathRequest pointedRequest = FinalPathRequests[finalRequestIndex];
                pointedRequest.SourceCount++;
                FinalPathRequests[finalRequestIndex] = pointedRequest;
                totalSourceCount++;
            }
        }
        PathRequestSourceCount.Value = totalSourceCount;

        //FREE ISLAND FIELDS OF DERIVED PATH REQUESTS
        for (int i = 0; i < islandFieldsOfDerivedPathRequests.Length; i++)
        {
            islandFieldsOfDerivedPathRequests[i].Dispose();
        }
        islandFieldsOfDerivedPathRequests.Dispose();
    }

    bool Contains(int islandFieldIndex, UnsafeList<int> list)
    {
        for(int i = 0; i < list.Length; i++)
        {
            if (list[i] == islandFieldIndex) { return true; }
        }
        return false;
    }
    int GetIndexOfFinalRequestToPointTowards(int island, int finalReqStartIndex, int finalReqCount)
    {
        for(int i = finalReqStartIndex; i < finalReqStartIndex + finalReqCount; i++)
        {
            if (FinalPathRequests[i].SourceIsland == island) { return i; }
        }
        return -1;
    }
}
