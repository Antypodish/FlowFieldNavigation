using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct CostFieldEditJob : IJob
{
    public Index2 Bound1;
    public Index2 Bound2;
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> WinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> SecPtrs;
    public NativeArray<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorPtrs;

    public AStarGrid _aStarGrid;
    public NativeArray<byte> _costs;
    public NativeArray<DirectionData> _directions;

    public int _fieldTileAmount;
    public float _fieldTileSize;
    public int _sectorTileAmount;
    public int _sectorMatrixSize;
    public int _portalPerWindow;

    public void Execute()
    {

    }
}
