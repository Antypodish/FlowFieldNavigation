using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

namespace FlowFieldNavigation
{
    public class FlowFieldNavigationInterface
    {
        FlowFieldNavigationManager _navigationManager;
        SimulationStartInputHandler _simStartInputHandler;
        NativeList<AgentReferance> _extracedAgentReferances;
        public FlowFieldNavigationInterface(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _simStartInputHandler = new SimulationStartInputHandler();
            _extracedAgentReferances = new NativeList<AgentReferance>(Allocator.Persistent);
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
        public void SetDestination(List<FlowFieldAgent> agents, Vector3 target, float range = 0)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            if (agents.Count == 0) { return; }
            GetAgentReferances(agents, _extracedAgentReferances);
            if(_extracedAgentReferances.Length == 0) { return; }
            _navigationManager.RequestAccumulator.RequestPath(_extracedAgentReferances.AsArray(), target, range);
        }
        public void SetDestination(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent, float range = 0)
        {
            if (!_navigationManager.SimulationStarted || agents.Count == 0 || targetAgent == null) { return; }
            GetAgentReferances(agents, _extracedAgentReferances);
            AgentReferance targetAgentRef = targetAgent.AgentReferance;
            if (_extracedAgentReferances.Length == 0 || !targetAgentRef.IsValid()) { return; }
            _navigationManager.RequestAccumulator.RequestPath(_extracedAgentReferances.AsArray(), targetAgentRef, range);
        }
        public int GetPathIndex(FlowFieldAgent agent)
        {
            AgentReferance agentRef = agent.AgentReferance;
            if (!_navigationManager.SimulationStarted ||!agentRef.IsValid()) { return -1; }
            return _navigationManager.AgentDataReadSystem.ReadCurPathIndex(agentRef);
        }
        public void SetObstacle(FlowFieldDynamicObstacle dynamicObstacle)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            if(dynamicObstacle.ObstacleIndex != -1) { return; }
            dynamicObstacle._navManager = _navigationManager;
            dynamicObstacle.ObstacleIndex = _navigationManager.RequestAccumulator.HandleObstacleRequestAndGetIndex(dynamicObstacle.GetObstacleRequest());
        }
        public void RemoveObstacle(FlowFieldDynamicObstacle dynamicObstacle)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            int obstacleIndex = dynamicObstacle.ObstacleIndex;
            if (obstacleIndex == -1) { return; }
            dynamicObstacle.ObstacleIndex = -1;
            _navigationManager.RequestAccumulator.HandleObstacleRemovalRequest(obstacleIndex);
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
        public AgentStatus GetAgentStateFlags(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return 0; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return 0; }
            return _navigationManager.AgentDataReadSystem.ReadAgentStatusFlags(agentReferance);
        }
        public void SetSpeed(FlowFieldAgent agent, float speed)
        {
            if (!_navigationManager.SimulationStarted) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            _navigationManager.RequestAccumulator.RequestSetSpeed(agentReferance, speed);
        }
        public float GetSpeed(FlowFieldAgent agent)
        {
            if (!_navigationManager.SimulationStarted) { return 0; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return 0; }
            return _navigationManager.AgentDataReadSystem.ReadAgentSpeed(agentReferance);
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
            return _navigationManager.AgentDataReadSystem.ReadAgentNavigationMovementFlag(agentReferance);
        }
        public int GetAgentCount()
        {
            if (!_navigationManager.SimulationStarted) { return 0; }
            return _navigationManager.AgentDataContainer.AgentDataList.Length;
        }


        void GetAgentReferances(List<FlowFieldAgent> agents, NativeList<AgentReferance> outputListToAddAgentReferances)
        {
            outputListToAddAgentReferances.Clear();
            for(int i = 0; i < agents.Count; i++)
            {
                FlowFieldAgent agent = agents[i];
                if(agent == null) { continue; }
                AgentReferance referance = agent.AgentReferance;
                if (referance.IsValid())
                {
                    outputListToAddAgentReferances.Add(referance);
                }
            }
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
