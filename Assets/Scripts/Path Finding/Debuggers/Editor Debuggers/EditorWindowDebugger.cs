#if (UNITY_EDITOR) 

using UnityEngine;
public class EditorWindowDebugger
{
    PathfindingManager _pathfindingManager;

    Mesh _debugMesh;
    Vector3[] _debugVerticies;
    int[] _debugTriangles;

    public EditorWindowDebugger(PathfindingManager pathfindingManager)
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

    public void DebugWindows(int offset)
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        float yOffset = .02f;
        float tileSize = _pathfindingManager.TileSize;

        CostField costField = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset);
        for (int i = 0; i < costField.FieldGraph.WindowNodes.Length; i++)
        {
            Index2 botLeftBound = costField.FieldGraph.WindowNodes[i].Window.BottomLeftBoundary;
            Index2 upRightBound = costField.FieldGraph.WindowNodes[i].Window.TopRightBoundary;
            for(int r = botLeftBound.R; r <= upRightBound.R; r++)
            {
                for(int c = botLeftBound.C; c <= upRightBound.C; c++)
                {
                    Vector3 pos = new Vector3(c * tileSize, yOffset, r * tileSize);
                    Gizmos.DrawMesh(_debugMesh, pos);
                }
            }

        }
    }
}

#endif