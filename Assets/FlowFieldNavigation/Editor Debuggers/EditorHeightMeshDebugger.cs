using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;

internal class EditorHeightMeshDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _debugMesh;
    float2 _hitPos;
    internal EditorHeightMeshDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    internal void DebugHeightMapMesh()
    {
        if(_debugMesh == null)
        {
            HeightMeshProducer heightMapGenerator = _pathfindingManager.FieldManager.HeightMeshGenerator;
            NativeArray<float3> verticies = heightMapGenerator.Verticies;
            NativeArray<int> triangles = heightMapGenerator.Triangles;

            Mesh mesh = new Mesh();

            Vector3[] verts = new Vector3[verticies.Length];
            int[] trigs = triangles.ToArray();
            for (int i = 0; i < verticies.Length; i++)
            {
                //Gizmos.DrawSphere(verticies[i], 0.2f);
                verts[i] = verticies[i] + new float3(0, 0.05f, 0);
            }
            mesh.vertices = verts;
            mesh.triangles = trigs;
            mesh.RecalculateNormals();
            _debugMesh = mesh;
        }
        Gizmos.color = Color.black;
        Gizmos.DrawWireMesh(_debugMesh);
        //Gizmos.color = new Color(0, 1, 1, 0.25f);
        //Gizmos.DrawMesh(_debugMesh);
    }
    internal void DebugBorders(int gridIndex)
    {
        Gizmos.color = Color.black;
        TriangleSpatialHashGrid triangleHashGrid = _pathfindingManager.FieldManager.HeightMeshGenerator.GetTriangleSpatialHashGrid();
        if (gridIndex < 0 || gridIndex > triangleHashGrid.GetGridCount()) { return; }
        float tileSize = triangleHashGrid.GetGridTileSize(gridIndex);
        int colAmount = triangleHashGrid.GetGridColAmount(gridIndex);
        int rowAmount = triangleHashGrid.GetGridRowAmount(gridIndex);
        float maxZ = rowAmount * tileSize;
        float maxX = colAmount * tileSize;
        float yOffset = 0.2f;
        for (int i = 0; i < colAmount; i++)
        {
            Vector3 start = new Vector3(i * tileSize, yOffset, 0f);
            Vector3 end = new Vector3(start.x, yOffset, maxZ);
            Gizmos.DrawLine(start, end);
        }
        for (int i = 0; i < rowAmount; i++)
        {
            Vector3 start = new Vector3(0f, yOffset, tileSize * i);
            Vector3 end = new Vector3(maxX, yOffset, start.z);
            Gizmos.DrawLine(start, end);
        }
    }
    internal void DebugTrianglesAtTile()
    {
        Gizmos.color = Color.red;
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1000, 8))
            {
                _hitPos = new float2(hit.point.x, hit.point.z);
            }
        }

        NativeArray<float3> verticies = _pathfindingManager.FieldManager.HeightMeshGenerator.Verticies;

        TriangleSpatialHashGrid triangleHashGrid = _pathfindingManager.FieldManager.HeightMeshGenerator.GetTriangleSpatialHashGrid();
        for(int i = 0; i < triangleHashGrid.GetGridCount(); i++)
        {
            TriangleSpatialHashGridIterator gridIterator = triangleHashGrid.GetIterator(_hitPos, i);
            while (gridIterator.HasNext())
            {
                NativeSlice<int> triangles = gridIterator.GetNextRow();
                for(int j = 0; j < triangles.Length; j+=3)
                {
                    int t1 = triangles[j];
                    int t2 = triangles[j + 1];
                    int t3 = triangles[j + 2];
                    float3 v1 = verticies[t1];
                    float3 v2 = verticies[t2];
                    float3 v3 = verticies[t3];
                    float3 avg = (v1 + v2 + v3) / 3;
                    Gizmos.DrawSphere(avg, 0.2f);

                }
            }
        }
    }
}
