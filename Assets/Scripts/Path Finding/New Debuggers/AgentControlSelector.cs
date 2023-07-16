using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class AgentControlSelector
{
    public List<FlowFieldAgent> SelectedAgents
    {
        get { return _selectedAgents; }
    }

    List<FlowFieldAgent> _selectedAgents;
    Material _selectedAgentMaterial;
    Material _normalAgentMaterial;
    Vector3 _startMousePos;

    public AgentControlSelector(Material selectedAgentMaterial, Material normalAgentMaterial)
    {
        _selectedAgents = new List<FlowFieldAgent>();
        _selectedAgentMaterial = selectedAgentMaterial;
        _normalAgentMaterial = normalAgentMaterial;
    }

    public void StartSelection(Vector3 mousePosition)
    {
        _startMousePos = mousePosition;
    }
    public void EndSelection(Vector3 mousePosition, Camera cam, List<FlowFieldAgent> allAgents)
    {
        //UNSELECT OLD
        for (int i = 0; i < _selectedAgents.Count; i++)
        {
            _selectedAgents[i].GetComponent<MeshRenderer>().material = _normalAgentMaterial;
        }
        _selectedAgents.Clear();

        //GET SCREEN POSITIONS
        NativeArray<float3> sceenPositions = new NativeArray<float3>(allAgents.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < allAgents.Count; i++)
        {
            sceenPositions[i] = cam.WorldToScreenPoint(allAgents[i].transform.position);
        }

        //GET BOUND AGENTS
        NativeList<int> boundAgentIndicies = new NativeList<int>(Allocator.TempJob);
        AgentIndiciesBetweenBoundsJob boundJob = new AgentIndiciesBetweenBoundsJob()
        {
            Bound1 = _startMousePos,
            Bound2 = mousePosition,
            ScreenPositions = sceenPositions,
            AgentIndiciesBetweenBounds = boundAgentIndicies,
        };
        boundJob.Schedule().Complete();

        //SET SELECTED AGENTS
        SelectedAgents.Capacity = boundAgentIndicies.Length;
        for (int i = 0; i < boundAgentIndicies.Length; i++)
        {
            FlowFieldAgent agent = allAgents[boundAgentIndicies[i]];
            _selectedAgents.Add(agent);
            agent.gameObject.GetComponent<MeshRenderer>().material = _selectedAgentMaterial;
        }
    }
}