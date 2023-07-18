using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

internal class BuildLOSDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _mesh;
    Material _mat;
    public BuildLOSDebugger(PathfindingManager pathfindingManager, Material _losDebugMaterial)
    {
        _pathfindingManager = pathfindingManager;
        _mat = _losDebugMaterial;
    }

    public void Debug(FlowFieldAgent agent, float offset)
    {
        if (_mesh == null) { ConfigMesh(); }
        if (agent == null) { return; }
        float tileSize = _pathfindingManager.TileSize;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount * _pathfindingManager.SectorTileAmount;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;

        Path path = agent.GetPath();
        if (path == null) { return; }
        UnsafeList<int> sectorToPicked = path.SectorToPicked;
        NativeList<IntegrationTile> integrationField = path.IntegrationField;
        for (int i = 0; i < sectorToPicked.Length; i++)
        {
            int sectorIndex = i;
            int pickedIndex = sectorToPicked[sectorIndex];
            if (pickedIndex == 0) { continue; }

            int2 sectorIndex2d = new int2(i % sectorMatrixColAmount, i / sectorMatrixColAmount);
            int2 sectorStartIndex = sectorIndex2d * sectorColAmount;
            for (int j = pickedIndex; j < pickedIndex + sectorTileAmount; j++)
            {
                if (integrationField[j].Mark != IntegrationMark.LOSPass) { continue; }
                int local1d = j - pickedIndex;
                int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                int2 general2d = local2d + sectorStartIndex;
                Vector3 pos = new Vector3(general2d.x * tileSize, offset, general2d.y * tileSize);
                Graphics.DrawMesh(_mesh, pos, Quaternion.identity, _mat, 1);
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
