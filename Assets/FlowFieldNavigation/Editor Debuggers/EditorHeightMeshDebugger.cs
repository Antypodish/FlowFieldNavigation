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
            HeightMeshProducer heightMapGenerator = _pathfindingManager.FieldDataContainer.HeightMeshGenerator;
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
        TriangleSpatialHashGrid triangleHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid();
        if (gridIndex < 0 || gridIndex > triangleHashGrid.GetGridCount()) { return; }
        float tileSize = triangleHashGrid.GetGridTileSize(gridIndex);
        int colAmount = triangleHashGrid.GetGridColAmount(gridIndex);
        int rowAmount = triangleHashGrid.GetGridRowAmount(gridIndex);
        float2 maxPos = FlowFieldUtilities.GetGridMaxPosExcluding(rowAmount, colAmount, tileSize, FlowFieldUtilities.HeightMeshStartPosition);
        float maxZ = maxPos.y;
        float maxX = maxPos.x;
        float yOffset = 0.2f;
        for (int i = 0; i < colAmount; i++)
        {
            float2 start2 = new float2(i * tileSize, 0f);
            float2 end2 = new float2(start2.x, maxZ);
            start2 += FlowFieldUtilities.HeightMeshStartPosition;
            end2 += FlowFieldUtilities.HeightMeshStartPosition;
            Vector3 start = new Vector3(start2.x, yOffset, start2.y);
            Vector3 end = new Vector3(end2.x, yOffset, end2.y);
            Gizmos.DrawLine(start, end);
        }
        for (int i = 0; i < rowAmount; i++)
        {
            float2 start2 = new float2(0f, tileSize * i);
            float2 end2 = new float2(maxX, start2.y);
            start2 += FlowFieldUtilities.HeightMeshStartPosition;
            end2 += FlowFieldUtilities.HeightMeshStartPosition;
            Vector3 start = new Vector3(start2.x, yOffset, start2.y);
            Vector3 end = new Vector3(end2.x, yOffset, end2.y);
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

        NativeArray<float3> verticies = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.Verticies;

        TriangleSpatialHashGrid triangleHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid();
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
