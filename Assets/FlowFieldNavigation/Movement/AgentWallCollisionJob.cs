using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AgentWallCollisionJob : IJobParallelFor
{
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorMatrixColAmount;
    public int SectorTileAmount;
    public float TileSize;
    public float HalfTileSize;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementData;
    [ReadOnly] public NativeArray<UnsafeListReadOnly<byte>> CostFieldEachOffset;
    public NativeArray<float3> AgentPositionChangeBuffer;

    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementData[index];
        float agentRadiusWithoutHalfTile = agentData.Radius - HalfTileSize;
        float agentRadius = math.select(agentRadiusWithoutHalfTile % TileSize, agentData.Radius, agentRadiusWithoutHalfTile < 0);
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        int agentOffset = FlowFieldUtilities.RadiusToOffset(agentData.Radius, TileSize);
        UnsafeListReadOnly<byte> costs = CostFieldEachOffset[agentOffset];
        int2 agentGeneral2d = FlowFieldUtilities.PosTo2D(agentPos, TileSize);
        LocalIndex1d agentLocal = FlowFieldUtilities.GetLocal1D(agentGeneral2d, SectorColAmount, SectorMatrixColAmount);
        int2 cleanGeneral2d;
        if (costs[agentLocal.sector * SectorTileAmount + agentLocal.index] != byte.MaxValue)
        {
            cleanGeneral2d = agentGeneral2d;
            WallDirection wallDirections = GetWallData(cleanGeneral2d, costs);
            WallDirection wallDirectionToCollide = GetWallDirectionToCollide(agentPos, agentGeneral2d, cleanGeneral2d, wallDirections);
            if(wallDirectionToCollide == WallDirection.None) { return; }
            float2 clearTilePos = FlowFieldUtilities.IndexToPos(cleanGeneral2d, TileSize);
            float2 pushForce = GetPushForceOutside(agentPos, agentRadius, clearTilePos, wallDirectionToCollide);
            AgentPositionChangeBuffer[index] += new float3(pushForce.x, 0f, pushForce.y);
        }
        else
        {
            cleanGeneral2d = GetClosestCleanIndex(agentPos, CostFieldEachOffset[agentOffset]);
            WallDirection wallDirections = GetWallData(cleanGeneral2d, costs);
            WallDirection wallDirectionToCollide = GetWallDirectionToCollide(agentPos, agentGeneral2d, cleanGeneral2d, wallDirections);
            if (wallDirectionToCollide == WallDirection.None) { return; }
            float2 clearTilePos = FlowFieldUtilities.IndexToPos(cleanGeneral2d, TileSize);
            float2 pushForce = GetPushForceInside(agentPos, agentRadius, clearTilePos, wallDirectionToCollide);
            AgentPositionChangeBuffer[index] += new float3(pushForce.x, 0f, pushForce.y);
        }

    }
    float2 GetPushForceOutside(float2 agentPos, float agentRadius, float2 clearTilePos, WallDirection wallDirection)
    {
        float agentRadiusSqrd = agentRadius * agentRadius;
        bool notCloseEnough = true;
        float distanceSqrd = 0;
        float2 wall = 0;
        float2 change = 0;
        float changeLen = 0;
        switch (wallDirection)
        {
            case WallDirection.N:
                wall.y = clearTilePos.y + HalfTileSize;
                change.y = wall.y - (agentPos.y + agentRadius) - 0.0001f;
                distanceSqrd = (wall.y - agentPos.y) * (wall.y - agentPos.y);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.E:
                wall.x = clearTilePos.x + HalfTileSize;
                change.x = wall.x - (agentPos.x + agentRadius) - 0.0001f;
                distanceSqrd = (wall.x - agentPos.x) * (wall.x - agentPos.x);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.S:
                wall.y = clearTilePos.y - HalfTileSize;
                change.y = wall.y - (agentPos.y - agentRadius) + 0.0001f;
                distanceSqrd = (wall.y - agentPos.y) * (wall.y - agentPos.y);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.W:
                wall.x = clearTilePos.x - HalfTileSize;
                change.x = wall.x - (agentPos.x - agentRadius) + 0.0001f;
                distanceSqrd = (wall.x - agentPos.x) * (wall.x - agentPos.x);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.NE:
                wall = clearTilePos + HalfTileSize;
                change = agentPos - wall;
                changeLen = math.length(change);
                change = math.select(change / changeLen * (agentRadius - changeLen), 0, changeLen == 0 || agentRadius == changeLen);
                distanceSqrd = math.distancesq(wall, agentPos);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.SE:
                wall = clearTilePos + new float2(HalfTileSize, -HalfTileSize);
                change = agentPos - wall;
                changeLen = math.length(change);
                change = math.select(change / changeLen * (agentRadius - changeLen), 0, changeLen == 0 || agentRadius == changeLen);
                distanceSqrd = math.distancesq(wall, agentPos);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.SW:
                wall = clearTilePos - HalfTileSize;
                change = agentPos - wall;
                changeLen = math.length(change);
                change = math.select(change / changeLen * (agentRadius - changeLen), 0, changeLen == 0 || agentRadius == changeLen);
                distanceSqrd = math.distancesq(wall, agentPos);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
            case WallDirection.NW:
                wall = clearTilePos + new float2(-HalfTileSize, HalfTileSize);
                change = agentPos - wall;
                changeLen = math.length(change);
                change = math.select(change / changeLen * (agentRadius - changeLen), 0, changeLen == 0 || agentRadius == changeLen);
                distanceSqrd = math.distancesq(wall, agentPos);
                notCloseEnough = distanceSqrd > agentRadiusSqrd;
                break;
        }
        return math.select(change, 0, notCloseEnough);
    }
    float2 GetPushForceInside(float2 agentPos, float agentRadius, float2 clearTilePos, WallDirection wallDirection)
    {
        if(wallDirection == WallDirection.None) { return 0; }
        float2 wall = agentPos;
        switch (wallDirection)
        {
            case WallDirection.N:
                wall.y = clearTilePos.y + HalfTileSize;
                break;
            case WallDirection.E:
                wall.x = clearTilePos.x + HalfTileSize;
                break;
            case WallDirection.S:
                wall.y = clearTilePos.y - HalfTileSize;
                break;
            case WallDirection.W:
                wall.x = clearTilePos.x - HalfTileSize;
                break;
            case WallDirection.NE:
                wall = clearTilePos + HalfTileSize;
                break;
            case WallDirection.SE:
                wall = clearTilePos + new float2(HalfTileSize, -HalfTileSize);
                break;
            case WallDirection.SW:
                wall = clearTilePos - HalfTileSize;
                break;
            case WallDirection.NW:
                wall = clearTilePos + new float2(-HalfTileSize, HalfTileSize);
                break;
        }

        float agentRadiusSqrd = agentRadius * agentRadius;
        float2 change = wall - agentPos;
        float changeLen = math.length(change);
        float2 changeDir = math.select(change / changeLen, 0, changeLen == 0);
        float changeMag = changeLen + agentRadius;
        change = changeDir * changeMag;
        return change;
    }
    int2 GetClosestCleanIndex(float2 agentPosition, UnsafeListReadOnly<byte> costField)
    {
        int sectorTileAmount = SectorTileAmount;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int fieldColAmount = FieldColAmount;
        float tileSize = TileSize;

        int2 destinationIndex = FlowFieldUtilities.PosTo2D(agentPosition, TileSize);
        LocalIndex1d destinationLocal = FlowFieldUtilities.GetLocal1D(destinationIndex, SectorColAmount, SectorMatrixColAmount);
        if (costField[destinationLocal.sector * sectorTileAmount + destinationLocal.index] != byte.MaxValue) { return FlowFieldUtilities.PosTo2D(agentPosition, TileSize); }
        int destinationLocalIndex = destinationLocal.index;
        int destinationSector = destinationLocal.sector;

        int offset = 1;

        float pickedExtensionIndexCost = float.MaxValue;
        int pickedExtensionIndexLocalIndex = 0;
        int pickedExtensionIndexSector = 0;


        while (pickedExtensionIndexCost == float.MaxValue)
        {
            int2 topLeft = destinationIndex + new int2(-offset, offset);
            int2 topRight = destinationIndex + new int2(offset, offset);
            int2 botLeft = destinationIndex + new int2(-offset, -offset);
            int2 botRight = destinationIndex + new int2(offset, -offset);

            bool topOverflow = topLeft.y >= FieldRowAmount;
            bool botOverflow = botLeft.y < 0;
            bool rightOverflow = topRight.x >= FieldColAmount;
            bool leftOverflow = topLeft.x < 0;

            if (topOverflow && botOverflow && rightOverflow && leftOverflow) { return destinationIndex; }

            if (topOverflow)
            {
                topLeft.y = FieldRowAmount - 1;
                topRight.y = FieldRowAmount - 1;
            }
            if (botOverflow)
            {
                botLeft.y = 0;
                botRight.y = 0;
            }
            if (rightOverflow)
            {
                botRight.x = FieldColAmount - 1;
                topRight.x = FieldColAmount - 1;
            }
            if (leftOverflow)
            {
                topLeft.x = 0;
                botLeft.x = 0;
            }

            int topLeftSector = FlowFieldUtilities.GetSector1D(topLeft, sectorColAmount, SectorMatrixColAmount);
            int topRightSector = FlowFieldUtilities.GetSector1D(topRight, sectorColAmount, SectorMatrixColAmount);
            int botRightSector = FlowFieldUtilities.GetSector1D(botRight, sectorColAmount, SectorMatrixColAmount);
            int botLeftSector = FlowFieldUtilities.GetSector1D(botLeft, sectorColAmount, SectorMatrixColAmount);
            if (!topOverflow)
            {
                int rowToCheck = topLeft.y % SectorRowAmount;
                for (int i = topLeftSector; i <= topRightSector; i++)
                {
                    int colStart = math.select(0, topLeft.x % SectorColAmount, i == topLeftSector);
                    int colEnd = math.select(10, topRight.x % SectorColAmount, i == topRightSector);
                    ExtensionIndex checkedExtension = CheckSectorRow(i, rowToCheck, colStart, colEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!rightOverflow)
            {
                int colToCheck = topRight.x % SectorColAmount;
                for (int i = topRightSector; i >= botRightSector; i -= SectorMatrixColAmount)
                {
                    int rowStart = math.select(9, topRight.y % SectorRowAmount, i == topRightSector);
                    int rowEnd = math.select(-1, botRight.y % SectorRowAmount, i == botRightSector);
                    ExtensionIndex checkedExtension = CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!botOverflow)
            {
                int rowToCheck = botRight.y % SectorRowAmount;
                for (int i = botRightSector; i >= botLeftSector; i--)
                {
                    int colStart = math.select(9, botRight.x % SectorColAmount, i == botRightSector);
                    int colEnd = math.select(-1, botLeft.x % SectorColAmount, i == botLeftSector);
                    ExtensionIndex checkedExtension = CheckSectorRow(i, rowToCheck, colStart, colEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!leftOverflow)
            {
                int colToCheck = topLeft.x % SectorColAmount;
                for (int i = botLeftSector; i <= topLeftSector; i += SectorMatrixColAmount)
                {
                    int rowStart = math.select(0, botLeft.y % SectorRowAmount, i == botLeftSector);
                    int rowEnd = math.select(10, topLeft.y % SectorRowAmount, i == topLeftSector);
                    ExtensionIndex checkedExtension = CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            offset++;
        }

        int2 outputGeneral2d = FlowFieldUtilities.GetGeneral2d(pickedExtensionIndexLocalIndex, pickedExtensionIndexSector, sectorMatrixColAmount, sectorColAmount);
        return outputGeneral2d;

        ExtensionIndex CheckSectorRow(int sectorToCheck, int rowToCheck, int colToStart, int colToEnd)
        {
            float currentExtensionIndexCost = float.MaxValue;
            int currentExtensionIndexLocalIndex = 0;
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToCheck * sectorColAmount + colToStart;
            int checkRange = colToEnd - colToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * checkUnit;
                int localIndex = indexToCheck - sectorStride;
                byte cost = costField[indexToCheck];
                if (cost == byte.MaxValue) { continue; }
                float2 indexPos = FlowFieldUtilities.Local1dToPos(localIndex, sectorToCheck, sectorColAmount, sectorMatrixColAmount, fieldColAmount, tileSize);
                float newExtensionCost = math.distancesq(agentPosition, indexPos);
                if (newExtensionCost < currentExtensionIndexCost) { currentExtensionIndexCost = newExtensionCost; currentExtensionIndexLocalIndex = localIndex; }
            }
            return new ExtensionIndex()
            {
                SectorIndex = sectorToCheck,
                LocalIndex = currentExtensionIndexLocalIndex,
                Cost = currentExtensionIndexCost
            };
        }
        ExtensionIndex CheckSectorCol(int sectorToCheck, int colToCheck, int rowToStart, int rowToEnd)
        {
            float currentExtensionIndexCost = float.MaxValue;
            int currentExtensionIndexLocalIndex = 0;
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToStart * sectorColAmount + colToCheck;
            int checkRange = rowToEnd - rowToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * sectorColAmount * checkUnit;
                int localIndex = indexToCheck - sectorStride;
                byte cost = costField[indexToCheck];
                if (cost == byte.MaxValue) { continue; }
                float2 indexPos = FlowFieldUtilities.Local1dToPos(localIndex, sectorToCheck, sectorColAmount, sectorMatrixColAmount, fieldColAmount, tileSize);
                float newExtensionCost = math.distancesq(agentPosition, indexPos);
                if (newExtensionCost < currentExtensionIndexCost) { currentExtensionIndexCost = newExtensionCost; currentExtensionIndexLocalIndex = localIndex; }
            }
            return new ExtensionIndex()
            {
                SectorIndex = sectorToCheck,
                LocalIndex = currentExtensionIndexLocalIndex,
                Cost = currentExtensionIndexCost
            };
        }
    }
    private struct ExtensionIndex
    {
        public int LocalIndex;
        public int SectorIndex;
        public float Cost;

        public bool IsValid()
        {
            return Cost != float.MaxValue;
        }
    }
    WallDirection GetWallData(int2 index, UnsafeListReadOnly<byte> costs)
    {
        LocalIndex1d agentLocal = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
        int agnetLocalIndex = agentLocal.index;
        int agentSector = agentLocal.sector;

        int curLocal1d = agnetLocalIndex;
        int curSector1d = agentSector;

        int nLocal1d = curLocal1d + SectorColAmount;
        int eLocal1d = curLocal1d + 1;
        int sLocal1d = curLocal1d - SectorColAmount;
        int wLocal1d = curLocal1d - 1;
        int neLocal1d = nLocal1d + 1;
        int seLocal1d = sLocal1d + 1;
        int swLocal1d = sLocal1d - 1;
        int nwLocal1d = nLocal1d - 1;

        //OVERFLOWS
        bool nLocalOverflow = nLocal1d >= SectorTileAmount;
        bool eLocalOverflow = (eLocal1d % SectorColAmount) == 0;
        bool sLocalOverflow = sLocal1d < 0;
        bool wLocalOverflow = (curLocal1d % SectorColAmount) == 0;

        //SECTOR INDICIES
        int nSector1d = math.select(curSector1d, curSector1d + SectorMatrixColAmount, nLocalOverflow);
        int eSector1d = math.select(curSector1d, curSector1d + 1, eLocalOverflow);
        int sSector1d = math.select(curSector1d, curSector1d - SectorMatrixColAmount, sLocalOverflow);
        int wSector1d = math.select(curSector1d, curSector1d - 1, wLocalOverflow);
        int neSector1d = math.select(curSector1d, curSector1d + SectorMatrixColAmount, nLocalOverflow);
        neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
        int seSector1d = math.select(curSector1d, curSector1d - SectorMatrixColAmount, sLocalOverflow);
        seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
        int swSector1d = math.select(curSector1d, curSector1d - SectorMatrixColAmount, sLocalOverflow);
        swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
        int nwSector1d = math.select(curSector1d, curSector1d + SectorMatrixColAmount, nLocalOverflow);
        nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);


        nLocal1d = math.select(nLocal1d, curLocal1d - (SectorColAmount * SectorColAmount - SectorColAmount), nLocalOverflow);
        eLocal1d = math.select(eLocal1d, curLocal1d - SectorColAmount + 1, eLocalOverflow);
        sLocal1d = math.select(sLocal1d, curLocal1d + (SectorColAmount * SectorColAmount - SectorColAmount), sLocalOverflow);
        wLocal1d = math.select(wLocal1d, curLocal1d + SectorColAmount - 1, wLocalOverflow);
        neLocal1d = math.select(neLocal1d, neLocal1d - (SectorColAmount * SectorColAmount), nLocalOverflow);
        neLocal1d = math.select(neLocal1d, neLocal1d - SectorColAmount, eLocalOverflow);
        seLocal1d = math.select(seLocal1d, seLocal1d + (SectorColAmount * SectorColAmount), sLocalOverflow);
        seLocal1d = math.select(seLocal1d, seLocal1d - SectorColAmount, eLocalOverflow);
        swLocal1d = math.select(swLocal1d, swLocal1d + (SectorColAmount * SectorColAmount), sLocalOverflow);
        swLocal1d = math.select(swLocal1d, swLocal1d + SectorColAmount, wLocalOverflow);
        nwLocal1d = math.select(nwLocal1d, nwLocal1d - (SectorColAmount * SectorColAmount), nLocalOverflow);
        nwLocal1d = math.select(nwLocal1d, nwLocal1d + SectorColAmount, wLocalOverflow);

        WallDirection nMask = (WallDirection) math.select(0, 1, costs[nLocal1d + nSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection eMask = (WallDirection) math.select(0, 2, costs[eLocal1d + eSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection sMask = (WallDirection) math.select(0, 4, costs[sLocal1d + sSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection wMask = (WallDirection) math.select(0, 8, costs[wLocal1d + wSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection neMask = (WallDirection) math.select(0, 16, costs[neLocal1d + neSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection seMask = (WallDirection) math.select(0, 32, costs[seLocal1d + seSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection swMask = (WallDirection) math.select(0, 64, costs[swLocal1d + swSector1d * SectorTileAmount] == byte.MaxValue);
        WallDirection nwMask = (WallDirection) math.select(0, 128, costs[nwLocal1d + nwSector1d * SectorTileAmount] == byte.MaxValue);

        return nMask | eMask | sMask | wMask | neMask | seMask | swMask | nwMask;
    }
    WallDirection GetWallDirectionToCollide(float2 agentPos, int2 agentGeneral2d, int2 general2dToCheck, WallDirection wallDirections)
    {
        float2 checkedIndexPos = FlowFieldUtilities.IndexToPos(general2dToCheck, TileSize);

        float2 nPos = checkedIndexPos;
        float2 ePos = checkedIndexPos;
        float2 sPos = checkedIndexPos;
        float2 wPos = checkedIndexPos;
        float2 nePos = checkedIndexPos;
        float2 sePos = checkedIndexPos;
        float2 swPos = checkedIndexPos;
        float2 nwPos = checkedIndexPos;
        nPos.y += HalfTileSize;
        ePos.x += HalfTileSize;
        sPos.y -= HalfTileSize;
        wPos.x -= HalfTileSize;
        nePos += new float2(HalfTileSize, HalfTileSize);
        sePos += new float2(HalfTileSize, -HalfTileSize);
        swPos += new float2(-HalfTileSize, -HalfTileSize);
        nwPos += new float2(-HalfTileSize, HalfTileSize);

        float nDistSquqred = math.distancesq(nPos, agentPos);
        float eDistSquqred = math.distancesq(ePos, agentPos);
        float sDistSquqred = math.distancesq(sPos, agentPos);
        float wDistSquqred = math.distancesq(wPos, agentPos);
        float neDistSquqred = math.distancesq(nePos, agentPos);
        float seDistSquqred = math.distancesq(sePos, agentPos);
        float swDistSquqred = math.distancesq(swPos, agentPos);
        float nwDistSquqred = math.distancesq(nwPos, agentPos);

        bool hasN = (wallDirections & WallDirection.N) == WallDirection.N;
        bool hasE = (wallDirections & WallDirection.E) == WallDirection.E;
        bool hasS = (wallDirections & WallDirection.S) == WallDirection.S;
        bool hasW = (wallDirections & WallDirection.W) == WallDirection.W;
        bool hasNE = (wallDirections & WallDirection.NE) == WallDirection.NE;
        bool hasSE = (wallDirections & WallDirection.SE) == WallDirection.SE;
        bool hasSW = (wallDirections & WallDirection.SW) == WallDirection.SW;
        bool hasNW = (wallDirections & WallDirection.NW) == WallDirection.NW;
        bool canCollideNE = (agentPos.x >= nePos.x && agentPos.y >= nePos.y) || (!hasN && !hasE);
        bool canCollideSE = (agentPos.x >= sePos.x && agentPos.y <= sePos.y) || (!hasS && !hasE);
        bool canCollideSW = (agentPos.x <= swPos.x && agentPos.y <= swPos.y) || (!hasS && !hasW);
        bool canCollideNW = (agentPos.x <= nwPos.x && agentPos.y >= nwPos.y) || (!hasN && !hasW);

        WallDirection dirToReturn = WallDirection.None;
        float minDist = float.MaxValue;
        if(nDistSquqred < minDist && hasN) { dirToReturn = WallDirection.N; minDist = nDistSquqred; }
        if(eDistSquqred < minDist && hasE) { dirToReturn = WallDirection.E; minDist = eDistSquqred; }
        if(sDistSquqred < minDist && hasS) { dirToReturn = WallDirection.S; minDist = sDistSquqred; }
        if(wDistSquqred < minDist && hasW) { dirToReturn = WallDirection.W; minDist = wDistSquqred; }
        if(neDistSquqred < minDist && hasNE && canCollideNE) { dirToReturn = WallDirection.NE; minDist = neDistSquqred; }
        if(seDistSquqred < minDist && hasSE && canCollideSE) { dirToReturn = WallDirection.SE; minDist = seDistSquqred; }
        if(swDistSquqred < minDist && hasSW && canCollideSW) { dirToReturn = WallDirection.SW; minDist = swDistSquqred; }
        if(nwDistSquqred < minDist && hasNW && canCollideNW) { dirToReturn = WallDirection.NW; minDist = nwDistSquqred; }
        return dirToReturn;
    }
    private enum WallDirection : byte
    {
        None = 0,
        N = 1,
        E = 2,
        S = 4,
        W = 8,
        NE = 16,
        SE = 32,
        SW = 64,
        NW = 128,
    }
}
