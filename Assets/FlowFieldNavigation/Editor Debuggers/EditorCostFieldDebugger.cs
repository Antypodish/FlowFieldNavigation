#if (UNITY_EDITOR) 

using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

internal class EditorCostFieldDebugger
{
    PathfindingManager _pathfindingManager;

    Mesh _debugMesh;
    Vector3[] _debugVerticies;
    int[] _debugTriangles;

    internal EditorCostFieldDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;

        //configure debug mesh
        float tileSize = FlowFieldUtilities.TileSize;
        _debugMesh = new Mesh();
        _debugVerticies = new Vector3[4];
        _debugTriangles = new int[6];
        SetVerticies();
        SetTriangles();
        UpdateMesh();

        //HELPERS
        void SetVerticies()
        {
            _debugVerticies[0] = new Vector3(0, 0, tileSize);
            _debugVerticies[1] = new Vector3(tileSize, 0, 0);
            _debugVerticies[2] = new Vector3(0, 0, 0);
            _debugVerticies[3] = new Vector3(tileSize, 0, tileSize);

            _debugMesh.vertices = _debugVerticies;
        }
        void SetTriangles()
        {
            _debugTriangles[0] = 0;
            _debugTriangles[1] = 1;
            _debugTriangles[2] = 2;
            _debugTriangles[3] = 0;
            _debugTriangles[4] = 3;
            _debugTriangles[5] = 1;

            _debugMesh.triangles = _debugTriangles;
        }
        void UpdateMesh()
        {
            _debugMesh.Clear();
            _debugMesh.vertices = _debugVerticies;
            _debugMesh.triangles = _debugTriangles;
            _debugMesh.RecalculateNormals();
        }
    }
    internal void DebugCostFieldWithMesh(int offset)
    {
        Gizmos.color = Color.black;
        NativeArray<byte> costs = _pathfindingManager.FieldDataContainer.GetCostFieldWithOffset(offset).Costs;
        float yOffset = .02f;
        float tileSize = FlowFieldUtilities.TileSize;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;

        for (int s = 0; s < FlowFieldUtilities.SectorMatrixTileAmount; s++)
        {
            NativeSlice<byte> sector = new NativeSlice<byte>(costs, s * FlowFieldUtilities.SectorTileAmount, FlowFieldUtilities.SectorTileAmount);
            for (int i = 0; i < sector.Length; i++)
            {
                if (sector[i] == 1) { continue; }
                int2 index2d = FlowFieldUtilities.GetGeneral2d(i, s, sectorMatrixColAmount, sectorColAmount);
                Vector2 indexpos2 = FlowFieldUtilities.IndexToPos(index2d, tileSize) - new float2(tileSize / 2, tileSize / 2);
                Vector3 indexPos = new Vector3(indexpos2.x, yOffset, indexpos2.y);
                Gizmos.DrawMesh(_debugMesh, indexPos);
            }
        }
    }
}
#endif