using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class AgentDirectionCalculator
{
    List<FlowFieldAgent> _agents;
    List<Path> _paths;
    NativeList<UnsafeList<DirCalcNode>> _jobData = new NativeList<UnsafeList<DirCalcNode>>(Allocator.Persistent);

    public AgentDirectionCalculator(List<FlowFieldAgent> agents)
    {
        _agents = agents;
        _paths = new List<Path>();
    }
    public void CalculateDirections()
    {
        SetContainers();
        SetDirCalcNodes();
        
        //HELPERS
        void SetContainers()
        {
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
            for (int i = 0; i < _agents.Count; i++)
            {
                FlowFieldAgent agent = _agents[i];
                Path path = agent.CurPath;
                if (path == null) { continue; }
                if (path.RoutineMark == -1)
                {
                    UnsafeList<DirCalcNode> newPathNodes = new UnsafeList<DirCalcNode>(0, Allocator.Persistent);
                    DirCalcNode newNode = new DirCalcNode()
                    {
                        agentIndex = i,
                        pos = agent.transform.position,
                        direction = 0,
                        local1d = 0,
                        sector1d = 0,
                    };
                    newPathNodes.Add(newNode);
                    path.RoutineMark = _paths.Count;
                    _paths.Add(path);
                    _jobData.Add(newPathNodes);
                }
                else
                {
                    UnsafeList<DirCalcNode> nodes = _jobData[path.RoutineMark];
                    DirCalcNode newNode = new DirCalcNode()
                    {
                        agentIndex = i,
                        pos = agent.transform.position,
                        direction = 0,
                        local1d = 0,
                        sector1d = 0,
                    };
                    nodes.Add(newNode);
                    _jobData[path.RoutineMark] = nodes;
                }
            }
        }
        void SetDirCalcNodes()
        {
            for (int i = 0; i < _jobData.Length; i++)
            {
                DirectionCalculationJob dirJob = new DirectionCalculationJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    Nodes = _jobData[i],
                    FlowField = _paths[i].FlowField,
                    SectorMarks = _paths[i].SectorMarks,
                };
                dirJob.Schedule(_jobData[i].Length, 64).Complete();
            }
            for (int i = 0; i < _jobData.Length; i++)
            {
                UnsafeList<DirCalcNode> nodes = _jobData[i];
                for (int j = 0; j < nodes.Length; j++)
                {
                    DirCalcNode node = nodes[j];
                    _agents[node.agentIndex].Direction = node.direction;
                }
            }
        }
    }
}
public struct DirCalcNode
{
    public float3 pos;
    public int agentIndex;
    public float2 direction;
    public ushort local1d;
    public ushort sector1d;
}