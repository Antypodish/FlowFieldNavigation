
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
    
    public AgentUpdater(AgentDataContainer agentDataContainer)
    {
        _agentDataContainer = agentDataContainer;
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
                    data.DesiredDirection = 0;
                    data.Destination = agentPath.NewPath.Destination;
                    data.SetStatusBit(AgentStatus.Moving);
                    data.ClearStatusBit(AgentStatus.HoldGround);
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
        };
        movJob.Schedule(agentTransforms).Complete();

        //ROTATE
        AgentRotationUpdateJob rotateJob = new AgentRotationUpdateJob()
        {
            agentData = agentDataList,
        };
        rotateJob.Schedule(agentTransforms).Complete();
    }
}