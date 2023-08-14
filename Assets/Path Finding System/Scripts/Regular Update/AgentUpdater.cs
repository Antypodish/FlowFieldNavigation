
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;

public class AgentUpdater
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    UnsafeList<float> PathStopDistances;
    
    public AgentUpdater(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        PathStopDistances = new UnsafeList<float>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        PathStopDistances.Length = 1;
    }
    public void OnUpdate()
    {
        List<FlowFieldAgent> agents = _agentDataContainer.Agents;
        NativeArray<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        List<AgentPath> pathList = _agentDataContainer.Paths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        //REFRESH PATH
        for (int i = 0; i < agents.Count; i++)
        {
            AgentPath agentPath = pathList[i];
            AgentData data = agentDataList[i];
            if (agentPath.NewPath != null)
            {
                if (agentPath.NewPath.IsCalculated)
                {
                    if (agentPath.CurPath != null) { agentPath.CurPath.Unsubscribe(); }
                    agentPath.CurPath = agentPath.NewPath;
                    data.Destination = agentPath.NewPath.Destination;
                    data.SetStatusBit(AgentStatus.Moving);
                    data.ClearStatusBit(AgentStatus.HoldGround);
                    agentPath.NewPath = null;
                    pathList[i] = agentPath;
                    agentDataList[i] = data;
                }
            }
        }

        //PREPARE PathStoppedAgentCounts
        List<Path> producedPaths = _pathfindingManager.PathProducer.ProducedPaths;
        if(PathStopDistances.Length < producedPaths.Count)
        {
            PathStopDistances.Length = producedPaths.Count;
        }
        UnsafeListResetJob<float> resetJob = new UnsafeListResetJob<float>()
        {
            List = PathStopDistances,
        };
        resetJob.Schedule().Complete();
        for(int i = 0; i < agentDataList.Length; i++)
        {
            Path path = pathList[i].CurPath;
            if(path == null) { continue; }
            int pathId = path.Id;
            AgentData agentData = agentDataList[i];
            agentData.StopDistanceIndex = pathId;
            agentDataList[i] = agentData;
            PathStopDistances[pathId] += (agentData.Status & AgentStatus.Moving) == AgentStatus.Moving ? 0 : 1;
        }
        for (int i = 0; i < PathStopDistances.Length; i++)
        {
            float radius = 0.6f;
            float dist = PathStopDistances[i];
            if(dist == 0) { PathStopDistances[i] = radius; continue; }
            float trigNum = math.floor((dist-1) / 6);
            float trigRoot = (math.sqrt(8 * trigNum + 1) - 1) / 2;
            trigRoot = math.floor(trigRoot);
            PathStopDistances[i] = (trigRoot + 1) * radius * 2;
        }

        //MOVE
        AgentMovementUpdateJob movJob = new AgentMovementUpdateJob()
        {
            DeltaTime = Time.deltaTime,
            AgentDataArray = agentDataList,
            AgentPositions = _agentDataContainer.AgentPositions,
            PathStopDistances = PathStopDistances,
        };
        movJob.Schedule(agentTransforms).Complete();
    }
}