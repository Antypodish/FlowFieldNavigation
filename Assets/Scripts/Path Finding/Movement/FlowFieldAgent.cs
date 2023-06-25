using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Timeline;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentController _controller;
    [HideInInspector] public Vector3 Destination;
    [HideInInspector] public Vector2 Direction;
    
    public float Speed;
    public Path CurPath;
    public Path NewPath;
    private void Start()
    {
        _controller.Subscribe(this);
        _pathfindingManager.Subscribe(this);
    }
    private void Update()
    {
        if (NewPath != null)
        {
            if (NewPath.IsCalculated)
            {
                if(CurPath != null) { CurPath.Unsubscribe(); }
                NewPath.Subscribe();
                CurPath = NewPath;
                Destination = NewPath.Destination;
                Destination.y = transform.position.y;
                NewPath = null;
            }
        }
        if(CurPath != null)
        {
            if(Direction == Vector2.zero)
            {
                Vector3 destination = new Vector3(Destination.x, transform.position.y, Destination.z);
                transform.position = Vector3.MoveTowards(transform.position, destination, Speed * Time.deltaTime);
            }
            else
            {
                Vector3 direction = new Vector3(Direction.x, 0f, Direction.y);
                transform.position += direction * Speed * Time.deltaTime;
            }
        }
    }

    public void SetPath(Path path)
    {
        NewPath = path;
    }
}
