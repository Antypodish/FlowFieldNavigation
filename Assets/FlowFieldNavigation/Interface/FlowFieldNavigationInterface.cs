using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using static Codice.Client.Common.WebApi.WebApiEndpoints;

namespace FlowFieldNavigation
{
    public class FlowFieldNavigationInterface
    {
        FlowFieldNavigationManager _navigationManager;
        SimulationStartInputHandler _simStartInputHandler;
        public FlowFieldNavigationInterface(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _simStartInputHandler = new SimulationStartInputHandler();
        }

        public void StartSimulation(SimulationStartParametersStandard startParameters)
        {
            if (_navigationManager.SimulationStarted)
            {
                UnityEngine.Debug.Log("Request declined. Simulation is already started.");
                return;
            }
            SimulationInputs simulationStartInputs = _simStartInputHandler.HandleInput(startParameters, Allocator.TempJob);
            _navigationManager.StartSimulation(simulationStartInputs);
            simulationStartInputs.Dispose();
        }
        public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
            _navigationManager.RequestAccumulator.RequestPath(agents, target);
        }
        public void SetDestination(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            if (agents.Count == 0) { return; }
            if (!targetAgent.AgentReferance.IsValid()) { SetDestination(agents, targetAgent.transform.position); return; }
            _navigationManager.RequestAccumulator.RequestPath(agents, targetAgent);
        }
        public void SetObstacle(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            _navigationManager.RequestAccumulator.HandleObstacleRequest(obstacleRequests, outputListToAddObstacleIndicies);
        }
        public void RemoveObstacle(NativeArray<int>.ReadOnly obstaclesToRemove)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            _navigationManager.RequestAccumulator.HandleObstacleRemovalRequest(obstaclesToRemove);
        }
        public void RequestSubscription(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            if (agent.AgentReferance.IsValid()) { return; }
            int newAgentDataReferanceIndex = _navigationManager.AgentReferanceManager.CreateAgentReferance();
            agent._navigationManager = _navigationManager;
            agent.AgentReferance = new AgentReferance(newAgentDataReferanceIndex);
            AgentInput agentInput = agent.GetAgentInput();
            Transform agentTransform = agent.transform;
            _navigationManager.RequestAccumulator.RequestAgentAddition(agentInput, agentTransform, newAgentDataReferanceIndex);
        }
        public void RequestUnsubscription(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            agent.AgentReferance = new AgentReferance();
            _navigationManager.RequestAccumulator.RequestAgentRemoval(agentReferance);
        }
        public void SetHoldGround(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            _navigationManager.RequestAccumulator.RequestHoldGround(agentReferance);
        }
        public void SetStopped(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            _navigationManager.RequestAccumulator.RequestStop(agentReferance);
        }
        public void SetSpeed(FlowFieldAgent agent, float speed)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            _navigationManager.RequestAccumulator.RequestSetSpeed(agentReferance, speed);
        }
        public bool IsClearBetweenImmediate(Vector3 startPos, Vector3 endPos, int fieldIndex, float stopDistanceFromEnd = 0f)
        {
            if (!_navigationManager.SimulationStarted) { return false; }
            return _navigationManager.FieldImmediateQueryManager.IsClearBetween(startPos, endPos, fieldIndex, stopDistanceFromEnd);
        }
        public NativeArray<bool> IsClearBetweenImmediate(NativeArray<LineCastData> lineCasts, int fieldIndex, Allocator allocator)
        {
            if (!_navigationManager.SimulationStarted) { return new NativeArray<bool>(0, allocator); }
            return _navigationManager.FieldImmediateQueryManager.IsClearBetween(lineCasts, fieldIndex, allocator);
        }
        public int GetPathIndex(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return -1; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return - 1; }
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(agentReferance.GetIndexNonchecked());
            return _navigationManager.AgentDataContainer.AgentCurPathIndicies[agentDataIndex];
        }
        public void SetUseNavigationMovementFlag(FlowFieldAgent agent, bool set)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(agentReferance.GetIndexNonchecked());
            _navigationManager.AgentDataContainer.AgentUseNavigationMovementFlags[agentDataIndex] = set;
        }
        public bool GetUseNavigationMovementFlag(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return false; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return false; }
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(agentReferance.GetIndexNonchecked());
            return _navigationManager.AgentDataContainer.AgentUseNavigationMovementFlags[agentDataIndex];
        }
        public int GetAgentCount()
        {
            if (!_navigationManager.SimulationStarted) { return 0; }
            return _navigationManager.AgentDataContainer.AgentDataList.Length;
        }

    }
    public struct SimulationStartParametersStandard
    {
        internal FlowFieldSurface[] NavigationSurfaces;
        internal FlowFieldStaticObstacle[] StaticObstacles;
        internal float BaseAgentSpatialGridSize;
        internal float BaseTriangleSpatialGridSize;
        internal float MaxSurfaceHeightDifference;
        internal float TileSize;
        internal float MaxWalkableHeight;
        internal float VerticalVoxelSize;
        internal float MaxAgentRadius;
        internal int LineOfSightRange;
        internal Vector2 FieldStartPositionXZ;
        internal Vector2 FieldEndPositionXZ;

        public SimulationStartParametersStandard(FlowFieldSurface[] navigationSurfaces,
            FlowFieldStaticObstacle[] staticObstacles,
            float baseAgentSpatialGridSize,
            float baseTriangleSpatialGridSize,
            float maxAgentRadius,
            float maxSurfaceHeightDifference,
            float tileSize,
            float verticalVoxelSize,
            int lineOfSightRange,
            float maxWalkableHeight = float.MaxValue)
        {
            NavigationSurfaces = navigationSurfaces;
            StaticObstacles = staticObstacles;
            BaseAgentSpatialGridSize = Mathf.Max(baseAgentSpatialGridSize, 0.1f);
            BaseTriangleSpatialGridSize = Mathf.Max(baseTriangleSpatialGridSize, 0.1f);
            VerticalVoxelSize = Mathf.Max(verticalVoxelSize, 0.01f);
            MaxSurfaceHeightDifference = Mathf.Max(maxSurfaceHeightDifference, VerticalVoxelSize);
            TileSize = Mathf.Max(tileSize, 0.25f);
            MaxWalkableHeight = maxWalkableHeight;
            MaxAgentRadius = Mathf.Max(maxAgentRadius, 0.2f);
            LineOfSightRange = Mathf.Max(lineOfSightRange, 0);

            FieldStartPositionXZ = new Vector2(float.MinValue, float.MinValue);
            FieldEndPositionXZ = new Vector2(float.MaxValue, float.MaxValue);
        }
    }
    public enum Walkability : byte
    {
        Unwalkable,
        Walkable
    }

}
