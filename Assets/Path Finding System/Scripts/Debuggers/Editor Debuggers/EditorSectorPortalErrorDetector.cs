using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class EditorSectorPortalErrorDetector
{
    PathfindingManager _pathfindingManager;

    public EditorSectorPortalErrorDetector(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void Debug(int offset)
    {
        FieldGraph[] fieldGraphs = _pathfindingManager.FieldProducer.GetAllFieldGraphs();
        for(int i = 0; i < fieldGraphs.Length; i++)
        {
            Gizmos.color = Color.red;
            FieldGraph field = fieldGraphs[i];

            NativeArray<SectorNode> sectors = field.SectorNodes;
            NativeArray<int> sectowinptrs = field.SecToWinPtrs;
            NativeArray<WindowNode> windowNodes = field.WindowNodes;
            NativeArray<PortalNode> portalNodes = field.PortalNodes;
            for (int j = 0; j < sectors.Length; j++)
            {
                int sectorIndex = j;
                int2 sector2d = FlowFieldUtilities.To2D(sectorIndex, FlowFieldUtilities.SectorMatrixColAmount);
                SectorNode sector = sectors[sectorIndex];

                int winStart = sector.SecToWinPtr;
                int winCnt = sector.SecToWinCnt;

                for (int k = winStart; k < winStart + winCnt; k++)
                {
                    int windowIndex = sectowinptrs[k];
                    WindowNode windowNode = windowNodes[windowIndex];
                    int porStart = windowNode.PorPtr;
                    int porCount = windowNode.PorCnt;
                    for (int l = porStart; l < porStart + porCount; l++)
                    {
                        PortalNode portalNode = portalNodes[l];
                        int2 index1 = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
                        int2 index2 = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);

                        bool index1insector = FlowFieldUtilities.GetSector2D(index1, FlowFieldUtilities.SectorColAmount).Equals(sector2d);
                        bool index2insector = FlowFieldUtilities.GetSector2D(index2, FlowFieldUtilities.SectorColAmount).Equals(sector2d);

                        if(!(index1insector || index2insector))
                        {
                            string str = "offset: " + i + "\n" +
                                "sector1d: " + sectorIndex + "\n" +
                                "sector2d: " + sector2d + "\n" +
                                "portal1 2d: " + index1 + "\n" +
                                "portal2 2d: " + index2 + "\n" +
                                "portal1 sector" + FlowFieldUtilities.GetSector2D(index1, FlowFieldUtilities.SectorColAmount) + "\n" +
                                "portal2 sector" + FlowFieldUtilities.GetSector2D(index2, FlowFieldUtilities.SectorColAmount);
                            UnityEngine.Debug.Log(str);
                            float2 portalPos = (FlowFieldUtilities.IndexToPos(index1, FlowFieldUtilities.TileSize) + FlowFieldUtilities.IndexToPos(index1, FlowFieldUtilities.TileSize)) / 2;
                            float3 portalPos3d = new float3(portalPos.x, 0f, portalPos.y);
                            if(offset == i)
                            {
                                Gizmos.DrawSphere(portalPos3d, 1f);
                            }
                        }
                    }
                }
            }

        }

    }
}
