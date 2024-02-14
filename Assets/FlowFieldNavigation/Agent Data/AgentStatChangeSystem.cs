using Unity.Collections;
using Unity.Jobs;
internal class AgentStatChangeSystem
{
    FlowFieldNavigationManager _navigationManager;
    internal AgentStatChangeSystem(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public void SetAgentsHoldGround(NativeArray<int> agentIndiciesToHoldGround)
    {
        SetAgentHoldGroundJob holdGroundJob = new SetAgentHoldGroundJob()
        {
            AgentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray(),
            AgentIndiciesToHoldGround = agentIndiciesToHoldGround,
        };
        holdGroundJob.Schedule().Complete();
    }
    public void SetAgentsStopped(NativeArray<int> agentIndiciesToStop)
    {
        AgentStopJob holdGroundJob = new AgentStopJob()
        {
            AgentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray(),
            AgentIndiciesToStop = agentIndiciesToStop,
        };
        holdGroundJob.Schedule().Complete();
    }
    public void SetAgentSpeed(NativeArray<SetSpeedReq> setSpeedRequests)
    {
        AgentSpeedSetJob setSpeedJob = new AgentSpeedSetJob()
        {
            AgentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray(),
            SetSpeedRequests = setSpeedRequests,
        };
        setSpeedJob.Schedule().Complete();
    }
}