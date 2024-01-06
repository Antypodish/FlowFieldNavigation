using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

[BurstCompile]
public struct FlockIndexSubmissionJob : IJob
{
    public int InitialPathRequestCount;

    public NativeArray<int> AgentNewPathIndexArray;
    public NativeArray<int> AgentFlockIndexArray;

    public NativeArray<int> FlockIndexForEachInitialRequest;
    public NativeList<int> UnusedFlockIndexList;
    public NativeList<Flock> FlockList;
    public void Execute()
    {
        //Refresh FlockIndexForEachInitialRequest
        for(int i = 0; i < FlockIndexForEachInitialRequest.Length; i++)
        {
            FlockIndexForEachInitialRequest[0] = 0;
        }

        //Rest
        for(int i = 0; i < AgentNewPathIndexArray.Length; i++)
        {
            int newPathIndex = AgentNewPathIndexArray[i];
            if(newPathIndex == -1) { continue; }

            int initialRequestFlockIndex = FlockIndexForEachInitialRequest[newPathIndex];
            int agentCurrentFlockIndex = AgentFlockIndexArray[i];

            bool agentAlreadyHasFlock = agentCurrentFlockIndex != 0;
            bool initialRequestHasFlock = initialRequestFlockIndex != 0;
            bool hasUnusedIndex = UnusedFlockIndexList.Length != 0;
            if (agentAlreadyHasFlock)
            {
                Flock currentFlock = FlockList[agentCurrentFlockIndex];
                currentFlock.AgentCount -= 1;
                FlockList[agentCurrentFlockIndex] = currentFlock;
                if(currentFlock.AgentCount == 0) { UnusedFlockIndexList.Add(agentCurrentFlockIndex); }
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
                FlockIndexForEachInitialRequest[newPathIndex] = newFlockIndex;

                Flock newFlock = new Flock();
                newFlock.AgentCount++;
                FlockList[newFlockIndex] = newFlock;

                AgentFlockIndexArray[i] = newFlockIndex;
            }
            else
            {
                int newFlockIndex = FlockList.Length;
                FlockIndexForEachInitialRequest[newPathIndex] = newFlockIndex;

                Flock newFlock = new Flock();
                newFlock.AgentCount++;
                FlockList.Add(newFlock);

                AgentFlockIndexArray[i] = newFlockIndex;
            }
        }
    }
}
public struct Flock
{
    public int AgentCount;
}