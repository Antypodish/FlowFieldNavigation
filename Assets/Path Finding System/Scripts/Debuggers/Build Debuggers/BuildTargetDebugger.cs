using UnityEngine;
using static UnityEditor.PlayerSettings;

public class BuildTargetDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _mesh;
    Material _mat;

    public BuildTargetDebugger(PathfindingManager pathfindingManager, Material _targetDebugMesh)
    {
        _pathfindingManager = pathfindingManager;
        _mat = _targetDebugMesh;
    }
    public void Debug(FlowFieldAgent agent, float offset)
    {
        if (_mesh == null) { ConfigMesh(); }
        if (agent == null) { return; }

        Path path = agent.GetPath();
        if (path == null) { return; }

        Vector2 destination = path.Destination;

        Graphics.DrawMesh(_mesh, new Vector3(destination.x, offset, destination.y), Quaternion.identity, _mat, 1);

    }
    void ConfigMesh()
    {
        float tileSize = 0.5f;
        _mesh = new Mesh();
        _mesh.name = "Field Mesh";

        Vector3[] verticies = new Vector3[4];
        verticies[0] = Vector3.zero;
        verticies[1] = new Vector3(0, 0, tileSize);
        verticies[2] = new Vector3(tileSize, 0, tileSize);
        verticies[3] = new Vector3(tileSize, 0, 0);

        int[] trigs = new int[6];
        trigs[0] = 0;
        trigs[1] = 1;
        trigs[2] = 3;
        trigs[3] = 1;
        trigs[4] = 2;
        trigs[5] = 3;

        _mesh.Clear();
        _mesh.vertices = verticies;
        _mesh.triangles = trigs;
        _mesh.RecalculateNormals();
    }
}
