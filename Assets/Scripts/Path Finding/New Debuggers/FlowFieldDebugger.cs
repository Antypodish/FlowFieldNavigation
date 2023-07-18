using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class FlowFieldDebugger
{
    PathfindingManager _pathfindingManager;
    public void Debug(FlowFieldAgent agent)
    {
        Color color = Color.black;
        if (agent == null) { return; }
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = 0.001f;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount * _pathfindingManager.SectorTileAmount;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;

        Path path = agent.GetPath();
        if (path == null) { return; }
        UnsafeList<int> sectorToPicked = path.SectorToPicked;
        UnsafeList<FlowData> flowField = path.FlowField;
        for (int i = 0; i < sectorToPicked.Length; i++)
        {
            int sectorIndex = i;
            int pickedIndex = sectorToPicked[sectorIndex];
            if (pickedIndex == 0) { continue; }

            int2 sectorIndex2d = new int2(i % sectorMatrixColAmount, i / sectorMatrixColAmount);
            int2 sectorStartIndex = sectorIndex2d * sectorColAmount;
            for (int j = pickedIndex; j < pickedIndex + sectorTileAmount; j++)
            {
                int local1d = j - pickedIndex;
                int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                int2 general2d = local2d + sectorStartIndex;
                Vector3 pos = new Vector3(tileSize / 2 + general2d.x * tileSize, yOffset, tileSize / 2 + general2d.y * tileSize);

            }
        }


        void DrawLine(Vector3 start, Vector3 end)
        {

        }
    }
}