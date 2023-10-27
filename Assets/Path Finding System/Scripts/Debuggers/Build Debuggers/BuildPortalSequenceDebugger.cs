using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class BuildPortalSequenceDebugger
{
    PathfindingManager _pathfindingManager;
    Mesh _mesh;
    Material _meshMat;

    public BuildPortalSequenceDebugger(PathfindingManager pathfindingManager, Material _portalSequenceDebugMaterial)
    {
        _pathfindingManager = pathfindingManager;
        _meshMat = _portalSequenceDebugMaterial;
    }
    public void Debug(FlowFieldAgent agent, float offset)
    {
        if (_mesh == null) { ConfigMesh(); }
        if (agent == null) { return; }
        float tileSize = _pathfindingManager.TileSize;

        Path path = agent.GetPath();
        if (path == null) { return; }
        NativeArray<PortalNode> portalNodes = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset).PortalNodes;
        NativeList<ActivePortal> portalSequence = path.PortalSequence;
        NativeList<int> portalSequenceBorders = path.PortalSequenceBorders;
        for (int i = 0; i < portalSequenceBorders.Length - 1; i++)
        {
            int start = portalSequenceBorders[i];
            int end = portalSequenceBorders[i + 1];
            for (int j = start; j < end - 1; j++)
            {
                Gizmos.color = Color.black;
                PortalNode firstportalNode = portalNodes[portalSequence[j].Index];
                PortalNode secondportalNode = portalNodes[portalSequence[j + 1].Index];
                if (firstportalNode.Portal1.Index.R == 0) { continue; }
                Vector3 firstPorPos = firstportalNode.GetPosition(tileSize);
                Vector3 secondPorPos = secondportalNode.GetPosition(tileSize);
                Graphics.DrawMesh(_mesh, firstPorPos, Quaternion.identity, _meshMat, 1);
            }
            Vector3 pos = portalNodes[portalSequence[end - 2].Index].GetPosition(tileSize);
            Graphics.DrawMesh(_mesh, pos, Quaternion.identity, _meshMat, 1);
        }


        void DrawLine(Vector3 start, Vector3 end)
        {

        }
        void ConfigMesh()
        {
            float tileSize = 0.5f;
            _mesh = new Mesh();
            _mesh.name = "Field Mesh";

            Vector3[] verticies = new Vector3[4];
            verticies[0] = Vector3.zero;
            verticies[1] = new Vector3(0, 0, tileSize);
            verticies[2] = new Vector3(tileSize, 0, tileSize);
            verticies[3] = new Vector3(tileSize, 0, 0);

            int[] trigs = new int[6];
            trigs[0] = 0;
            trigs[1] = 1;
            trigs[2] = 3;
            trigs[3] = 1;
            trigs[4] = 2;
            trigs[5] = 3;

            _mesh.Clear();
            _mesh.vertices = verticies;
            _mesh.triangles = trigs;
            _mesh.RecalculateNormals();
        }
    }
}
