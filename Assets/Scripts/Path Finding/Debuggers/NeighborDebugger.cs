using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class NeighborDebugger
{
    PathfindingManager _pathfindingManager;

    public NeighborDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    public void DebugNeighbours()
    {
        Gizmos.color = Color.black;
        float yOffset = 0.05f;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount;
        int fieldColAmount = _pathfindingManager.ColumnAmount;
        NativeArray<Vector3> indexPositions = _pathfindingManager.TilePositions;
        NativeArray<UnsafeList<LocalDirectionData1d>> localDirections = _pathfindingManager.CostFieldProducer.LocalDirections;
        for(int i = 0; i < localDirections.Length; i++)
        {
            UnsafeList<LocalDirectionData1d> directionSector = localDirections[i];
            for(int j = 0; j < directionSector.Length; j++)
            {
                LocalDirectionData1d directionData = directionSector[j];
                Vector3 startingPos = indexPositions[ToGeneral1d(j, i)];
                Vector3 nPos = indexPositions[ToGeneral1d(directionData.n, directionData.nSector)];
                Vector3 ePos = indexPositions[ToGeneral1d(directionData.e, directionData.eSector)];
                Vector3 sPos = indexPositions[ToGeneral1d(directionData.s, directionData.sSector)];
                Vector3 wPos = indexPositions[ToGeneral1d(directionData.w, directionData.wSector)];
                Vector3 nePos = indexPositions[ToGeneral1d(directionData.ne, directionData.neSector)];
                Vector3 sePos = indexPositions[ToGeneral1d(directionData.se, directionData.seSector)];
                Vector3 swPos = indexPositions[ToGeneral1d(directionData.sw, directionData.swSector)];
                Vector3 nwPos = indexPositions[ToGeneral1d(directionData.nw, directionData.nwSector)];
                startingPos += new Vector3(0f, yOffset, 0f);
                nPos += new Vector3(0f, yOffset, 0f);
                ePos += new Vector3(0f, yOffset, 0f);
                sPos += new Vector3(0f, yOffset, 0f);
                wPos += new Vector3(0f, yOffset, 0f);
                nePos += new Vector3(0f, yOffset, 0f);
                sePos += new Vector3(0f, yOffset, 0f);
                swPos += new Vector3(0f, yOffset, 0f);
                nwPos += new Vector3(0f, yOffset, 0f);
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, nPos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, ePos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, sPos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, wPos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, nePos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, sePos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, swPos, 0.3f));
                Gizmos.DrawLine(startingPos, Vector3.Lerp(startingPos, nwPos, 0.3f));
            }
        }

        int ToGeneral1d(int local1d, int sector1d)
        {
            int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
            int2 sector2d = new int2(sector1d % sectorMatrixColAmount, sector1d / sectorMatrixColAmount);
            int2 sectorStart2d = sector2d * sectorColAmount;
            int2 general2d = local2d + sectorStart2d;
            return general2d.y * fieldColAmount + general2d.x;
        }
    }
}
