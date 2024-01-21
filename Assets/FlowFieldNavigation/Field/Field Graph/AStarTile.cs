internal struct AStarTile
{
    internal bool Enqueued;
    internal float IntegratedCost;

    internal AStarTile(float integratedCost, bool enqueued)
    {
        Enqueued = enqueued;
        IntegratedCost = integratedCost;
    }
}