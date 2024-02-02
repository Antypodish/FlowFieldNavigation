using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
internal class PortalDebugMeshBuilder
{
    PathfindingManager _pathfindingManager;
    List<Mesh> _debugMeshes;
    bool _isCreated;

    internal PortalDebugMeshBuilder(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _isCreated = false;
        _debugMeshes = new List<Mesh>();
    }
    internal List<Mesh> GetDebugMeshes(int offset)
    {
        if (!_isCreated) { Create(offset); }
        return _debugMeshes;
    }
    void Create(int offset)
    {
        _isCreated = true;

        float3 portalDebugPrimitiveSize = new float3(0.25f, 0.25f, 0.25f);
        NativeArray<WindowNode> windowNodes = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(offset).WindowNodes;
        NativeArray<PortalNode> portalNodes = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(offset).PortalNodes;
        NativeList<int> alivePortals = new NativeList<int>(Allocator.TempJob);
        AlivePortalCalculationJob alivePortalJob = new AlivePortalCalculationJob()
        {
            PortalNodes = portalNodes,
            WindowNodes = windowNodes,
            AlivePortalIndicies = alivePortals,
        };
        alivePortalJob.Schedule().Complete();

        const int cubeVertCount = 8;
        const int maxPortalSlcieSize = 20000 / cubeVertCount;

        NativeList<Vector3> verts = new NativeList<Vector3>(Allocator.TempJob);
        NativeList<int> trigs = new NativeList<int>(Allocator.TempJob);
        for(int i = 0; i < alivePortals.Length; i+=maxPortalSlcieSize)
        {
            int sliceSize = Math.Min(maxPortalSlcieSize, alivePortals.Length - i);
            NativeSlice<int> alivePortalSlice = new NativeSlice<int>(alivePortals.AsArray(), i, sliceSize);

            PortalDebugMeshBuildJob meshBuild = new PortalDebugMeshBuildJob()
            {
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                TileSize = FlowFieldUtilities.TileSize,
                TriangleSpatialHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
                HeightMeshVerts = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
                PortalNodes = portalNodes,
                AlivePortalIndicies = alivePortalSlice,
                PortalDebugPrimitiveSize = portalDebugPrimitiveSize,
                Trigs = trigs,
                Verts = verts,
            };
            meshBuild.Schedule().Complete();
            Mesh mesh = CreateMesh(verts.AsArray(), trigs.AsArray());
            _debugMeshes.Add(mesh);
            verts.Clear();
            trigs.Clear();
        }
        verts.Dispose();
        trigs.Dispose();
        alivePortals.Dispose();

    }
    Mesh CreateMesh(NativeArray<Vector3> verts, NativeArray<int> trigs)
    {
        Mesh mesh = new Mesh();
        mesh.Clear();
        mesh.vertices = verts.ToArray();
        mesh.triangles = trigs.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
}
