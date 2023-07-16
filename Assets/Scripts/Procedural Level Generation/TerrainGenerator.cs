using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] MeshCollider _fieldMeshCollider;
    [SerializeField] MeshFilter _fieldMeshFilter;
    [SerializeField] MeshFilter _obstacleMeshFilter;
    [Header("Random Generator")]
    [SerializeField][Range(1,20)] float _resolution;    //5 good
    [SerializeField] SimulationState _simulationState;
    [SerializeField] Material _obstacleMat;
    [HideInInspector] public WalkabilityData WalkabilityData;

    public float TileSize;
    public int RowAmount;
    public int ColumnAmount;

    //FIELD
    Vector3[] _fieldVerticies = new Vector3[4];
    int[] _fieldTriangles = new int[6];
    Mesh _fieldMesh;

    ObstacleGenerator obsGenerator;

    private void Awake()
    {
        WalkabilityData = new WalkabilityData(TileSize, RowAmount, ColumnAmount, _resolution, _simulationState);

        //CONFIGURE FIELD MESH
        _fieldMesh = new Mesh();
        _fieldMesh.name = "Field Mesh";
        _fieldMeshFilter.mesh = _fieldMesh;

        //SET VERTICIES OF FIELD MESH
        _fieldVerticies[0] = Vector3.zero;
        _fieldVerticies[1] = new Vector3(TileSize * ColumnAmount, 0, 0);
        _fieldVerticies[2] = new Vector3(0, 0, TileSize * RowAmount);
        _fieldVerticies[3] = _fieldVerticies[1] + _fieldVerticies[2];

        //SET TRIANGLES OF FIELD MESH
        _fieldTriangles[0] = 2;
        _fieldTriangles[1] = 1;
        _fieldTriangles[2] = 0;
        _fieldTriangles[3] = 2;
        _fieldTriangles[4] = 3;
        _fieldTriangles[5] = 1;

        //UPDATE FIELD MESH
        _fieldMesh.Clear();
        _fieldMesh.vertices = _fieldVerticies;
        _fieldMesh.triangles = _fieldTriangles;
        _fieldMesh.RecalculateNormals();
        _fieldMeshCollider.sharedMesh = _fieldMesh;

        obsGenerator = new ObstacleGenerator(this, _obstacleMeshFilter, WalkabilityData, _obstacleMat);
        obsGenerator.CreateMesh();
    }
}
public enum SimulationState : byte
{
    PerlinNoise,
    FullWalkable
}