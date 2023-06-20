#if (UNITY_EDITOR) 

using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public class CostFieldDebugger
{
    PathfindingManager _pathfindingManager;

    Mesh _debugMesh;
    Vector3[] _debugVerticies;
    int[] _debugTriangles;

    public CostFieldDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;

        //configure debug mesh
        float tileSize = pathfindingManager.TileSize;
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
    public void DebugCostFieldWithMesh(int offset)
    {
        Gizmos.color = Color.black;
        NativeArray<UnsafeList<byte>> costs = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Costs;
        float yOffset = .02f;
        float tileSize = _pathfindingManager.TileSize;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;

        for(int s = 0; s < costs.Length; s++)
        {
            UnsafeList<byte> sector = costs[s];
            Vector3 sectorStartPos = GetSectorStartPosition(s, sectorMatrixColAmount, sectorColAmount, tileSize);
            for(int i = 0; i < sector.Length; i++)
            {
                Vector3 indexPos = GetIndexPosition(sectorStartPos, i, sectorColAmount, tileSize);
                if (sector[i] == 1) { continue; }
                Gizmos.DrawMesh(_debugMesh, indexPos);
            }
        }

        Vector3 GetSectorStartPosition(int sector1d, int sectorMatrixColAmount, int sectorColAmount, float tileSize)
        {
            int2 sector2d = new int2(sector1d % sectorMatrixColAmount, sector1d / sectorMatrixColAmount);
            return new Vector3(sector2d.x * sectorColAmount * tileSize, yOffset, sector2d.y * sectorColAmount * tileSize);
        }
        Vector3 GetIndexPosition(Vector3 sectorStartPosition, int local1d, int sectorColAmount, float tileSize)
        {
            int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
            Vector3 localIndexPos = new Vector3(local2d.x * tileSize, yOffset, local2d.y * tileSize);
            return sectorStartPosition + localIndexPos;
        }
    }
}
#endif
