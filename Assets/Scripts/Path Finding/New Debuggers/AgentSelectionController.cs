using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AgentSelectionController : MonoBehaviour
{
    public List<FlowFieldAgent> SelectedAgents;
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] Material _normalAgentMaterial;
    [SerializeField] Material _selectedAgentMaterial;
    [SerializeField] Image _selectionBox;
    [SerializeField] PathDebuggingController _pathDebuggingController;

    AgentBoundSelector _agentSelector;
    ControllerState _state;
    private void Start()
    {
        _agentSelector = new AgentBoundSelector(_selectionBox);
        _state = ControllerState.Single;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            _state = _state == ControllerState.Single ? ControllerState.Multi : ControllerState.Single;
            _agentSelector.ForceStopSelection();
        }
        if (_state == ControllerState.Single)
        {
            if (Input.GetMouseButtonDown(0))
            {
                DeselectAllAgents();
                _agentSelector.SelectPointedObject(SelectedAgents);
                SetMaterialOfAgents(SelectedAgents, _selectedAgentMaterial);
                if(SelectedAgents.Count == 1) { _pathDebuggingController.SetAgentToDebug(SelectedAgents[0]); }
                else { _pathDebuggingController.SetAgentToDebug(null); }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                _agentSelector.StartBoxSelection(Input.mousePosition);
            }
            if (Input.GetMouseButton(0))
            {
                _agentSelector.ContinueBoxSelection(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                DeselectAllAgents();
                _agentSelector.GetAgentsInBox(Input.mousePosition, Camera.main, _pathfindingManager.GetAllAgents(), SelectedAgents);
                SetMaterialOfAgents(SelectedAgents, _selectedAgentMaterial);
                _pathDebuggingController.SetAgentToDebug(null);
            }
        }
        if (Input.GetMouseButtonDown(1) && SelectedAgents.Count != 0)
        {
            SetDestination();
        }
    }
    void SetDestination()
    {
        List<FlowFieldAgent> agents = SelectedAgents;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Vector3 destination = hit.point;
            NativeArray<Vector3> positions = new NativeArray<Vector3>(agents.Count, Allocator.Persistent);
            for (int i = 0; i < agents.Count; i++)
            {
                positions[i] = agents[i].transform.position;
            }
            Path newPath = _pathfindingManager.SetDestination(positions, destination);
            if (newPath == null) { positions.Dispose(); return; }
            for (int i = 0; i < agents.Count; i++)
            {
                agents[i].SetPath(newPath);
            }
        }
    }
    void DeselectAllAgents()
    {
        for(int i = 0; i < SelectedAgents.Count; i++)
        {
            SelectedAgents[i].GetComponent<MeshRenderer>().material = _normalAgentMaterial;
        }
        SelectedAgents.Clear();
    }
    void SetMaterialOfAgents(List<FlowFieldAgent> agents, Material mat)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].GetComponent<MeshRenderer>().material = mat;
        }
    }
    enum ControllerState : byte
    {
        Single,
        Multi
    };
}