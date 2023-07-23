public struct Portal
{
    public Index2 Index;
    public int PorToPorPtr;
    public byte PorToPorCnt;

    public Portal(Index2 index, int porToPorPtr)
    {
        Index = index;
        PorToPorPtr = porToPorPtr;
        PorToPorCnt = 0;
    }
}
