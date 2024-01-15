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
                    if ((obsCount % 6000) == 0)
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
                    Vector3 lowerBotLeft = cell.CellPosition;
                    Vector3 lowerTopLeft = cell.CellPosition + new Vector3(0, 0, tileSize);
                    Vector3 lowerTopRight = cell.CellPosition + new Vector3(tileSize, 0, tileSize);
                    Vector3 lowerBotRight = cell.CellPosition + new Vector3(tileSize, 0, 0);

                    Vector3 upperBotLeft = cell.CellPosition + new Vector3(0, 2f, 0);
                    Vector3 upperTopLeft = cell.CellPosition + new Vector3(0, 2f, tileSize);
                    Vector3 upperTopRight = cell.CellPosition + new Vector3(tileSize, 2f, tileSize);
                    Vector3 upperBotRight = cell.CellPosition + new Vector3(tileSize, 2f, 0);

                    int lbl = vertexStartIndex;
                    int ltl = vertexStartIndex + 1;
                    int ltr = vertexStartIndex + 2;
                    int lbr = vertexStartIndex + 3;
                    int ubl = vertexStartIndex + 4;
                    int utl = vertexStartIndex + 5;
                    int utr = vertexStartIndex + 6;
                    int ubr = vertexStartIndex + 7;

                    //Verts
                    _obstacleVerticies.Add(lowerBotLeft);
                    _obstacleVerticies.Add(lowerTopLeft);
                    _obstacleVerticies.Add(lowerTopRight);
                    _obstacleVerticies.Add(lowerBotRight);
                    _obstacleVerticies.Add(upperBotLeft);
                    _obstacleVerticies.Add(upperTopLeft);
                    _obstacleVerticies.Add(upperTopRight);
                    _obstacleVerticies.Add(upperBotRight);

                    //Trigs
                    _obstacleTriangles.Add(ubl);
                    _obstacleTriangles.Add(utl);
                    _obstacleTriangles.Add(ubr);
                    _obstacleTriangles.Add(utl);
                    _obstacleTriangles.Add(utr);
                    _obstacleTriangles.Add(ubr);

                    _obstacleTriangles.Add(lbl);
                    _obstacleTriangles.Add(ubl);
                    _obstacleTriangles.Add(lbr);
                    _obstacleTriangles.Add(ubl);
                    _obstacleTriangles.Add(ubr);
                    _obstacleTriangles.Add(lbr);

                    _obstacleTriangles.Add(lbr);
                    _obstacleTriangles.Add(ubr);
                    _obstacleTriangles.Add(ltr);
                    _obstacleTriangles.Add(ubr);
                    _obstacleTriangles.Add(utr);
                    _obstacleTriangles.Add(ltr);

                    _obstacleTriangles.Add(ltl);
                    _obstacleTriangles.Add(utl);
                    _obstacleTriangles.Add(lbl);
                    _obstacleTriangles.Add(utl);
                    _obstacleTriangles.Add(ubl);
                    _obstacleTriangles.Add(lbl);

                    _obstacleTriangles.Add(ltr);
                    _obstacleTriangles.Add(utr);
                    _obstacleTriangles.Add(ltl);
                    _obstacleTriangles.Add(utr);
                    _obstacleTriangles.Add(utl);
                    _obstacleTriangles.Add(ltl);
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
        obj.GetComponent<MeshFilter>().mesh = mesh;
        return obj;
    }
}
