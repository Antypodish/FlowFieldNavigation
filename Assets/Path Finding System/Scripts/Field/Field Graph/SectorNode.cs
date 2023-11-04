using Unity.Collections;

public struct SectorNode
{
    public Sector Sector;
    public int SecToWinPtr;
    public int SecToWinCnt;
    public int SectorIslandPortalIndex;
    public bool IsIslandField;
    public SectorNode(Sector sector, int secToWinCnt, int secToWinPtr)
    {
        Sector = sector;
        SecToWinCnt = secToWinCnt;
        SecToWinPtr = secToWinPtr;
        IsIslandField = false;
        SectorIslandPortalIndex = -1;
    }
    public bool IsIslandValid()
    {
        return SectorIslandPortalIndex != -1;
    }
    public bool HasIsland()
    {
        return IsIslandField || SectorIslandPortalIndex != -1;
    }
}