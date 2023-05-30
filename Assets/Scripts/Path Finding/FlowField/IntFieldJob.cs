using System.Security.Cryptography.X509Certificates;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

[BurstCompile]
public struct IntFieldJob : IJob
{
    public NativeArray<IntegrationTile> IntegrationField;
    public NativeQueue<int> IntegrationQueue;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<DirectionData> Directions;
    public void Execute()
    {
        Integrate();
    }
    void Integrate()
    {
        //DATA
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        NativeArray<byte> costs = Costs;
        NativeQueue<int> integrationQueue = IntegrationQueue;
        //CODE

        while (!integrationQueue.IsEmpty())
        {
            int index = integrationQueue.Dequeue();
            IntegrationTile tile = IntegrationField[index];
            tile.Cost = GetCost(Directions[index]);
            IntegrationField[index] = tile;
            Enqueue(Directions[index]);
        }

        //HELPERS
        void Enqueue(DirectionData directions)
        {
            int n = directions.N;
            int e = directions.E;
            int s = directions.S;
            int w = directions.W;
            byte nCost = costs[n];
            byte eCost = costs[e];
            byte sCost = costs[s];
            byte wCost = costs[w];
            bool isNorthAvailable = integrationField[n].Mark == IntegrationMark.Relevant && nCost != byte.MaxValue;
            bool isEastAvailable = integrationField[e].Mark == IntegrationMark.Relevant && eCost != byte.MaxValue;
            bool isSouthAvailable = integrationField[s].Mark == IntegrationMark.Relevant && sCost != byte.MaxValue;
            bool isWestAvailable = integrationField[w].Mark == IntegrationMark.Relevant && wCost != byte.MaxValue;
            
            if (isNorthAvailable)
            {
                integrationQueue.Enqueue(n);
                IntegrationTile tile = integrationField[n];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[n] = tile;
            }
            if (isEastAvailable)
            {
                integrationQueue.Enqueue(e);
                IntegrationTile tile = integrationField[e];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[e] = tile;
            }
            if (isSouthAvailable)
            {
                integrationQueue.Enqueue(s);
                IntegrationTile tile = integrationField[s];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[s] = tile;
            }
            if (isWestAvailable)
            {
                integrationQueue.Enqueue(w);
                IntegrationTile tile = integrationField[w];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[w] = tile;
            }
        }
        float GetCost(DirectionData directions)
        {
            float costToReturn = float.MaxValue;
            float nCost = integrationField[directions.N].Cost + 1f;
            float eCost = integrationField[directions.E].Cost + 1f;
            float sCost = integrationField[directions.S].Cost + 1f;
            float wCost = integrationField[directions.W].Cost + 1f;
            float neCost = integrationField[directions.NE].Cost + 1.4f;
            float seCost = integrationField[directions.SE].Cost + 1.4f;
            float swCost = integrationField[directions.SW].Cost + 1.4f;
            float nwCost = integrationField[directions.NW].Cost + 1.4f;

            if (nCost < costToReturn) { costToReturn = nCost; }
            if (eCost < costToReturn) { costToReturn = eCost; }
            if (sCost < costToReturn) { costToReturn = sCost; }
            if (wCost < costToReturn) { costToReturn = wCost; }
            if (neCost < costToReturn) { costToReturn = neCost; }
            if (seCost < costToReturn) { costToReturn = seCost; }
            if (swCost < costToReturn) { costToReturn = swCost; }
            if (nwCost < costToReturn) { costToReturn = nwCost; }
            return costToReturn;
        }
    }
}
public struct IntegrationTile
{
    public float Cost;
    public IntegrationMark Mark;

    public IntegrationTile(float cost, IntegrationMark mark)
    {
        Cost = cost;
        Mark = mark;
    }
}
public enum IntegrationMark : byte
{
    Irrelevant,
    Relevant,
    LOSPass,
    LOSBlock,
    LOSC,
    Awaiting,
}
