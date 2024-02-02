using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
internal class GenericDebugTileMeshBuilder
{
    PathfindingManager _pathfindingManager;
    List<Mesh> _debugMeshes;
    bool _isCreated;

    int _colCount;
    int _rowCount;
    float _tileSize;
    float2 _gridStartPos;
    internal GenericDebugTileMeshBuilder(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _debugMeshes = new List<Mesh>();
        _isCreated = false;
        _rowCount = 0;
        _colCount = 0;
        _tileSize = 0;
        _gridStartPos = 0;
    }
    internal List<Mesh> GetDebugMesh(int colCount, int rowCount, float tileSize, float2 gridStartPos)
    {
        if (!_isCreated || colCount != _colCount || rowCount != _rowCount || tileSize != _tileSize || !gridStartPos.Equals(_gridStartPos))
        {
            _colCount = colCount;
            _rowCount = rowCount;
            _tileSize = tileSize;
            _gridStartPos = gridStartPos;
            Create();
        }
        return _debugMeshes;
    }

    void Create()
    {
        _isCreated = true;
        const int maxRowEachMesh = 100;
        const int maxColEachMesh = 100;
        for (int r = 0; r < _rowCount; r += maxRowEachMesh)
        {
            for (int c = 0; c < _colCount; c += maxColEachMesh)
            {
                int2 startIndex = new int2(c, r);
                int2 endIndex = new int2(math.min(c + maxColEachMesh - 1, _colCount - 1), math.min(r + maxRowEachMesh - 1, _rowCount - 1));
                int meshColAmount = endIndex.x - startIndex.x + 2;
                int meshRowAmount = endIndex.y - startIndex.y + 2;
                NativeArray<Vector3> verts = new NativeArray<Vector3>(meshColAmount * meshRowAmount, Allocator.Persistent);
                NativeArray<int> trigs = new NativeArray<int>((meshColAmount - 1) * (meshRowAmount - 1) * 4, Allocator.Persistent);
                SectorMeshBuildJob tileMeshCalculation = new SectorMeshBuildJob()
                {
                    SectorSize = _tileSize,
                    MeshStartPos = _gridStartPos + startIndex * new float2(_tileSize, _tileSize),
                    TriangleSpatialHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
                    HeightMeshVerts = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
                    MeshColCount = meshColAmount,
                    MeshRowCount = meshRowAmount,
                    Verts = verts,
                    Trigs = trigs,
                };
                tileMeshCalculation.Schedule().Complete();
                Mesh mesh = CreateMesh(verts, trigs);
                _debugMeshes.Add(mesh);
                verts.Dispose();
                trigs.Dispose();
            }
        }
    }

    Mesh CreateMesh(NativeArray<Vector3> verts, NativeArray<int> trigs)
    {
        Mesh mesh = new Mesh();
        mesh.Clear();
        mesh.vertices = verts.ToArray();
        mesh.triangles = new int[0];
        mesh.RecalculateNormals();
        mesh.SetIndices(trigs.ToArray(), MeshTopology.Lines, 0);
        return mesh;
    }
}
