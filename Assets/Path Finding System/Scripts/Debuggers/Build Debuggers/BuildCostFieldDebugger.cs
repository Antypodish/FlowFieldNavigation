using Mono.Cecil;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class BuildCostFieldDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _mesh;
    Material _meshMat;

    public BuildCostFieldDebugger(PathfindingManager pathfindingManager, Material _costDebugMaterial)
    {
        _pathfindingManager = pathfindingManager;
        _meshMat = _costDebugMaterial;
    }

    public void Debug(FlowFieldAgent agent, float offset)
    {
        if(_mesh == null) { ConfigMesh(); }
        if(agent == null) { return; }
        float tileSize = _pathfindingManager.TileSize;
        int sectorColAmount = _pathfindingManager.SectorColAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;

        Path path = agent.GetPath();
        if(path == null) { return; }
        CostField costField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset);
        UnsafeList<int> sectorToPicked = path.SectorToPicked;
        NativeArray<UnsafeList<byte>> costs = costField.CostsL;
        for(int i = 0; i < sectorToPicked.Length; i++)
        {
            if(sectorToPicked[i] == 0) { continue; }
            UnsafeList<byte> pickedSector = costs[i];
            int2 sectorIndex2d = new int2(i % sectorMatrixColAmount, i / sectorMatrixColAmount);
            int2 sectorStartIndex = sectorIndex2d * sectorColAmount;
            for (int j = 0; j < pickedSector.Length; j++)
            {
                byte cost = pickedSector[j];
                if (cost == byte.MaxValue) { continue; }
                int2 local2d = new int2(j % sectorColAmount, j / sectorColAmount);
                int2 general2d = local2d + sectorStartIndex;
                Vector3 pos = new Vector3(general2d.x * tileSize, offset, general2d.y * tileSize);
                Graphics.DrawMesh(_mesh, pos, Quaternion.identity, _meshMat, 1);
            }
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