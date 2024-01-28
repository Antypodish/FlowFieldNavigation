using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
internal struct PathSectorEditCheckJob : IJobParallelFor
{
    [ReadOnly] internal NativeArray<SectorBitArray> PathSectorIDArray;
    [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] internal NativeArray<PathState> PathStateArray;
    internal NativeArray<SectorBitArray>.ReadOnly FieldEditSectorIDArray;
    internal NativeArray<PathRoutineData> PathRoutineDataArray;
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
                PathReconstructionFlag = routineData.PathReconstructionFlag,
                Task = routineData.Task | PathTask.Reconstruct,
            };
        }
    }
}
