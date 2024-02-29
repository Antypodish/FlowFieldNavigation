using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class RequestAccumulator
    {
        FlowFieldNavigationManager _navigationManager;

        internal NativeList<AgentInput> SubReqAgentInputs;
        internal List<Transform> SubReqAgentTransforms;
        internal NativeList<int> SubReqAgentDataRefIndicies;
        internal NativeList<int> AgentReferanceIndiciesToRemove;
        internal NativeList<PathRequest> PathRequests;
        internal NativeList<CostEdit> CostEditRequests;
        internal NativeList<AgentDataWrite> AgentDataWrites;
        internal RequestAccumulator(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            AgentReferanceIndiciesToRemove = new NativeList<int>(Allocator.Persistent);
            PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
            CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
            SubReqAgentDataRefIndicies = new NativeList<int>(Allocator.Persistent);
            SubReqAgentInputs = new NativeList<AgentInput>(Allocator.Persistent);
            SubReqAgentTransforms = new List<Transform>();
            AgentDataWrites = new NativeList<AgentDataWrite>(Allocator.Persistent);
        }
        internal void RequestAgentAddition(AgentInput agentInput, Transform agentTransform, int agentDataReferanceIndex)
        {
            _navigationManager.AgentReferanceManager.AgentDataReferances[agentDataReferanceIndex] = new AgentDataReferance(SubReqAgentDataRefIndicies.Length);
            SubReqAgentDataRefIndicies.Add(agentDataReferanceIndex);
            SubReqAgentInputs.Add(agentInput);
            SubReqAgentTransforms.Add(agentTransform);
        }
        internal void RequestAgentRemoval(AgentReferance agentReferance)
        {
            int agentDataReferanceIndex = agentReferance.GetIndexNonchecked();
            AgentDataReferanceState dataRefState = _navigationManager.AgentReferanceManager.AgentDataRefStates[agentDataReferanceIndex];
            _navigationManager.AgentReferanceManager.AgentDataRefStates[agentDataReferanceIndex] = AgentDataReferanceState.Removed;
            if (dataRefState == AgentDataReferanceState.BeingAdded) { return; }
            AgentReferanceIndiciesToRemove.Add(agentDataReferanceIndex);
        }
        internal void RequestPath(NativeArray<AgentReferance> sourceAgentReferances, Vector3 target)
        {
            int newPathIndex = PathRequests.Length;
            float2 target2d = new float2(target.x, target.z);
            PathRequests.Add(new PathRequest(target2d));
            SetAgentRequestedPaths(sourceAgentReferances, newPathIndex);
        }
        internal void RequestPath(NativeArray<AgentReferance> sourceAgentReferances, AgentReferance targetAgentRef)
        {
            int newPathIndex = PathRequests.Length;
            int targetAgentIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(targetAgentRef.GetIndexNonchecked());
            PathRequest request = new PathRequest(targetAgentIndex);
            PathRequests.Add(request);
            SetAgentRequestedPaths(sourceAgentReferances, newPathIndex);
        }
        internal void RequestHoldGround(AgentReferance agentReferance)
        {
            int agentDataWriteIndex = SetAgentDataWriteAndGetIndex(agentReferance);
            AgentDataWrite dataWrite = AgentDataWrites[agentDataWriteIndex];
            dataWrite.SetAgentHoldGround();
            AgentDataWrites[agentDataWriteIndex] = dataWrite;
        }
        internal void RequestStop(AgentReferance agentReferance)
        {
            int agentDataWriteIndex = SetAgentDataWriteAndGetIndex(agentReferance);
            AgentDataWrite dataWrite = AgentDataWrites[agentDataWriteIndex];
            dataWrite.SetAgentStopped();
            AgentDataWrites[agentDataWriteIndex] = dataWrite;
        }
        internal void RequestSetSpeed(AgentReferance agentReferance, float speed)
        {
            int agentDataWriteIndex = SetAgentDataWriteAndGetIndex(agentReferance);
            AgentDataWrite dataWrite = AgentDataWrites[agentDataWriteIndex];
            dataWrite.SetSpeed(speed);
            AgentDataWrites[agentDataWriteIndex] = dataWrite;
        }

        internal int HandleObstacleRequestAndGetIndex(ObstacleRequest obstacleRequest)
        {
            (int obstacleIndex, CostEdit costEdit) = _navigationManager.FieldDataContainer.ObstacleContainer.AddObstacleAndGetIndexAndCostEdit(obstacleRequest);
            CostEditRequests.Add(costEdit);
            return obstacleIndex;
        }
        internal void HandleObstacleRemovalRequest(int obstacleIndex)
        {
            CostEdit removalCostEdit = _navigationManager.FieldDataContainer.ObstacleContainer.RemoveObstacleAndGetCostEdit(obstacleIndex);
            CostEditRequests.Add(removalCostEdit);
        }
        internal void DisposeAll()
        {
            PathRequests.Dispose();
            CostEditRequests.Dispose();
        }


        void SetAgentRequestedPaths(NativeArray<AgentReferance> sourceAgentRefs, int requestedPathIndex)
        {
            NativeArray<int> agentDataRefWriteIndicies = _navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies.AsArray();

            SetAgentDataWrites(sourceAgentRefs);
            for (int i = 0; i < sourceAgentRefs.Length; i++)
            {
                AgentReferance sourceAgentRef = sourceAgentRefs[i];
                int agentDataRefIndex = sourceAgentRef.GetIndexNonchecked();
                int writeIndex = agentDataRefWriteIndicies[agentDataRefIndex];
                AgentDataWrite dataWrite = AgentDataWrites[writeIndex];
                dataWrite.SetReqPathIndex(requestedPathIndex);
                AgentDataWrites[writeIndex] = dataWrite;
            }
        }
        void SetAgentDataWrites(NativeArray<AgentReferance> agentReferances)
        {
            NativeArray<int> agentDataRefWriteIndicies = _navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies.AsArray();
            for (int i = 0; i < agentReferances.Length; i++)
            {
                int agentDataRefIndex = agentReferances[i].GetIndexNonchecked();
                if (agentDataRefWriteIndicies[agentDataRefIndex] == -1)
                {
                    AgentDataWrite dataWrite = new AgentDataWrite(agentDataRefIndex);
                    AgentDataWrites.Add(dataWrite);
                    agentDataRefWriteIndicies[agentDataRefIndex] = AgentDataWrites.Length - 1;
                }
            }
        }
        int SetAgentDataWriteAndGetIndex(AgentReferance agentReferance)
        {
            int agentDataRefIndex = agentReferance.GetIndexNonchecked();
            if (_navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataRefIndex] == -1)
            {
                AgentDataWrite dataWrite = new AgentDataWrite(agentDataRefIndex);
                AgentDataWrites.Add(dataWrite);
                _navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataRefIndex] = AgentDataWrites.Length - 1;
            }
            return _navigationManager.AgentReferanceManager.AgentDataReferanceWriteIndicies[agentDataRefIndex];
        }
    }
    internal struct SetSpeedReq
    {
        internal int AgentIndex;
        internal float NewSpeed;
    }


}
