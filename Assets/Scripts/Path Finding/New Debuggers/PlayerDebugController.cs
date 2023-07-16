using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class PlayerDebugController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] Material _normalAgentMaterial;
    [SerializeField] Material _selectedAgentMaterial;

    AgentControlSelector _agentControlSelector;

    private void Start()
    {
        _agentControlSelector = new AgentControlSelector(_selectedAgentMaterial, _normalAgentMaterial);
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _agentControlSelector.StartSelection(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _agentControlSelector.EndSelection(Input.mousePosition, Camera.main, _pathfindingManager.GetAllAgents());
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
}