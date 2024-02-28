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
        internal NativeList<int> AgentIndiciesToSetHoldGround;
        internal NativeList<int> AgentIndiciesToStop;
        internal NativeList<SetSpeedReq> SetSpeedRequests;

        internal RequestAccumulator(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            AgentReferanceIndiciesToRemove = new NativeList<int>(Allocator.Persistent);
            PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
            CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
            AgentIndiciesToSetHoldGround = new NativeList<int>(Allocator.Persistent);
            AgentIndiciesToStop = new NativeList<int>(Allocator.Persistent);
            SetSpeedRequests = new NativeList<SetSpeedReq>(Allocator.Persistent);
            SubReqAgentDataRefIndicies = new NativeList<int>(Allocator.Persistent);
            SubReqAgentInputs = new NativeList<AgentInput>(Allocator.Persistent);
            SubReqAgentTransforms = new List<Transform>();
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
            if(dataRefState == AgentDataReferanceState.BeingAdded)
            {
                _navigationManager.AgentReferanceManager.AgentDataRefStates[agentDataReferanceIndex] = AgentDataReferanceState.Removed;
                return;
            }
            AgentReferanceIndiciesToRemove.Add(agentDataReferanceIndex);
        }
        internal void RequestPath(NativeList<AgentReferance> sourceAgentReferances, Vector3 target)
        {
            int newPathIndex = PathRequests.Length;
            float2 target2d = new float2(target.x, target.z);
            PathRequests.Add(new PathRequest(target2d));
            SetAgentRequestedPaths(sourceAgentReferances, newPathIndex);
        }
        internal void RequestPath(NativeList<AgentReferance> sourceAgentReferances, AgentReferance targetAgentRef)
        {
            int newPathIndex = PathRequests.Length;
            int targetAgentIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(targetAgentRef.GetIndexNonchecked());
            PathRequest request = new PathRequest(targetAgentIndex);
            PathRequests.Add(request);
            SetAgentRequestedPaths(sourceAgentReferances, newPathIndex);
        }
        internal void RequestHoldGround(AgentReferance agentReferance)
        {
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferances[agentReferance.GetIndexNonchecked()].GetIndexNonchecked();
            AgentIndiciesToSetHoldGround.Add(agentDataIndex);
        }
        internal void RequestStop(AgentReferance agentReferance)
        {
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferances[agentReferance.GetIndexNonchecked()].GetIndexNonchecked();
            AgentIndiciesToStop.Add(agentDataIndex);
        }
        internal void RequestSetSpeed(AgentReferance agentReferance, float speed)
        {
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferances[agentReferance.GetIndexNonchecked()].GetIndexNonchecked();
            SetSpeedReq setSpeedReq = new SetSpeedReq()
            {
                NewSpeed = speed,
                AgentIndex = agentDataIndex,
            };
            SetSpeedRequests.Add(setSpeedReq);
        }

        internal void HandleObstacleRequest(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
        {
            ObstacleRequestToCostEdit obstacleToEdit = new ObstacleRequestToCostEdit()
            {
                TileSize = FlowFieldUtilities.TileSize,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                CostEditOutput = CostEditRequests,
                ObstacleRequests = obstacleRequests,
                NewObstacleKeyListToAdd = outputListToAddObstacleIndicies,
                ObstacleList = _navigationManager.FieldDataContainer.ObstacleContainer.ObstacleList,
                RemovedObstacleIndexList = _navigationManager.FieldDataContainer.ObstacleContainer.RemovedIndexList,
            };
            obstacleToEdit.Schedule().Complete();
        }
        internal void HandleObstacleRemovalRequest(NativeArray<int>.ReadOnly obstacleIndiciesToRemove)
        {
            ObstacleRemovalRequestToCostEdit obstacleToEdit = new ObstacleRemovalRequestToCostEdit()
            {
                CostEditOutput = CostEditRequests,
                ObstacleRemovalIndicies = obstacleIndiciesToRemove,
                ObstacleList = _navigationManager.FieldDataContainer.ObstacleContainer.ObstacleList,
                RemovedObstacleIndexList = _navigationManager.FieldDataContainer.ObstacleContainer.RemovedIndexList,
            };
            obstacleToEdit.Schedule().Complete();
        }
        internal void DisposeAll()
        {
            PathRequests.Dispose();
            CostEditRequests.Dispose();
        }

        void SetAgentRequestedPaths(NativeList<AgentReferance> sourceAgentReferances, int requestedPathIndex)
        {
            NativeArray<AgentDataReferance> agentDataReferances = _navigationManager.AgentReferanceManager.AgentDataReferances.AsArray();
            NativeArray<int> agentRequestedPathIndicies = _navigationManager.AgentDataContainer.AgentRequestedPathIndicies.AsArray();

            if (sourceAgentReferances.Length > 2500)
            {
                AgentRequestedPathSetJob reqPathSet = new AgentRequestedPathSetJob()
                {
                    RequestedPathIndex = requestedPathIndex,
                    AgentDataReferances = agentDataReferances,
                    SourceAgentReferances = sourceAgentReferances.AsArray(),
                    AgentRequestedPathIndicies = agentRequestedPathIndicies,
                };
                reqPathSet.Schedule().Complete();
                return;
            }

            for (int i = 0; i < sourceAgentReferances.Length; i++)
            {
                AgentReferance sourceAgentReferance = sourceAgentReferances[i];
                AgentDataReferance agentDataReferance = agentDataReferances[sourceAgentReferance.GetIndexNonchecked()];
                int agentDataIndex = agentDataReferance.GetIndexNonchecked();
                agentRequestedPathIndicies[agentDataIndex] = requestedPathIndex;
            }
        }
    }
    internal struct SetSpeedReq
    {
        internal int AgentIndex;
        internal float NewSpeed;
    }


}
