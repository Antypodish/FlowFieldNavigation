using UnityEngine;

internal class UnwalkableTileDebugger
{
    WalkabilityData _walkabilityData;
    TerrainGenerator _terrainGenerator;
    Mesh _debugMesh;
    Vector3[] _debugVerticies;
    int[] _debugTriangles;

    internal UnwalkableTileDebugger(WalkabilityData walkabilityData, TerrainGenerator terrainGenerator)
    {
        _walkabilityData = walkabilityData;
        _terrainGenerator = terrainGenerator;
    }

    internal void CreateMesh()
    {
        _debugVerticies = new Vector3[4];
        _debugTriangles = new int[6];

        float tileSize = _terrainGenerator.TileSize;

        ConfigureMesh();
        SetVerticies();
        SetTriangles();
        UpdateMesh();

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
        void ConfigureMesh()
        {
            _debugMesh = new Mesh();
            _debugMesh.name = "DebugMesh";
        }
        void UpdateMesh()
        {
            _debugMesh.Clear();
            _debugMesh.vertices = _debugVerticies;
            _debugMesh.triangles = _debugTriangles;
            _debugMesh.RecalculateNormals();
        }
    }
    public void DebugUnwalkableTiles()
    {
        Gizmos.color = Color.black;
        if (_walkabilityData == null) { return; }

        float tileSize = _terrainGenerator.TileSize;
        int tileAmount = _terrainGenerator.TileAmount;
        float yOffset = .01f;

        for (int r = 0; r < tileAmount; r++)
        {
            for (int c = 0; c < tileAmount; c++)
            {
                if (_terrainGenerator.WalkabilityData.WalkabilityMatrix[r][c].Walkability == Walkability.Walkable) { continue; }
                Vector3 pos = _terrainGenerator.WalkabilityData.WalkabilityMatrix[r][c].CellPosition + new Vector3(0, yOffset, 0);
                Gizmos.DrawMesh(_debugMesh, pos);
            }
        }
    }
}
