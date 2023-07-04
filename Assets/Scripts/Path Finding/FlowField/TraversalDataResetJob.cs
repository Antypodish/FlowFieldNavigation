using Unity.Collections;
using Unity.Jobs;

public struct TraversalDataResetJob : IJobParallelFor
{
    public NativeArray<PortalTraversalData> TraversalDataArray;

    public void Execute(int index)
    {
        TraversalDataArray[index] = new PortalTraversalData()
        {
            fCost = float.MaxValue,
            gCost = float.MaxValue,
            hCost = float.MaxValue,
            mark = PortalTraversalMark.None,
            originIndex = index,
        };
    }
}
