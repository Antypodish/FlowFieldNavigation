using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class AgentDataWriteSystem
    {
        FlowFieldNavigationManager _navigationManager;
        internal AgentDataWriteSystem(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }
        public void WriteData()
        {
            NativeList<AgentDataWrite> agentDataWrites = _navigationManager.RequestAccumulator.AgentDataWrites;
            NativeArray<AgentDataReferanceState> agentDataReferanceState = _navigationManager.AgentReferanceManager.AgentDataRefStates.AsArray();
            NativeArray<AgentDataReferance> agentDataReferances = _navigationManager.AgentReferanceManager.AgentDataReferances.AsArray();
            NativeArray<int> agentDataReferancesWriteIndicies = _navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies.AsArray();
            NativeArray<int> agentNewPathIndicies = _navigationManager.AgentDataContainer.AgentNewPathIndicies.AsArray();
            NativeArray<AgentData> agentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray();

            AgentDataWriteTransferJob writeTransferJob = new AgentDataWriteTransferJob()
            {
                AgentDataWrites = agentDataWrites.AsArray(),
                AgentDataReferanceStates = agentDataReferanceState,
                AgentDataReferances = agentDataReferances,
                AgentDataReferanceWriteIndicies = agentDataReferancesWriteIndicies,
                AgentNewPathIndicies = agentNewPathIndicies,
                AgentDataArray = agentDataArray,
            };
            writeTransferJob.Schedule().Complete();

            agentDataWrites.Clear();
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

}