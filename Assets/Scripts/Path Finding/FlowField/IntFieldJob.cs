using System.Security.Cryptography.X509Certificates;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct IntFieldJob : IJob
{
    public int InitialWaveFront;
    public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<DirectionData> DirectionData;
    public void Execute()
    {
        RunBFS();
    }
    void RunBFS()
    {
        //DATA
        NativeArray<byte> costs = Costs;
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        NativeQueue<int> integrationQueue = new NativeQueue<int>(Allocator.Temp); ;
        //CODE

        int targetLocalIndex = InitialWaveFront;
        IntegrationTile targetTile = IntegrationField[targetLocalIndex];
        targetTile.Cost = 0f;
        targetTile.Mark = IntegrationMark.Wave1;
        IntegrationField[targetLocalIndex] = targetTile;
        Enqueue(DirectionData[targetLocalIndex]);
        while (!integrationQueue.IsEmpty())
        {
            int index = integrationQueue.Dequeue();
            IntegrationTile tile = IntegrationField[index];
            tile.Cost = GetCost(DirectionData[index]);
            IntegrationField[index] = tile;
            Enqueue(DirectionData[index]);
        }

        //HELPERS
        void Enqueue(DirectionData directions)
        {
            int n = directions.N;
            int e = directions.E;
            int s = directions.S;
            int w = directions.W;
            bool isNorthAvailable = integrationField[n].Mark == IntegrationMark.None;
            bool isEastAvailable = integrationField[e].Mark == IntegrationMark.None;
            bool isSouthAvailable = integrationField[s].Mark == IntegrationMark.None;
            bool isWestAvailable = integrationField[w].Mark == IntegrationMark.None;
            if (isNorthAvailable)
            {
                integrationQueue.Enqueue(n);
                IntegrationTile tile = integrationField[n];
                tile.Mark = IntegrationMark.Wave1;
                integrationField[n] = tile;
            }
            if (isEastAvailable)
            {
                integrationQueue.Enqueue(e);
                IntegrationTile tile = integrationField[e];
                tile.Mark = IntegrationMark.Wave1;
                integrationField[e] = tile;
            }
            if (isSouthAvailable)
            {
                integrationQueue.Enqueue(s);
                IntegrationTile tile = integrationField[s];
                tile.Mark = IntegrationMark.Wave1;
                integrationField[s] = tile;
            }
            if (isWestAvailable)
            {
                integrationQueue.Enqueue(w);
                IntegrationTile tile = integrationField[w];
                tile.Mark = IntegrationMark.Wave1;
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
            float swCost = integrationField[directions.SW].Cost + 1f;
            float nwCost = integrationField[directions.NW].Cost + 1f;

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
    Absolute = 0,
    None = 1,
    Wave1 = 2,
    Wave2 = 3,
    Wave3 = 4,
    Wave4 = 5,
    Wave5 = 6,
    Wave6 = 7,
    Wave7 = 8,
    Wave8 = 9,
    Wave9 = 10,
    Wave10 = 11,
    Wave11 = 12,
    Wave12 = 13,
    Wave13 = 14,
    Wave14 = 15,
    Wave15 = 16,
}
