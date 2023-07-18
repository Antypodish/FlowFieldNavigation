using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class AgentBoundSelector
{
    Vector3 _startMousePos;
    Image _selectionBox;

    public AgentBoundSelector(Image selectionBox)
    {
        _selectionBox = selectionBox;
        _selectionBox.rectTransform.sizeDelta = Vector3.zero;
    }
    public void SelectPointedObject(List<FlowFieldAgent> selected)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            FlowFieldAgent agent = hit.collider.GetComponent<FlowFieldAgent>();
            if (agent != null) { selected.Add(agent); }
        }
    }
    public void StartBoxSelection(Vector3 mousePosition)
    {
        _startMousePos = mousePosition;
    }
    public void ContinueBoxSelection(Vector3 mousePosition)
    {
        //RESIZE RECTANGLE
        _selectionBox.transform.position = (_startMousePos + mousePosition) / 2;
        _selectionBox.rectTransform.sizeDelta = new Vector2(Mathf.Abs(_startMousePos.x - mousePosition.x), Mathf.Abs(_startMousePos.y - mousePosition.y));
    }
    public void GetAgentsInBox(Vector3 mousePosition, Camera cam, List<FlowFieldAgent> allAgents, List<FlowFieldAgent> selectedAgents)
    {
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
        selectedAgents.Capacity = boundAgentIndicies.Length;
        for (int i = 0; i < boundAgentIndicies.Length; i++)
        {
            FlowFieldAgent agent = allAgents[boundAgentIndicies[i]];
            selectedAgents.Add(agent);
        }
        _selectionBox.rectTransform.sizeDelta = Vector3.zero;
    }
    public void ForceStopSelection()
    {
        _selectionBox.rectTransform.sizeDelta = Vector3.zero;
    }
    
}