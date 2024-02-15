

namespace FlowFieldNavigation
{
    internal struct IntegrationTile
    {
        internal float Cost;
        internal IntegrationMark Mark;

        internal IntegrationTile(float cost, IntegrationMark mark)
        {
            Cost = cost;
            Mark = mark;
        }
    }

}