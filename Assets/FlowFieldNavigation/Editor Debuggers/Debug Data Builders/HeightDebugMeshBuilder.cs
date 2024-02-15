using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace FlowFieldNavigation
{

    internal class HeightDebugMeshBuilder
    {
        FlowFieldNavigationManager _navigationManager;
        List<Mesh> _debugMeshes;
        bool _isCreated;

        internal HeightDebugMeshBuilder(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _debugMeshes = new List<Mesh>();
            _isCreated = false;
        }

        internal List<Mesh> GetHeightDebugMeshes()
        {
            if (!_isCreated) { CreateTileIslandDebugMesh(); }
            return _debugMeshes;
        }
        void CreateTileIslandDebugMesh()
        {
            _isCreated = true;
            NativeArray<float3> heightMeshVerts = _navigationManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray();
            NativeArray<int> heightMeshTrigs = _navigationManager.FieldDataContainer.HeightMeshGenerator.Triangles.AsArray();
            const int maxTrigPerMesh = 60000;
            NativeList<Vector3> verts = new NativeList<Vector3>(Allocator.TempJob);
            NativeList<int> trigs = new NativeList<int>(Allocator.TempJob);
            for (int i = 0; i < heightMeshTrigs.Length; i += maxTrigPerMesh)
            {
                HeightDebugMeshBuildJob debugMeshBuild = new HeightDebugMeshBuildJob()
                {
                    StartTrig = i,
                    TrigCount = math.min(maxTrigPerMesh, heightMeshTrigs.Length - i),
                    HeightMeshTrigs = heightMeshTrigs,
                    HeightMeshVerts = heightMeshVerts,
                    Verts = verts,
                    Trigs = trigs,
                };
                debugMeshBuild.Schedule().Complete();
                if (verts.Length >= 3)
                {
                    Mesh mesh = CreateMesh(verts.AsArray(), trigs.AsArray());
                    _debugMeshes.Add(mesh);
                    verts.Clear();
                    trigs.Clear();
                }
            }
            verts.Dispose();
            trigs.Dispose();
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

}