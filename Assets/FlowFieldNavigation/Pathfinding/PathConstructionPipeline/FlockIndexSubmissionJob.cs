using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct FlockIndexSubmissionJob : IJob
    {
        internal int InitialPathRequestCount;

        internal NativeArray<int> AgentNewPathIndexArray;
        internal NativeArray<int> AgentFlockIndexArray;

        internal NativeArray<PathRequest> InitialPathRequests;
        internal NativeList<int> UnusedFlockIndexList;
        internal NativeList<Flock> FlockList;
        public void Execute()
        {
            //Rest
            for (int i = 0; i < AgentNewPathIndexArray.Length; i++)
            {
                int newPathIndex = AgentNewPathIndexArray[i];
                if (newPathIndex == -1) { continue; }

                PathRequest initialRequest = InitialPathRequests[newPathIndex];
                int initialRequestFlockIndex = initialRequest.FlockIndex;
                int agentCurrentFlockIndex = AgentFlockIndexArray[i];

                bool agentAlreadyHasFlock = agentCurrentFlockIndex != 0;
                bool initialRequestHasFlock = initialRequestFlockIndex != 0;
                bool hasUnusedIndex = UnusedFlockIndexList.Length != 0;
                if (agentAlreadyHasFlock)
                {
                    Flock currentFlock = FlockList[agentCurrentFlockIndex];
                    currentFlock.AgentCount -= 1;
                    FlockList[agentCurrentFlockIndex] = currentFlock;
                    if (currentFlock.AgentCount == 0) { UnusedFlockIndexList.Add(agentCurrentFlockIndex); }
                }

                if (initialRequestHasFlock)
                {
                    AgentFlockIndexArray[i] = initialRequestFlockIndex;
                    Flock flock = FlockList[initialRequestFlockIndex];
                    flock.AgentCount++;
                    FlockList[initialRequestFlockIndex] = flock;
                }
                else if (hasUnusedIndex)
                {
                    int newFlockIndex = UnusedFlockIndexList[0];
                    UnusedFlockIndexList.RemoveAtSwapBack(0);
                    initialRequest.FlockIndex = newFlockIndex;
                    InitialPathRequests[newPathIndex] = initialRequest;

                    Flock newFlock = new Flock();
                    newFlock.AgentCount++;
                    FlockList[newFlockIndex] = newFlock;

                    AgentFlockIndexArray[i] = newFlockIndex;
                }
                else
                {
                    int newFlockIndex = FlockList.Length;
                    initialRequest.FlockIndex = newFlockIndex;
                    InitialPathRequests[newPathIndex] = initialRequest;

                    Flock newFlock = new Flock();
                    newFlock.AgentCount++;
                    FlockList.Add(newFlock);

                    AgentFlockIndexArray[i] = newFlockIndex;
                }
            }
        }
    }
    internal struct Flock
    {
        internal int AgentCount;
    }

}