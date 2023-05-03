using UnityEngine;
using Unity.Collections;
using UnityEditor;

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

    public void DebugCostField(int offset)
    {
        NativeArray<Vector3> tilePositions = _pathfindingManager.TilePositions;
        NativeArray<byte> costs = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Costs;

        for(int i = 0; i < costs.Length; i++)
        {
            Vector3 pos = tilePositions[i];
            byte cost = costs[i];
            //Handles.Label(pos, cost.ToString());
        }
    }
    public void DebugCostFieldWithMesh(int offset)
    {
        Gizmos.color = Color.black;
        NativeArray<byte> costs = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Costs;
        float yOffset = .02f;
        float tileSize = _pathfindingManager.TileSize;
        int tileAmount = _pathfindingManager.TileAmount;

        for (int r = 0; r < tileAmount; r++)
        {
            for(int c = 0; c < tileAmount; c++)
            {
                int index = r * tileAmount + c;
                if (costs[index] == 1) { continue; }

                Vector3 pos = new Vector3(c * tileSize, yOffset, r * tileSize);
                Gizmos.DrawMesh(_debugMesh, pos);
            }            
        }
    }

    
}
