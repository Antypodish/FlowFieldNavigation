using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;

public class FlowFieldAgent : MonoBehaviour
{
    PathfindingManager _pathfindingManager;
    public float Speed;
    public float LandOffset;

    [HideInInspector] public int AgentDataIndex;
    [HideInInspector] public Transform Transform;

    private void Start()
    {
        _pathfindingManager = GameObject.FindObjectOfType<PathfindingManager>();
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
    public float GetSpeed() => Speed;
    public void SetSpeed(float newSpeed) { Speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }
}
