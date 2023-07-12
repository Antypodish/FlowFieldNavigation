using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Jobs;

public class AgentDirectionCalculator
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    NativeList<AgentMovementData> _agentMovementData;

    public AgentDirectionCalculator(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        _agentMovementData = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
    }
    public void CalculateDirections()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        List<AgentPath> pathList = _agentDataContainer.Paths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        //CLEAR
        _agentMovementData.Clear();
        _agentMovementData.Length = agentDataList.Length;

        //FILL
        for (int i = 0; i < agentDataList.Length; i++)
        {
            Path curPath = pathList[i].CurPath;

            if(curPath == null)
            {
                AgentMovementData data = new AgentMovementData()
                {
                    Position = 0,
                    Direction = 0,
                    Local1d = 0,
                    Sector1d = 0,
                    OutOfFieldFlag = false,
                };
                _agentMovementData[i] = data;
            }
            else
            {
                AgentMovementData data = new AgentMovementData()
                {
                    Position = 0,
                    Direction = 0,
                    Local1d = 0,
                    Sector1d = 0,
                    OutOfFieldFlag = false,
                    FlowField = curPath.FlowField,
                    SectorToPicked = curPath.SectorToPicked,
                };
                _agentMovementData[i] = data;
            }
        }

        //SCHEDULE JOB
        AgentMovementDataCalculationJob movementDataJob = new AgentMovementDataCalculationJob()
        {
            TileSize = _pathfindingManager.TileSize,
            SectorColAmount = _pathfindingManager.SectorTileAmount,
            SectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount,
            AgentMovementData = _agentMovementData,
        };
        movementDataJob.Schedule(agentTransforms).Complete();

        //SEND DIRECTIONS
        for(int i = 0; i < _agentMovementData.Length; i++)
        {
            _agentDataContainer.SetDirection(i, _agentMovementData[i].Direction);
        }
        sw.Stop();
        UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
    }
}
public struct AgentMovementData
{
    public float3 Position;
    public float2 Direction;
    public ushort Local1d;
    public ushort Sector1d;
    public bool OutOfFieldFlag;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<int> SectorToPicked;
}