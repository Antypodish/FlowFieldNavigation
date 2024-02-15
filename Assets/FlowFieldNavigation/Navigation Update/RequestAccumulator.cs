using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class RequestAccumulator
    {
        FlowFieldNavigationManager _navigationManager;

        internal List<FlowFieldAgent> AgentAddRequest;
        internal NativeList<int> AgentIndiciesToRemove;
        internal NativeList<PathRequest> PathRequests;
        internal NativeList<CostEdit> CostEditRequests;
        internal NativeList<int> AgentIndiciesToSetHoldGround;
        internal NativeList<int> AgentIndiciesToStop;
        internal NativeList<SetSpeedReq> SetSpeedRequests;
        internal RequestAccumulator(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            AgentAddRequest = new List<FlowFieldAgent>();
            AgentIndiciesToRemove = new NativeList<int>(Allocator.Persistent);
            PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
            CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
            AgentIndiciesToSetHoldGround = new NativeList<int>(Allocator.Persistent);
            AgentIndiciesToStop = new NativeList<int>(Allocator.Persistent);
            SetSpeedRequests = new NativeList<SetSpeedReq>(Allocator.Persistent);
        }
        internal void RequestAgentAddition(FlowFieldAgent agent)
        {
            AgentAddRequest.Add(agent);
        }
        internal void RequestAgentRemoval(int agentIndex)
        {
            AgentIndiciesToRemove.Add(agentIndex);
        }
        internal void RequestPath(List<FlowFieldAgent> agents, Vector3 target)
        {
            int newPathIndex = PathRequests.Length;
            float2 target2d = new float2(target.x, target.z);
            PathRequests.Add(new PathRequest(target2d));
            _navigationManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
        }
        internal void RequestPath(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
        {
            int newPathIndex = PathRequests.Length;
            int targetAgentIndex = targetAgent.AgentDataIndex;
            PathRequest request = new PathRequest(targetAgentIndex);
            PathRequests.Add(request);
            _navigationManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
        }
        internal void RequestHoldGround(int agentIndex)
        {
            AgentIndiciesToSetHoldGround.Add(agentIndex);
        }
        internal void RequestStop(int agentIndex)
        {
            AgentIndiciesToStop.Add(agentIndex);
        }
        internal void RequestSetSpeed(int agentIndex, float speed)
        {
            SetSpeedReq setSpeedReq = new SetSpeedReq()
            {
                NewSpeed = speed,
                AgentIndex = agentIndex,
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
            AgentAddRequest.Clear();
            AgentAddRequest.TrimExcess();
            AgentAddRequest = null;
            PathRequests.Dispose();
            CostEditRequests.Dispose();
        }
    }
    internal struct SetSpeedReq
    {
        internal int AgentIndex;
        internal float NewSpeed;
    }


}
