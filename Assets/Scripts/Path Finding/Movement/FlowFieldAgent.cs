using Unity.VisualScripting;
using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentController _controller;
    public int AgentID;
    public float Speed;
    public Vector3 Destination;
    public Path CurPath;
    public Path NewPath;
    private void Start()
    {
        _controller.Subscribe(this);
    }
    private void Update()
    {
        if (NewPath != null)
        {
            if (NewPath.IsCalculated)
            {
                CurPath = NewPath;
                Destination = NewPath.Destination;
                Destination.y = transform.position.y;
                NewPath = null;
            }
        }
        if(CurPath != null)
        {
            Debug.Log(CurPath.Destination);
            _pathfindingManager.GetIndexAtPos(transform.position, out int local1d, out int sector1d);
            FlowData flow = CurPath.GetFlow(local1d, sector1d);
            Vector3 direction = Vector3.zero;
            if(flow == FlowData.LOS)
            {
                transform.position = Vector3.MoveTowards(transform.position, Destination, Speed * Time.deltaTime);
            }
            else
            {
                switch (flow)
                {
                    case FlowData.N:
                        direction = new Vector3(0f, 0f, 1f);
                        break;
                    case FlowData.E:
                        direction = new Vector3(1f, 0f, 0f);
                        break;
                    case FlowData.S:
                        direction = new Vector3(0f, 0f, -1f);
                        break;
                    case FlowData.W:
                        direction = new Vector3(-1f, 0f, 0f);
                        break;
                    case FlowData.NE:
                        direction = new Vector3(1f, 0f, 1f);
                        break;
                    case FlowData.SE:
                        direction = new Vector3(1f, 0f, -1f);
                        break;
                    case FlowData.SW:
                        direction = new Vector3(-1f, 0f, -1f);
                        break;
                    case FlowData.NW:
                        direction = new Vector3(-1f, 0f, 1f);
                        break;
                }
                direction = direction.normalized;
                transform.position = Vector3.MoveTowards(transform.position, transform.position + direction, Speed * Time.deltaTime);
            }
        }
    }

    public void SetPath(Path path)
    {
        if(CurPath == null)
        {
            CurPath = path;
            Destination = path.Destination;
            Destination.y = transform.position.y;
            return;
        }
        NewPath = path;
    }
}
