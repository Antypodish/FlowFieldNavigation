
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
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;


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