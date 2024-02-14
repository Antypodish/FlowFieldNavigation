using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
internal class SectorCornerHeightBuilder
{
    FlowFieldNavigationManager _navigationManager;
    NativeArray<float> _sectorCornerHeights;
    bool _isCreated;
    internal SectorCornerHeightBuilder(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _isCreated = false;
    }
    internal NativeArray<float> GetSectorCornerHeights()
    {
        if (!_isCreated)
        {
            Create();
        }
        return _sectorCornerHeights;
    }

    void Create()
    {
        _isCreated = true;
        if (!_sectorCornerHeights.IsCreated) { _sectorCornerHeights = new NativeArray<float>(FlowFieldUtilities.SectorMatrixTileAmount * 4, Allocator.Persistent); }

        float sectorSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.SectorColAmount;
        float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        int sectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        float tileSize = FlowFieldUtilities.TileSize;
        NativeArray<float2> sectorCornerPositions = new NativeArray<float2>(FlowFieldUtilities.SectorMatrixTileAmount * 4, Allocator.TempJob);

        int sectorCornerPositionsIndex = 0;
        for(int r = 0; r < sectorMatrixRowAmount; r++)
        {
            for(int c = 0; c < sectorMatrixColAmount; c++)
            {
                int2 sector2d = new int2(c, r);
                int2 sectorStart2d = FlowFieldUtilities.GetSectorStartIndex(sector2d, sectorColAmount);
                float2 botLeft = FlowFieldUtilities.IndexToStartPos(sectorStart2d, tileSize, fieldGridStartPos);
                float2 topLeft = botLeft + new float2(0, sectorSize);
                float2 topRight = botLeft + new float2(sectorSize, sectorSize);
                float2 botRight = botLeft + new float2(sectorSize, 0);
                sectorCornerPositions[sectorCornerPositionsIndex++] = botLeft;
                sectorCornerPositions[sectorCornerPositionsIndex++] = topLeft;
                sectorCornerPositions[sectorCornerPositionsIndex++] = topRight;
                sectorCornerPositions[sectorCornerPositionsIndex++] = botRight;
            }
        }
        PointHeightCalculationJob poitnHeights = new PointHeightCalculationJob()
        {
            TriangleSpatialHashGrid = _navigationManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
            HeightMeshVerts = _navigationManager.FieldDataContainer.HeightMeshGenerator.Verticies.AsArray(),
            Heights = _sectorCornerHeights,
            Points = sectorCornerPositions,
        };
        poitnHeights.Schedule(sectorCornerPositions.Length, 64).Complete();
        sectorCornerPositions.Dispose();
    }
}
internal struct CornerHeights
{
    internal float BotLeft;
    internal float TopLeft;
    internal float TopRight;
    internal float BotRight;
}