using Mono.Cecil;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class CostFieldDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _mesh;
    Material _meshMat;

    public CostFieldDebugger(PathfindingManager pathfindingManager, Material _costDebugMaterial)
    {
        _pathfindingManager = pathfindingManager;
        _meshMat = _costDebugMaterial;
    }

    public void Debug(FlowFieldAgent agent)
    {
        if(_mesh == null) { ConfigMesh(); }
        if(agent == null) { return; }
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = 0.001f;
        int fieldColAmount = _pathfindingManager.ColumnAmount;

        Path path = agent.GetPath();
        if(path == null) { return; }
        CostField costField = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(path.Offset);
        NativeArray<byte> costs = costField.CostsG;
        for(int i = 0; i < costs.Length; i++)
        {
            byte cost = costs[i];
            if(cost == 1) { continue; }
            int col = i % fieldColAmount;
            int row = i / fieldColAmount;
            Vector3 pos = new Vector3(col * tileSize, yOffset, row * tileSize);
            Graphics.DrawMesh(_mesh, pos, Quaternion.identity, _meshMat, 1);
        }
    }
    void ConfigMesh()
    {
        float tileSize = _pathfindingManager.TileSize;
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