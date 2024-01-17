using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;

public class EditorHeightMapDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _debugMesh;
    float2 _hitPos;
    public EditorHeightMapDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugHeightMapMesh()
    {
        if(_debugMesh == null)
        {
            HeightMapProducer heightMapGenerator = _pathfindingManager.HeightMapGenerator;
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
    public void DebugTrianglesAtTile()
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

        NativeArray<float3> verticies = _pathfindingManager.HeightMapGenerator.Verticies;
        NativeArray<int> triangles = _pathfindingManager.HeightMapGenerator.Triangles;
        NativeArray<TileTriangleSpan> tileTrianglePointerSpans = _pathfindingManager.HeightMapGenerator.TileTrianglePointerSpans;
        NativeArray<int> tileTrianglePointers = _pathfindingManager.HeightMapGenerator.TileTrianglePointers;

        int2 tile2d = FlowFieldUtilities.PosTo2D(_hitPos, FlowFieldUtilities.TileSize);
        int tile1d = FlowFieldUtilities.To1D(tile2d, FlowFieldUtilities.FieldColAmount);

        TileTriangleSpan triangleSpan = tileTrianglePointerSpans[tile1d];
        NativeSlice<int> trianlgePointers = new NativeSlice<int>(tileTrianglePointers, triangleSpan.TrianglePointerStartIndex, triangleSpan.TrianglePointerCount);
        for(int i = 0; i < trianlgePointers.Length; i++)
        {
            int t1 = trianlgePointers[i];
            int t2 = t1 + 1;
            int t3 = t1 + 2;
            float3 v1 = verticies[triangles[t1]];
            float3 v2 = verticies[triangles[t2]];
            float3 v3 = verticies[triangles[t3]];
            float3 avg = (v1 + v2 + v3) / 3;
            Gizmos.DrawSphere(avg, 0.2f);
        }
    }
}
