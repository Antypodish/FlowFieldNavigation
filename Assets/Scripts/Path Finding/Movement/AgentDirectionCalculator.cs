using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class AgentDirectionCalculator
{
    AgentDataContainer _agentDataContainer;
    List<FlowFieldAgent> _agents;
    List<Path> _paths;
    NativeList<UnsafeList<AgentMovementData>> _jobData = new NativeList<UnsafeList<AgentMovementData>>(Allocator.Persistent);
    NativeList<AgentMovementData> _agentMovementData;

    public AgentDirectionCalculator(AgentDataContainer agentDataContainer)
    {
        _agentDataContainer = agentDataContainer;
        _paths = new List<Path>();
        _jobData = new NativeList<UnsafeList<AgentMovementData>>(Allocator.Persistent);
        _agents = agentDataContainer.Agents;
        _agentMovementData = new NativeList<AgentMovementData>(_agents.Count, Allocator.Persistent);
    }
    public void CalculateDirections()
    {
        NativeList<AgentData> agentDatas = _agentDataContainer.AgentDatas;
        List<AgentPaths> agentPaths = _agentDataContainer.AgentPaths;
        List<Transform> agentTransforms = _agentDataContainer.AgentTransforms;

        SetContainers();
        SetAgentMovementData();
        SendAgentDirections();

        //HELPERS
        void SetContainers()
        {
            //PREPARE CONTAINER SIZES
            _agentMovementData.Capacity = _agents.Count;
            _agentMovementData.Length = _agents.Count;
            for (int i = 0; i < _paths.Count; i++)
            {
                _paths[i].RoutineMark = -1;
            }
            _paths.Clear();
            for (int i = 0; i < _jobData.Length; i++)
            {
                _jobData[i].Dispose();
            }
            _jobData.Clear();

            //PREPARE CONTAINER CONTENT
            
            for (int i = 0; i < _agents.Count; i++)
            {
                int agentIndex = _agents[i].AgentDataIndex;
                Path agentPath = agentPaths[agentIndex].CurPath;
                Transform agentTransform = agentTransforms[agentIndex];
                AgentData agentData = agentDatas[agentIndex];

                if (agentPath == null) { continue; }
                if (agentPath.RoutineMark == -1)
                {
                    UnsafeList<AgentMovementData> movementData = new UnsafeList<AgentMovementData>(0, Allocator.Persistent);
                    AgentMovementData newData = new AgentMovementData()
                    {
                        agentIndex = i,
                        pos = agentTransform.position,
                        direction = agentData.Direction,
                        local1d = 0,
                        sector1d = 0,
                    };
                    movementData.Add(newData);
                    agentPath.RoutineMark = _paths.Count;
                    _paths.Add(agentPath);
                    _jobData.Add(movementData);
                }
                else
                {
                    UnsafeList<AgentMovementData> movementData = _jobData[agentPath.RoutineMark];
                    AgentMovementData newData = new AgentMovementData()
                    {
                        agentIndex = i,
                        pos = agentTransform.position,
                        direction = agentData.Direction,
                        local1d = 0,
                        sector1d = 0,
                    };
                    movementData.Add(newData);
                    _jobData[agentPath.RoutineMark] = movementData;
                }
            }
        }
        void SetAgentMovementData()
        {
            //SCHEDULE JOBS
            for (int i = 0; i < _jobData.Length; i++)
            {
                AgentMovementDataCalculationJob dirJob = new AgentMovementDataCalculationJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    AgentMovementData = _jobData[i],
                    FlowField = _paths[i].FlowField,
                    SectorMarks = _paths[i].SectorMarks,
                };
                dirJob.Schedule(_jobData[i].Length, 64).Complete();
            }

            //TRANSFER JOB RESULTS TO THE BUFFER
            for (int i = 0; i < _jobData.Length; i++)
            {
                UnsafeList<AgentMovementData> nodes = _jobData[i];
                for (int j = 0; j < nodes.Length; j++)
                {
                    AgentMovementData node = nodes[j];
                    _agentMovementData[node.agentIndex] = node;
                }
            }
        }
        void SendAgentDirections()
        {
            for(int i = 0; i < _agentMovementData.Length; i++)
            {
                AgentData data = agentDatas[i];
                data.Direction = _agentMovementData[i].direction;
                agentDatas[i] = data;
            }
        }
    }
}
public struct AgentMovementData
{
    public float3 pos;
    public int agentIndex;
    public float2 direction;
    public ushort local1d;
    public ushort sector1d;
}