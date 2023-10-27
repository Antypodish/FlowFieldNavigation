using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
[BurstCompile]
public struct NewActivePortalSubmitJob
{
    public NativeArray<int> PickedToSectors;
    public NativeArray<UnsafeList<ActivePortal>> ActivePortalListArray;


}