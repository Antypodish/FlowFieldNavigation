using Unity.Collections;
using Unity.Jobs;
internal class AgentStatChangeSystem
{
    PathfindingManager _pathfindingManager;
    internal AgentStatChangeSystem(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void SetAgentsHoldGround(NativeArray<int> agentIndiciesToHoldGround)
    {
        SetAgentHoldGroundJob holdGroundJob = new SetAgentHoldGroundJob()
        {
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList.AsArray(),
            AgentIndiciesToHoldGround = agentIndiciesToHoldGround,
        };
        holdGroundJob.Schedule().Complete();
    }
    public void SetAgentsStopped(NativeArray<int> agentIndiciesToStop)
    {
        AgentStopJob holdGroundJob = new AgentStopJob()
        {
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList.AsArray(),
            AgentIndiciesToStop = agentIndiciesToStop,
        };
        holdGroundJob.Schedule().Complete();
    }
    public void SetAgentSpeed(NativeArray<SetSpeedReq> setSpeedRequests)
    {
        AgentSpeedSetJob setSpeedJob = new AgentSpeedSetJob()
        {
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList.AsArray(),
            SetSpeedRequests = setSpeedRequests,
        };
        setSpeedJob.Schedule().Complete();
    }
}