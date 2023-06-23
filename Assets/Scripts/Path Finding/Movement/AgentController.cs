using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class AgentController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    List<FlowFieldAgent> _agents;
    private void Awake()
    {
        _agents = new List<FlowFieldAgent>();
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 destination = hit.point;
                NativeArray<Vector3> positions = new NativeArray<Vector3>(_agents.Count, Allocator.Persistent);
                for(int i = 0; i < positions.Length; i++)
                {
                    positions[i] = _agents[i].transform.position;
                }
                Path newPath = _pathfindingManager.SetDestination(positions, destination);
                for(int i = 0; i < _agents.Count; i++)
                {
                    _agents[i].SetPath(newPath);
                }
            }
        }
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        _agents.Add(agent);
    }
}
