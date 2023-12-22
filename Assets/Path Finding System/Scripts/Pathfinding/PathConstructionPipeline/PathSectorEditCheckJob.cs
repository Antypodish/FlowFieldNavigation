using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct PathSectorEditCheckJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<SectorBitArray> PathSectorIDArray;
    [ReadOnly] public NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] public NativeArray<PathState> PathStateArray;
    public NativeArray<SectorBitArray>.ReadOnly FieldEditSectorIDArray;
    public NativeArray<PathRoutineData> PathRoutineDataArray;
    public void Execute(int index)
    {
        if (PathStateArray[index] == PathState.Removed) { return; }
        SectorBitArray examinedPathSectorid = PathSectorIDArray[index];
        int offset = PathDestinationDataArray[index].Offset;
        SectorBitArray fieldEditSectorid = FieldEditSectorIDArray[offset];
        if (fieldEditSectorid.DoesMatchWith(examinedPathSectorid))
        {
            PathRoutineData routineData = PathRoutineDataArray[index];
            PathRoutineDataArray[index] = new PathRoutineData()
            {
                DestinationState = routineData.DestinationState,
                FlowRequestSourceCount = routineData.FlowRequestSourceCount,
                FlowRequestSourceStart = routineData.FlowRequestSourceStart,
                PathAdditionSourceCount = routineData.PathAdditionSourceCount,
                PathAdditionSourceStart = routineData.PathAdditionSourceStart,
                ReconstructionRequestIndex = routineData.ReconstructionRequestIndex,
                Task = routineData.Task | PathTask.Reconstruct,
            };
        }
    }
}
