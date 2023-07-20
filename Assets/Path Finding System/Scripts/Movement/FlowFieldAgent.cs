using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;

    [HideInInspector] public int AgentDataIndex;
    [HideInInspector] public Transform Transform { get; private set; }

    [SerializeField] float _speed;
    private void Start()
    {
        _pathfindingManager.Subscribe(this);
        Transform = transform;
    }
    public void SetPath(Path path)
    {
        _pathfindingManager.SetPath(AgentDataIndex, path);
    }
    public Path GetPath()
    {
        return _pathfindingManager.GetPath(AgentDataIndex);
    }
    public float GetSpeed() => _speed;
    public void SetSpeed(float newSpeed) { _speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }
}
