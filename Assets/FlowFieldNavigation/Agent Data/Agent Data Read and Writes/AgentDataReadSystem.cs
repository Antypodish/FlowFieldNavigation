using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowFieldNavigation
{
    internal class AgentDataReadSystem
    {
        FlowFieldNavigationManager _navManager;
        internal AgentDataReadSystem(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
        }

        internal int ReadCurPathIndex(AgentReferance agentRef)
        {
            if (_navManager.AgentReferanceManager.TryAgentDataReferanceIndexToAgentDataIndex(agentRef.GetIndexNonchecked(), out int agentDataIndex))
            {
                return _navManager.AgentDataContainer.AgentCurPathIndicies[agentDataIndex];
            }
            return -1;
        }
        internal AgentStatus ReadAgentStatusFlags(AgentReferance agentRef)
        {
            int agentDataRefIndex = agentRef.GetIndexNonchecked();
            AgentDataReferanceState dataRefState = _navManager.AgentReferanceManager.AgentDataRefStates[agentDataRefIndex];
            int agentDataIndex = _navManager.AgentReferanceManager.AgentDataReferances[agentDataRefIndex].GetIndexNonchecked();
            int agentDataRefWriteIndex = _navManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataIndex];
            if(agentDataRefWriteIndex == -1)
            {
                return _navManager.AgentDataContainer.AgentDataList[agentDataIndex].Status;
            }
            AgentDataWrite dataWrite = _navManager.RequestAccumulator.AgentDataWrites[agentDataRefWriteIndex];
            if(dataWrite.GetStatusIfWritten(out AgentStatus status))
            {
                return status;
            }
            return _navManager.AgentDataContainer.AgentDataList[agentDataIndex].Status;
        }
        internal float ReadAgentSpeed(AgentReferance agentRef)
        {
            int agentDataRefIndex = agentRef.GetIndexNonchecked();
            AgentDataReferanceState dataRefState = _navManager.AgentReferanceManager.AgentDataRefStates[agentDataRefIndex];
            int agentDataIndex = _navManager.AgentReferanceManager.AgentDataReferances[agentDataRefIndex].GetIndexNonchecked();
            int agentDataRefWriteIndex = _navManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataIndex];
            if (agentDataRefWriteIndex == -1)
            {
                return _navManager.AgentDataContainer.AgentDataList[agentDataIndex].Speed;
            }
            AgentDataWrite dataWrite = _navManager.RequestAccumulator.AgentDataWrites[agentDataRefWriteIndex];
            if (dataWrite.GetSpeedIfWritten(out float speed))
            {
                return speed;
            }
            return _navManager.AgentDataContainer.AgentDataList[agentDataIndex].Speed;
        }
        internal bool ReadAgentNavigationMovementFlag(AgentReferance agentRef)
        {
            int agentDataRefIndex = agentRef.GetIndexNonchecked();
            AgentDataReferanceState dataRefState = _navManager.AgentReferanceManager.AgentDataRefStates[agentDataRefIndex];
            int agentDataIndex = _navManager.AgentReferanceManager.AgentDataReferances[agentDataRefIndex].GetIndexNonchecked();
            int agentDataRefWriteIndex = _navManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataIndex];
            if (agentDataRefWriteIndex == -1)
            {
                return _navManager.AgentDataContainer.AgentUseNavigationMovementFlags[agentDataIndex];
            }
            AgentDataWrite dataWrite = _navManager.RequestAccumulator.AgentDataWrites[agentDataRefWriteIndex];
            if (dataWrite.GetNavMovementFlagIfWritten(out bool flag))
            {
                return flag;
            }
            return _navManager.AgentDataContainer.AgentUseNavigationMovementFlags[agentDataIndex];
        }
    }
}
