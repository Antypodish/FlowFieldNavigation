internal struct Portal
{
    internal Index2 Index;
    internal int PorToPorPtr;
    internal byte PorToPorCnt;

    internal Portal(Index2 index, int porToPorPtr)
    {
        Index = index;
        PorToPorPtr = porToPorPtr;
        PorToPorCnt = 0;
    }
}
