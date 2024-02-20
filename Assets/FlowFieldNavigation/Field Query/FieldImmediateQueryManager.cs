using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using System;

namespace FlowFieldNavigation
{
    internal class FieldImmediateQueryManager
    {
        FlowFieldNavigationManager _navManager;

        internal FieldImmediateQueryManager(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
        }
        
        internal bool IsClearBettween(float3 start3, float3 end3, int fieldIndex, float stopDistance = 0f)
        {
            float tileSize = FlowFieldUtilities.TileSize;
            float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
            int sectorColAmount = FlowFieldUtilities.SectorColAmount;
            int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
            int SectorTileAmount = FlowFieldUtilities.SectorTileAmount;
            NativeArray<byte> costField = _navManager.FieldDataContainer.GetCostFieldWithOffset(fieldIndex).Costs;

            float2 start = new float2(start3.x, start3.z);
            float2 end = new float2(end3.x, end3.z);
            float2 startToEnd = end - start;
            float startToEndLength = math.length(startToEnd);
            if (startToEndLength <= stopDistance) { return true; }
            float2 startToEndNormalized = math.select(startToEnd / startToEndLength, 0f, startToEndLength == 0); ;
            end = start + startToEndNormalized * stopDistance;
            ClipLineIfNecessary(ref start, ref end);
            return LineCast(start, end);

            void ClipLineIfNecessary(ref float2 p1, ref float2 p2)
            {

            }
            bool LineCast(float2 start, float2 end)
            {
                start += math.select(0f, 0.0001f, start.x == end.x);
                float2 leftPoint = math.select(end, start, start.x < end.x);
                float2 rigthPoint = math.select(start, end, start.x < end.x);
                float xMin = leftPoint.x;
                float xMax = rigthPoint.x;
                int2 leftIndex = FlowFieldUtilities.PosTo2D(leftPoint, tileSize, fieldGridStartPos);
                int2 rightIndex = FlowFieldUtilities.PosTo2D(rigthPoint, tileSize, fieldGridStartPos);

                float deltaX = (leftPoint.x - rigthPoint.x);
                float x1 = rigthPoint.x;
                float deltaY = (leftPoint.y - rigthPoint.y);
                float y1 = rigthPoint.y;
                for (int xIndex = leftIndex.x; xIndex <= rightIndex.x; xIndex++)
                {
                    float xLeft = fieldGridStartPos.x + xIndex * tileSize;
                    float xRight = xLeft + tileSize;
                    xLeft = math.max(xLeft, xMin);
                    xRight = math.min(xRight, xMax);

                    float tLeft = (xLeft - x1) / deltaX;
                    float tRight = (xRight - x1) / deltaX;
                    float yLeft = y1 + deltaY * tLeft;
                    float yRight = y1 + deltaY * tRight;

                    int yIndexLeft = (int)math.floor((yLeft - fieldGridStartPos.y) / tileSize);
                    int yIndexRight = (int)math.floor((yRight - fieldGridStartPos.y) / tileSize);
                    int yIndexMin = math.min(yIndexLeft, yIndexRight);
                    int yIndexMax = math.max(yIndexLeft, yIndexRight);

                    for (int yIndex = yIndexMin; yIndex <= yIndexMax; yIndex++)
                    {
                        int2 indexToPlot = new int2(xIndex, yIndex);
                        LocalIndex1d localToPlot = FlowFieldUtilities.GetLocal1D(indexToPlot, sectorColAmount, sectorMatrixColAmount);
                        if (costField[localToPlot.sector * SectorTileAmount + localToPlot.index] == byte.MaxValue)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
        internal NativeBitArray IsClearBetwen(NativeArray<LineCastData> linesToCast, int fieldIndex, Allocator allocator)
        {
            NativeBitArray returnBits = new NativeBitArray(linesToCast.Length, allocator);
            LineCastJob lineCast = new LineCastJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TileSize = FlowFieldUtilities.TileSize,
                CostField = _navManager.FieldDataContainer.GetCostFieldWithOffset(fieldIndex).Costs,
                LinesToCast = linesToCast,
                ResultBits = returnBits,
            };
            lineCast.Schedule(linesToCast.Length, 64).Complete();
            return returnBits;
        }
    }
}