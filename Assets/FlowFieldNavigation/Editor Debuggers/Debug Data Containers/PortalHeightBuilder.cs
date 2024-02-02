using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
internal class PortalHeightBuilder
{
    PathfindingManager _pathfindingManager;
    NativeList<float> _heights;
    bool _isCreated;

    internal PortalHeightBuilder(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _isCreated = false;
    }
    internal NativeArray<float> GetPortalHeights(int offset)
    {
        if (!_isCreated) { Create(offset); }
        return _heights.AsArray();
    }

    void Create(int offset)
    {
        _isCreated = true;
        if (!_heights.IsCreated) { _heights = new NativeList<float>(Allocator.Persistent); }
        float tileSize = FlowFieldUtilities.TileSize;
        float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
        NativeArray<PortalNode> portalNodes = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(offset).PortalNodes;
        NativeArray<float2> portalPositions = new NativeArray<float2>(portalNodes.Length, Allocator.TempJob);
        _heights.Length = portalNodes.Length;

        PortalNodeToPosJob portaltopos = new PortalNodeToPosJob()
        {
            FieldGridStartPos = fieldGridStartPos,
            TileSize = tileSize,
            Nodes = portalNodes,
            Positions = portalPositions,
        };
        portaltopos.Schedule(portalNodes.Length, 64).Complete();

        PointHeightCalculationJob pointHeight = new PointHeightCalculationJob()
        {
            TriangleSpatialHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
            HeightMeshVerts = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
            Heights = _heights.AsArray(),
            Points = portalPositions,
        };
        pointHeight.Schedule(portalNodes.Length, 64).Complete();
    }
}