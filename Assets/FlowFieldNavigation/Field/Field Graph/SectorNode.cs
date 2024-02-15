using Unity.Collections;

namespace FlowFieldNavigation
{

    internal struct SectorNode
    {
        internal Sector Sector;
        internal int SecToWinPtr;
        internal int SecToWinCnt;
        internal int SectorIslandPortalIndex;
        internal bool IsIslandField;
        internal SectorNode(Sector sector, int secToWinCnt, int secToWinPtr)
        {
            Sector = sector;
            SecToWinCnt = secToWinCnt;
            SecToWinPtr = secToWinPtr;
            IsIslandField = false;
            SectorIslandPortalIndex = -1;
        }
        internal bool IsIslandValid()
        {
            return SectorIslandPortalIndex != -1;
        }
        internal bool HasIsland()
        {
            return IsIslandField || SectorIslandPortalIndex != -1;
        }
    }

}