using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
internal struct PortalNode
{
    internal Portal Portal1;
    internal Portal Portal2;
    internal int WinPtr;
    internal int IslandIndex;

    internal PortalNode(Portal portal1, Portal portal2, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = portal2;
        WinPtr = winPtr;
        IslandIndex = 0;
    }
    internal PortalNode(Portal portal1, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = new Portal();
        WinPtr = winPtr;
        IslandIndex = 0;
    }
    internal Vector3 GetPosition(float tileSize, float2 gridStartPos)
    {
        int2 index1 = new int2(Portal1.Index.C, Portal1.Index.R);
        int2 index2 = new int2(Portal2.Index.C, Portal2.Index.R);

        float2 pos1 = FlowFieldUtilities.IndexToPos(index1, tileSize, gridStartPos);
        float2 pos2 = FlowFieldUtilities.IndexToPos(index2, tileSize, gridStartPos);
        float2 pos = (pos1 + pos2) / 2;
        return new Vector3(pos.x, 0.1f, pos.y);
    }
    internal bool IsIslandValid()
    {
        return IslandIndex != 0;
    }
}