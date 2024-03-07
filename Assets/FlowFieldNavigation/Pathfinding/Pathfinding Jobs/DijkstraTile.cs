namespace FlowFieldNavigation
{
    internal struct DijkstraTile
    {
        internal byte Cost;
        internal float IntegratedCost;
        internal bool IsAvailable;

        internal DijkstraTile(byte cost, float integratedCost, bool isAvailable)
        {
            Cost = cost;
            IntegratedCost = integratedCost;
            IsAvailable = isAvailable;
        }
    }

}

