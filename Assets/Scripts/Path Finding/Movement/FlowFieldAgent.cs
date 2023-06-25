using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentController _controller;

    [HideInInspector] public int AgentDataIndex;
    [SerializeField] float _speed;
    private void Start()
    {
        _controller.Subscribe(this);
        _pathfindingManager.AgentDataContainer.Subscribe(this);
    }

    public void SetPath(Path path)
    {
        _pathfindingManager.AgentDataContainer.SetPath(AgentDataIndex, path);
    }
    public float GetSpeed() => _speed;
    public void SetSpeed(float newSpeed) { _speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }
}
