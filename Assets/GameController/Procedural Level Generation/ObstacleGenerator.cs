using System.Collections.Generic;
using UnityEngine;

public class ObstacleGenerator
{
    TerrainGenerator _terrainGenerator;
    List<Vector3> _obstacleVerticies = new List<Vector3>();
    List<int> _obstacleTriangles = new List<int>();
    WalkabilityData _walkabilityData;
    Material _obstacleMat;
    List<GameObject> obstacleObjects;
    public ObstacleGenerator(TerrainGenerator terrainGenerator, WalkabilityData walkabilityData, Material obstacleMat)
    {
        _terrainGenerator = terrainGenerator;
        _walkabilityData = walkabilityData;
        _obstacleMat = obstacleMat;
        obstacleObjects = new List<GameObject>();
    }

    public void CreateMesh()
    {
        float tileSize = _terrainGenerator.TileSize;
        float yOffset = 0.001f;
        int obsCount = 0;

        Mesh curMesh = new Mesh();
        curMesh.name = "Obstacle Mesh";
        GameObject curObsObject = GetObstacleObject(curMesh);
        obstacleObjects.Add(curObsObject);
        curObsObject.GetComponent<MeshFilter>().mesh = curMesh;

        WalkabilityCell[][] walkabilityMatrix = _walkabilityData.WalkabilityMatrix;
        for (int y = 0; y < walkabilityMatrix.Length; y++)
        {
            for (int x = 0; x < walkabilityMatrix[y].Length; x++)
            {
                WalkabilityCell cell = walkabilityMatrix[y][x];
                if (cell.Walkability == Walkability.Unwalkable)
                {
                    //DETERMINE OBSTACLE OBJECT
                    obsCount++;
                    if ((obsCount % 12000) == 0)
                    {
                        curMesh.Clear();
                        curMesh.vertices = _obstacleVerticies.ToArray();
                        curMesh.triangles = _obstacleTriangles.ToArray();
                        curMesh.RecalculateNormals();

                        curMesh = new Mesh();
                        curMesh.name = "Obstacle Mesh";
                        curObsObject = GetObstacleObject(curMesh);
                        obstacleObjects.Add(curObsObject);
                        _obstacleVerticies.Clear();
                        _obstacleTriangles.Clear();
                    }

                    int vertexStartIndex = _obstacleVerticies.Count;
                    //ADD VERTICIES
                    Vector3 botLeft = cell.CellPosition;
                    Vector3 topLeft = cell.CellPosition + new Vector3(0, 0, tileSize);
                    Vector3 topRight = cell.CellPosition + new Vector3(tileSize, 0, tileSize);
                    Vector3 botRight = cell.CellPosition + new Vector3(tileSize, 0, 0);
                    _obstacleVerticies.Add(botLeft);
                    _obstacleVerticies.Add(topLeft);
                    _obstacleVerticies.Add(topRight);
                    _obstacleVerticies.Add(botRight);

                    _obstacleTriangles.Add(vertexStartIndex);
                    _obstacleTriangles.Add(vertexStartIndex + 1);
                    _obstacleTriangles.Add(vertexStartIndex + 3);
                    _obstacleTriangles.Add(vertexStartIndex + 1);
                    _obstacleTriangles.Add(vertexStartIndex + 2);
                    _obstacleTriangles.Add(vertexStartIndex + 3);

                }
            }
        }
        curMesh.Clear();
        curMesh.vertices = _obstacleVerticies.ToArray();
        curMesh.triangles = _obstacleTriangles.ToArray();
        curMesh.RecalculateNormals();

    }
    GameObject GetObstacleObject(Mesh mesh)
    {
        GameObject obj = new GameObject("Obstacle Object");
        obj.transform.parent = _terrainGenerator.transform;
        obj.transform.localPosition = new Vector3(0, 0.01f, 0);
        obj.AddComponent<MeshFilter>();
        obj.AddComponent<MeshRenderer>();
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.material = _obstacleMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        obj.GetComponent<MeshFilter>().mesh = mesh;
        return obj;
    }
}
