using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct IntegrationFieldJob : IJob
    {
        internal int2 TargetIndex;
        internal int SectorIndex;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        [NativeDisableContainerSafetyRestriction] internal NativeSlice<IntegrationTile> IntegrationField;
        [ReadOnly] internal NativeParallelMultiHashMap<int, ActiveWaveFront> SectorToWaveFrontsMap;
        [ReadOnly] internal UnsafeList<int> SectorToPicked;
        [ReadOnly] internal NativeSlice<byte> Costs;
        public void Execute()
        {
            //DATA
            NativeSlice<IntegrationTile> integrationField = IntegrationField;
            NativeSlice<byte> costs = Costs;
            NativeQueue<LocalIndex1d> integrationQueue = new NativeQueue<LocalIndex1d>(Allocator.Temp);
            int sectorColAmount = SectorColAmount;
            int sectorTileAmount = sectorColAmount * sectorColAmount;

            //Reset
            const IntegrationMark markResetMask = IntegrationMark.LOSPass | IntegrationMark.LOSBlock | IntegrationMark.LOSC;
            for(int i = 0; i < integrationField.Length; i++)
            {
                IntegrationTile tile = integrationField[i];
                tile.Cost = float.MaxValue;
                tile.Mark &= markResetMask;
                integrationField[i] = tile;
            }

            ///////////LOOKUP TABLE////////////////
            ///////////////////////////////////////
            int nLocal1d;
            int eLocal1d;
            int sLocal1d;
            int wLocal1d;
            int neLocal1d;
            int seLocal1d;
            int swLocal1d;
            int nwLocal1d;
            bool nBlocked;
            bool eBlocked;
            bool sBlocked;
            bool wBlocked;
            float curIntCost;
            float nIntCost;
            float eIntCost;
            float sIntCost;
            float wIntCost;
            float neIntCost;
            float seIntCost;
            float swIntCost;
            float nwIntCost;
            bool nAvailable;
            bool eAvailable;
            bool sAvailable;
            bool wAvailable;
            ///////////////////////////////////////////////
            //CODE
            int targetSector1d = FlowFieldUtilities.GetSector1D(TargetIndex, sectorColAmount, SectorMatrixColAmount);
            if (SectorIndex == targetSector1d)
            {
                int targetLocal1d = FlowFieldUtilities.GetLocal1D(TargetIndex, sectorColAmount);
                IntegrationTile startTile = integrationField[targetLocal1d];
                startTile.Cost = 0f;
                integrationField[targetLocal1d] = startTile;
                SetLookupTable(targetLocal1d);
                Enqueue();
            }

            NativeParallelMultiHashMap<int, ActiveWaveFront>.Enumerator enumerator = SectorToWaveFrontsMap.GetValuesForKey(SectorIndex);

            while (enumerator.MoveNext())
            {
                ActiveWaveFront front = enumerator.Current;
                IntegrationTile startTile = integrationField[front.LocalIndex];
                integrationField[front.LocalIndex] = new IntegrationTile()
                {
                    Cost = front.Distance,
                    Mark = startTile.Mark,
                };
            }
            enumerator.Reset();
            while (enumerator.MoveNext())
            {
                int index = enumerator.Current.LocalIndex;
                SetLookupTable(index);
                Enqueue();
            }
            while (!integrationQueue.IsEmpty())
            {
                LocalIndex1d cur = integrationQueue.Dequeue();
                SetLookupTable(cur.index);
                float newCost = GetCost();
                IntegrationTile tile = integrationField[cur.index];
                tile.Cost = newCost;
                tile.Mark = ~((~tile.Mark) | IntegrationMark.Awaiting);
                curIntCost = newCost;
                integrationField[cur.index] = tile;
                Enqueue();
            }
            //HELPERS
            void SetLookupTable(int curLocal1d)
            {
                //LOCAL INDICIES
                nLocal1d = curLocal1d + sectorColAmount;
                eLocal1d = curLocal1d + 1;
                sLocal1d = curLocal1d - sectorColAmount;
                wLocal1d = curLocal1d - 1;
                neLocal1d = nLocal1d + 1;
                seLocal1d = sLocal1d + 1;
                swLocal1d = sLocal1d - 1;
                nwLocal1d = nLocal1d - 1;

                //OVERFLOWS
                bool nLocalOverflow = nLocal1d >= sectorTileAmount;
                bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
                bool sLocalOverflow = sLocal1d < 0;
                bool wLocalOverflow = (curLocal1d % sectorColAmount) == 0;

                nLocal1d = math.select(nLocal1d, curLocal1d, nLocalOverflow);
                eLocal1d = math.select(eLocal1d, curLocal1d, eLocalOverflow);
                sLocal1d = math.select(sLocal1d, curLocal1d, sLocalOverflow);
                wLocal1d = math.select(wLocal1d, curLocal1d, wLocalOverflow);
                neLocal1d = math.select(neLocal1d, curLocal1d, nLocalOverflow || eLocalOverflow);
                seLocal1d = math.select(seLocal1d, curLocal1d, sLocalOverflow || eLocalOverflow);
                swLocal1d = math.select(swLocal1d, curLocal1d, sLocalOverflow || wLocalOverflow);
                nwLocal1d = math.select(nwLocal1d, curLocal1d, nLocalOverflow || wLocalOverflow);

                //COSTS
                nBlocked = costs[nLocal1d] == byte.MaxValue;
                eBlocked = costs[eLocal1d] == byte.MaxValue;
                sBlocked = costs[sLocal1d] == byte.MaxValue;
                wBlocked = costs[wLocal1d] == byte.MaxValue;

                IntegrationMark nMark = IntegrationMark.None;
                IntegrationMark eMark = IntegrationMark.None;
                IntegrationMark sMark = IntegrationMark.None;
                IntegrationMark wMark = IntegrationMark.None;

                nMark = integrationField[nLocal1d].Mark;
                eMark = integrationField[eLocal1d].Mark;
                sMark = integrationField[sLocal1d].Mark;
                wMark = integrationField[wLocal1d].Mark;

                //INTEGRATED COSTS
                curIntCost = integrationField[curLocal1d].Cost;
                nIntCost = integrationField[nLocal1d].Cost;
                eIntCost = integrationField[eLocal1d].Cost;
                sIntCost = integrationField[sLocal1d].Cost;
                wIntCost = integrationField[wLocal1d].Cost;
                neIntCost = integrationField[neLocal1d].Cost;
                seIntCost = integrationField[seLocal1d].Cost;
                swIntCost = integrationField[swLocal1d].Cost;
                nwIntCost = integrationField[nwLocal1d].Cost;

                //AVAILABILITY
                nAvailable = !nBlocked && !nLocalOverflow && (nMark & IntegrationMark.Awaiting) != IntegrationMark.Awaiting;
                eAvailable = !eBlocked && !eLocalOverflow && (eMark & IntegrationMark.Awaiting) != IntegrationMark.Awaiting;
                sAvailable = !sBlocked && !sLocalOverflow && (sMark & IntegrationMark.Awaiting) != IntegrationMark.Awaiting;
                wAvailable = !wBlocked && !wLocalOverflow && (wMark & IntegrationMark.Awaiting) != IntegrationMark.Awaiting;
            }
            void Enqueue()
            {
                float nDif = nIntCost - curIntCost;
                float eDif = eIntCost - curIntCost;
                float sDif = sIntCost - curIntCost;
                float wDif = wIntCost - curIntCost;
                if (nAvailable && nDif > 1f)
                {
                    IntegrationTile tile = integrationField[nLocal1d];
                    tile.Mark |= IntegrationMark.Awaiting;
                    integrationField[nLocal1d] = tile;
                    integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, 0));
                }
                if (eAvailable && eDif > 1f)
                {
                    IntegrationTile tile = integrationField[eLocal1d];
                    tile.Mark |= IntegrationMark.Awaiting;
                    integrationField[eLocal1d] = tile;
                    integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, 0));
                }
                if (sAvailable && sDif > 1f)
                {
                    IntegrationTile tile = integrationField[sLocal1d];
                    tile.Mark |= IntegrationMark.Awaiting;
                    integrationField[sLocal1d] = tile;
                    integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, 0));
                }
                if (wAvailable && wDif > 1f)
                {
                    IntegrationTile tile = integrationField[wLocal1d];
                    tile.Mark |= IntegrationMark.Awaiting;
                    integrationField[wLocal1d] = tile;
                    integrationQueue.Enqueue(new LocalIndex1d(wLocal1d, 0));
                }
            }
            float GetCost()
            {
                float costToReturn = float.MaxValue;
                float nCost = nIntCost + 1f;
                float eCost = eIntCost + 1f;
                float sCost = sIntCost + 1f;
                float wCost = wIntCost + 1f;
                float neCost = math.select(neIntCost + 1.4f, float.MaxValue, nBlocked && eBlocked);
                float seCost = math.select(seIntCost + 1.4f, float.MaxValue, sBlocked && eBlocked);
                float swCost = math.select(swIntCost + 1.4f, float.MaxValue, sBlocked && wBlocked);
                float nwCost = math.select(nwIntCost + 1.4f, float.MaxValue, nBlocked && wBlocked);

                costToReturn = math.select(costToReturn, nCost, nCost < costToReturn);
                costToReturn = math.select(costToReturn, eCost, eCost < costToReturn);
                costToReturn = math.select(costToReturn, sCost, sCost < costToReturn);
                costToReturn = math.select(costToReturn, wCost, wCost < costToReturn);
                costToReturn = math.select(costToReturn, neCost, neCost < costToReturn);
                costToReturn = math.select(costToReturn, seCost, seCost < costToReturn);
                costToReturn = math.select(costToReturn, swCost, swCost < costToReturn);
                costToReturn = math.select(costToReturn, nwCost, nwCost < costToReturn);
                return costToReturn;
            }
        }
    }

}