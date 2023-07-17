using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

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
    Image _selectionBox;

    public AgentControlSelector(Material selectedAgentMaterial, Material normalAgentMaterial, Image selectionBox)
    {
        _selectedAgents = new List<FlowFieldAgent>();
        _selectedAgentMaterial = selectedAgentMaterial;
        _normalAgentMaterial = normalAgentMaterial;
        _selectionBox = selectionBox;
        _selectionBox.rectTransform.sizeDelta = Vector3.zero;
    }

    public void StartSelection(Vector3 mousePosition)
    {
        _startMousePos = mousePosition;
    }
    public void ContinueSelection(Vector3 mousePosition)
    {
        //RESIZE RECTANGLE
        _selectionBox.transform.position = (_startMousePos + mousePosition) / 2;
        _selectionBox.rectTransform.sizeDelta = new Vector2(Mathf.Abs(_startMousePos.x - mousePosition.x), Mathf.Abs(_startMousePos.y - mousePosition.y));
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

        _selectionBox.rectTransform.sizeDelta = Vector3.zero;
    }
}