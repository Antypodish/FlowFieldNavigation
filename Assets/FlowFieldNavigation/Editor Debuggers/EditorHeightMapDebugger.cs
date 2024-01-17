using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
public class EditorHeightMapDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _debugMesh;
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
    
}
