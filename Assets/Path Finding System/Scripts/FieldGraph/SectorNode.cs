using Unity.Collections;

public struct SectorNode
{
    public Sector Sector;
    public int SecToWinPtr;
    public int SecToWinCnt;

    public SectorNode(Sector sector, int secToWinCnt, int secToWinPtr)
    {
        Sector = sector;
        SecToWinCnt = secToWinCnt;
        SecToWinPtr = secToWinPtr;
    }
}


