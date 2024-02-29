using Unity.Mathematics;


namespace FlowFieldNavigation
{
    internal struct CostEdit
    {
        internal int2 BotLeftBound;
        internal int2 TopRightBound;
        internal CostEditType EditType;
    }
    internal enum CostEditType : byte
    {
        Set,
        Clear,
    }

}