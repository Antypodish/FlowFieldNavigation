using Unity.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEditor.PackageManager.UI;
using UnityEngine.Analytics;

public struct PortalArray
{
    public NativeArray<PortalNode> Nodes;
    public NativeArray<PortalToPortal> PorPtrs;

    public PortalArray(int portalNodeAmount, int porToPorPtrAmount)
    {
        Nodes = new NativeArray<PortalNode>(portalNodeAmount, Allocator.Persistent);
        PorPtrs = new NativeArray<PortalToPortal>(porToPorPtrAmount, Allocator.Persistent);
    }
    public void ConfigurePortalNodes(NativeArray<WindowNode> windowNodes, NativeArray<byte> costs, int tileAmount, int porToPorCnt)
    {
        for (int i = 0; i < windowNodes.Length; i++)
        {
            Window window = windowNodes[i].Window;
            if (window.IsHorizontal())
            {
                ConfigureForHorizontal(Nodes);
            }
            else
            {
                ConfigureForVertical(Nodes);
            }
            void ConfigureForHorizontal(NativeArray<PortalNode> portalNodes)
            {
                int porPtr = windowNodes[i].PorPtr;
                int portalCount = 0;
                bool wasUnwalkable = true;
                Index2 bound1 = new Index2();
                Index2 bound2 = new Index2();
                int startCol = window.BottomLeftBoundary.C;
                int lastCol = window.TopRightBoundary.C;
                int row1 = window.BottomLeftBoundary.R;
                int row2 = window.TopRightBoundary.R;
                for (int j = startCol; j <= lastCol; j++)
                {
                    int index1 = row1 * tileAmount + j;
                    int index2 = row2 * tileAmount + j;
                    if (costs[index1] != byte.MaxValue && costs[index2] != byte.MaxValue)
                    {
                        if (wasUnwalkable)
                        {
                            bound1 = new Index2(row1, j);
                            bound2 = new Index2(row2, j);
                            wasUnwalkable = false;
                        }
                        else
                        {
                            bound2 = new Index2(row2, j);
                        }
                    }
                    if ((costs[index1] == byte.MaxValue || costs[index2] == byte.MaxValue) && !wasUnwalkable)
                    {
                        Portal portal = GetPortalBetween(bound1, bound2, true);
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    Portal portal = GetPortalBetween(bound1, bound2, true);
                    portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                }
            }
            void ConfigureForVertical(NativeArray<PortalNode> portalNodes)
            {
                int porPtr = windowNodes[i].PorPtr;
                int portalCount = 0;
                bool wasUnwalkable = true;
                Index2 bound1 = new Index2();
                Index2 bound2 = new Index2();
                int startRow = window.BottomLeftBoundary.R;
                int lastRow = window.TopRightBoundary.R;
                int col1 = window.BottomLeftBoundary.C;
                int col2 = window.TopRightBoundary.C;
                for (int j = startRow; j <= lastRow; j++)
                {
                    int index1 = j * tileAmount + col1;
                    int index2 = j * tileAmount + col2;
                    if (costs[index1] != byte.MaxValue && costs[index2] != byte.MaxValue)
                    {
                        if (wasUnwalkable)
                        {
                            bound1 = new Index2(j, col1);
                            bound2 = new Index2(j, col2);
                            wasUnwalkable = false;
                        }
                        else
                        {
                            bound2 = new Index2(j, col2);
                        }
                    }
                    if ((costs[index1] == byte.MaxValue || costs[index2] == byte.MaxValue) && !wasUnwalkable)
                    {
                        Portal portal = GetPortalBetween(bound1, bound2, false);
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    Portal portal = GetPortalBetween(bound1, bound2, false);
                    portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                }
            }
        }
        Portal GetPortalBetween(Index2 boundary1, Index2 boundary2, bool isHorizontal)
        {
            Portal portal;
            if (isHorizontal)
            {
                int col = (boundary1.C + boundary2.C) / 2;
                portal = new Portal(new Index2(boundary1.R, col), new Index2(boundary2.R, col));
            }
            else
            {
                int row = (boundary1.R + boundary2.R) / 2;
                portal = new Portal(new Index2(row, boundary1.C), new Index2(row, boundary2.C));
            }
            return portal;
        }
    }
    public void ConfigurePortalToPortalPtrs(AStarGrid aStarGrid, SectorArray sectorArray, WindowArray windowArray, int tileAmount)
    {
        NativeArray<SectorNode> sectorNodes = sectorArray.Nodes;

        for(int i = 0; i < sectorNodes.Length; i++)
        {
            Sector pickedSector = sectorNodes[i].Sector;
            NativeArray<int> portalIndicies = sectorArray.GetPortalIndicies(sectorNodes[i], windowArray.Nodes);
            for(int j = 0; j < portalIndicies.Length; j++)
            {
                //for each portal, set it "target" and calculate distances of others
                PortalNode sourcePortalNode = Nodes[portalIndicies[j]];
                Portal sourcePortal = sourcePortalNode.Portal;
                Index2 sourceIndex = pickedSector.ContainsIndex(sourcePortal.Index1) ? sourcePortal.Index1 : sourcePortal.Index2;
                NativeArray<AStarTile> integratedCosts = aStarGrid.GetIntegratedCostsFor(pickedSector, sourceIndex);

                for (int k = j + 1; k < portalIndicies.Length; k++)
                {
                    PortalNode targetPortalNode = Nodes[portalIndicies[k]];
                    Portal targetPortal = targetPortalNode.Portal;
                    Index2 targetIndex2 = pickedSector.ContainsIndex(targetPortal.Index1) ? targetPortal.Index1 : targetPortal.Index2;
                    int targetIndex = Index2.ToIndex(targetIndex2, tileAmount);
                    float cost = integratedCosts[targetIndex].IntegratedCost;

                    if(cost == float.MaxValue) { continue; }

                    //set for target
                    targetPortalNode.PorToPorCnt++;
                    Nodes[portalIndicies[k]] = targetPortalNode;
                    PorPtrs[targetPortalNode.PorToPorPtr + targetPortalNode.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[j]);

                    //set for source
                    sourcePortalNode.PorToPorCnt++;
                    PorPtrs[sourcePortalNode.PorToPorPtr + sourcePortalNode.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[k]);
                }
                Nodes[portalIndicies[j]] = sourcePortalNode;
            }
        }
    }
}
