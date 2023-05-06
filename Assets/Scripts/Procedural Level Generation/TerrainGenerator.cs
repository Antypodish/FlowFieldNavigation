using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] MeshCollider _meshCollider;
    [SerializeField] MeshFilter _meshFilter;
    [Header("Random Generator")]
    [SerializeField][Range(1,10)] float _resolution;    //5 good
    [SerializeField] SimulationState _simulationState;

    [HideInInspector] public WalkabilityData WalkabilityData;

    public float TileSize;
    public int TileAmount;

    Vector3[] _verticies = new Vector3[4];
    int[] _triangles = new int[6];
    Mesh _mesh;

    private void Awake()
    {
        ConfigureMesh();
        SetVerticies();
        SetTriangles();
        UpdateMesh();

        WalkabilityData = new WalkabilityData(TileSize, TileAmount, _resolution, _simulationState);


        void ConfigureMesh()
        {
            _mesh = new Mesh();
            _mesh.name = "Procedural Mesh";
            _meshFilter.mesh = _mesh;
        }
        void SetVerticies()
        {
            _verticies[0] = Vector3.zero;
            _verticies[1] = new Vector3(TileSize * TileAmount, 0, 0);
            _verticies[2] = new Vector3(0, 0, TileSize * TileAmount);
            _verticies[3] = _verticies[1] + _verticies[2];
        }
        void SetTriangles()
        {
            _triangles[0] = 2;
            _triangles[1] = 1;
            _triangles[2] = 0;
            _triangles[3] = 2;
            _triangles[4] = 3;
            _triangles[5] = 1;
        }
        void UpdateMesh()
        {
            _mesh.Clear();
            _mesh.vertices = _verticies;
            _mesh.triangles = _triangles;
            _mesh.RecalculateNormals();
            _meshCollider.sharedMesh = _mesh;
        }
    }
}
public enum SimulationState : byte
{
    PerlinNoise,
    FullWalkable
}