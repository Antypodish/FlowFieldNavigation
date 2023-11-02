using UnityEngine;
using Unity.Burst;

public struct PortalNode
{
    public Portal Portal1;
    public Portal Portal2;
    public int WinPtr;
    public int IslandIndex;

    public PortalNode(Portal portal1, Portal portal2, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = portal2;
        WinPtr = winPtr;
        IslandIndex = 0;
    }
    public PortalNode(Portal portal1, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = new Portal();
        WinPtr = winPtr;
        IslandIndex = 0;
    }
    public Vector3 GetPosition(float tileSize)
    {
        Index2 index1 = Portal1.Index;
        Index2 index2 = Portal2.Index;

        Vector3 pos1 = new Vector3(tileSize / 2 + tileSize * index1.C, 0.1f, tileSize / 2 + tileSize * index1.R);
        Vector3 pos2 = new Vector3(tileSize / 2 + tileSize * index2.C, 0.1f, tileSize / 2 + tileSize * index2.R);

        return (pos1 + pos2) / 2;
    }
    public bool IsIslandValid()
    {
        return IslandIndex != 0;
    }
}