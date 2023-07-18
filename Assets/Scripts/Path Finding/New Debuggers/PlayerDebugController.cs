using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDebugController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] Material _normalAgentMaterial;
    [SerializeField] Material _selectedAgentMaterial;
    [SerializeField] Image _selectionBox;

    AgentControlSelector _agentControlSelector;
    ControllerState _state;

    private void Start()
    {
        _agentControlSelector = new AgentControlSelector(_selectedAgentMaterial, _normalAgentMaterial, _selectionBox);
        _state = ControllerState.Single;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            _state = _state == ControllerState.Single ? ControllerState.Multi : ControllerState.Single;
            _agentControlSelector.ForceStopSelection();
        }
        if (_state == ControllerState.Single)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _agentControlSelector.SelectAgentPointed();
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                _agentControlSelector.StartSelection(Input.mousePosition);
            }
            if (Input.GetMouseButton(0))
            {
                _agentControlSelector.ContinueSelection(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _agentControlSelector.EndSelection(Input.mousePosition, Camera.main, _pathfindingManager.GetAllAgents());
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            SetDestination();
        }
    }
    void SetDestination()
    {
        List<FlowFieldAgent> agents = _agentControlSelector.SelectedAgents;
        float tileSize = _pathfindingManager.TileSize;
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

    enum ControllerState : byte
    {
        Single,
        Multi
    };
}