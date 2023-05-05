using Unity.Collections;
using UnityEngine;

public class SectorGraphDebugger
{
    PathfindingManager _pathfindingManager;

    public SectorGraphDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugSectorToWindow(int offset)
    {
        Gizmos.color = Color.black;
        float yOffset = 0.02f;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph sectorGraph = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph;
        NativeArray<SectorNode> sectorNodes = sectorGraph.SectorArray.Nodes;
        for (int i = 0; i < sectorNodes.Length; i++)
        {
            int sectorSize = sectorNodes[i].Sector.Size;
            Index2 secIndex = sectorNodes[i].Sector.StartIndex;
            float sectorPosX = (secIndex.C * tileSize + (secIndex.C + sectorSize) * tileSize) / 2;
            float sectorPosZ = (secIndex.R * tileSize + (secIndex.R + sectorSize) * tileSize) / 2;
            Vector3 sectorPos = new Vector3(sectorPosX, yOffset, sectorPosZ);

            //draw square on the center of the sector
            Vector3 sqrTopLeft = new Vector3(sectorPos.x - 0.125f, sectorPos.y, sectorPos.z + 0.125f);
            Vector3 sqrTopRight = new Vector3(sectorPos.x + 0.125f, sectorPos.y, sectorPos.z + 0.125f);
            Vector3 sqrBotLeft = new Vector3(sectorPos.x - 0.125f, sectorPos.y, sectorPos.z - 0.125f);
            Vector3 sqrBotRight = new Vector3(sectorPos.x + 0.125f, sectorPos.y, sectorPos.z - 0.125f);
            Gizmos.DrawLine(sqrTopLeft, sqrTopRight);
            Gizmos.DrawLine(sqrTopRight, sqrBotRight);
            Gizmos.DrawLine(sqrBotRight, sqrBotLeft);
            Gizmos.DrawLine(sqrBotLeft, sqrTopLeft);


            //draw line through each window of the sector
            WindowNode[] windowNodes = sectorGraph.GetWindowNodesOf(sectorNodes[i]);
            for(int j = 0; j < windowNodes.Length; j++)
            {
                Index2 winBotLeftIndex = windowNodes[j].Window.BottomLeftBoundary;
                Index2 winTopRightIndex = windowNodes[j].Window.TopRightBoundary;
                Vector3 winBotLeftPos= new Vector3(winBotLeftIndex.C * tileSize, yOffset, winBotLeftIndex.R * tileSize);
                Vector3 winTopRightPos= new Vector3( winTopRightIndex.C * tileSize, yOffset, winTopRightIndex.R * tileSize);
                Gizmos.DrawLine(sectorPos, (winBotLeftPos + winTopRightPos) / 2);
            }
        }
    }
    public void DebugWindowToSector(int offset)
    {
        Gizmos.color = Color.black;
        float yOffset = 0.02f;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph sectorGraph = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph;
        NativeArray<WindowNode> windowNodes = sectorGraph.WindowArray.Nodes;
        for(int i = 0; i < windowNodes.Length; i++)
        {
            Index2 winBotLeftIndex = windowNodes[i].Window.BottomLeftBoundary;
            Index2 winTopRightIndex = windowNodes[i].Window.TopRightBoundary;
            Vector3 winBotLeftPos = new Vector3(winBotLeftIndex.C * tileSize, yOffset, winBotLeftIndex.R * tileSize);
            Vector3 winTopRightPos = new Vector3(winTopRightIndex.C * tileSize, yOffset, winTopRightIndex.R * tileSize);
            Vector3 windowPos = (winBotLeftPos + winTopRightPos) / 2;

            Vector3 sqrTopLeft = new Vector3(windowPos.x - 0.125f, windowPos.y, windowPos.z + 0.125f);
            Vector3 sqrTopRight = new Vector3(windowPos.x + 0.125f, windowPos.y, windowPos.z + 0.125f);
            Vector3 sqrBotLeft = new Vector3(windowPos.x - 0.125f, windowPos.y, windowPos.z - 0.125f);
            Vector3 sqrBotRight = new Vector3(windowPos.x + 0.125f, windowPos.y, windowPos.z - 0.125f);
            Gizmos.DrawLine(sqrTopLeft, sqrTopRight);
            Gizmos.DrawLine(sqrTopRight, sqrBotRight);
            Gizmos.DrawLine(sqrBotRight, sqrBotLeft);
            Gizmos.DrawLine(sqrBotLeft, sqrTopLeft);

            SectorNode[] sectorNodes = sectorGraph.GetSectorNodesOf(windowNodes[i]);
            for(int j = 0; j < sectorNodes.Length; j++)
            {
                int sectorSize = sectorNodes[j].Sector.Size;
                Index2 secIndex = sectorNodes[j].Sector.StartIndex;
                float sectorPosX = (secIndex.C * tileSize + (secIndex.C + sectorSize) * tileSize) / 2;
                float sectorPosZ = (secIndex.R * tileSize + (secIndex.R + sectorSize) * tileSize) / 2;
                Vector3 sectorPos = new Vector3(sectorPosX, yOffset, sectorPosZ);

                Gizmos.DrawLine(windowPos, sectorPos);
            }
        }
    }
}
