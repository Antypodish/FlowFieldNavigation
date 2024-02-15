﻿using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace FlowFieldNavigation
{

    internal class SectorDebugMeshBuilder
    {
        FlowFieldNavigationManager _navigationManager;
        List<Mesh> _debugMeshes;
        bool _isCreated;
        internal SectorDebugMeshBuilder(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _debugMeshes = new List<Mesh>();
            _isCreated = false;
        }
        internal List<Mesh> GetDebugMesh()
        {
            if (!_isCreated) { Create(); }
            return _debugMeshes;
        }

        void Create()
        {
            _isCreated = true;
            int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
            int sectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount;
            const int maxRowEachMesh = 100;
            const int maxColEachMesh = 100;
            for (int r = 0; r < sectorMatrixRowAmount; r += maxRowEachMesh)
            {
                for (int c = 0; c < sectorMatrixColAmount; c += maxColEachMesh)
                {
                    int2 startIndex = new int2(c, r);
                    int2 endIndex = new int2(math.min(c + maxColEachMesh - 1, sectorMatrixColAmount - 1), math.min(r + maxRowEachMesh - 1, sectorMatrixRowAmount - 1));
                    int meshColAmount = endIndex.x - startIndex.x + 2;
                    int meshRowAmount = endIndex.y - startIndex.y + 2;
                    NativeArray<Vector3> verts = new NativeArray<Vector3>(meshColAmount * meshRowAmount, Allocator.Persistent);
                    NativeArray<int> trigs = new NativeArray<int>((meshColAmount - 1) * (meshRowAmount - 1) * 4, Allocator.Persistent);
                    int2 startTileIndex = FlowFieldUtilities.GetSectorStartIndex(startIndex, FlowFieldUtilities.SectorColAmount);
                    SectorMeshBuildJob tileMeshCalculation = new SectorMeshBuildJob()
                    {
                        SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
                        MeshStartPos = FlowFieldUtilities.IndexToStartPos(startTileIndex, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
                        TriangleSpatialHashGrid = _navigationManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
                        HeightMeshVerts = _navigationManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
                        MeshColCount = meshColAmount,
                        MeshRowCount = meshRowAmount,
                        Verts = verts,
                        Trigs = trigs,
                    };
                    tileMeshCalculation.Schedule().Complete();
                    if (verts.Length >= 3)
                    {
                        Mesh mesh = CreateMesh(verts, trigs);
                        _debugMeshes.Add(mesh);
                        verts.Dispose();
                        trigs.Dispose();
                    }
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

}