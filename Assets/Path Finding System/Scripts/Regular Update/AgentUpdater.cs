﻿
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

public class AgentUpdater
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public AgentUpdater(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
    }
    public void OnUpdate()
    {
        List<FlowFieldAgent> agents = _agentDataContainer.Agents;
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
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
                    data.Status |= AgentStatus.Moving;
                    agentPath.NewPath = null;
                    pathList[i] = agentPath;
                    agentDataList[i] = data;
                }
            }
        }
        //MOVE
        AgentMovementUpdateJob movJob = new AgentMovementUpdateJob()
        {
            DeltaTime = Time.deltaTime,
            AgentDataArray = agentDataList,
            AgentPositions = _agentDataContainer.AgentPositions,
        };
        movJob.Schedule(agentTransforms).Complete();
    }
}