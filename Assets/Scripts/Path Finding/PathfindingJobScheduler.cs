using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class PathfindingJobScheduler
{
    PathfindingManager _pathfindingManager;

    DynamicArray<JobHandle> _costEditHandles;
    DynamicArray<NativeList<int>> _editedSectorIndicies = new DynamicArray<NativeList<int>>(10);
    DynamicArray<FlowFieldJobPack> _awitingPathFindingJobs;
    DynamicArray<Path> _scheduledPaths;
    NativeList<JobHandle> _pathFindingJobHandles;

    public PathfindingJobScheduler(PathfindingManager pathfindingManager)
    {
        _scheduledPaths = new DynamicArray<Path>(10);
        _pathfindingManager = pathfindingManager;
        _costEditHandles = new DynamicArray<JobHandle>(10);
        _awitingPathFindingJobs = new DynamicArray<FlowFieldJobPack>(10);
        _pathFindingJobHandles = new NativeList<JobHandle>(10, Allocator.Persistent);
    }
    public void Update()
    {
        if (CostEditJobsCompleted())
        {
            ScheduleAllAwaitingPathFinding();
            for(int i = 0; i < _editedSectorIndicies.Count; i++)
            {
                _costEditHandles[i].Complete();
                _pathfindingManager.SignalEditedSectors(_editedSectorIndicies.At(i));
            }
            _editedSectorIndicies.Clear();
            _costEditHandles.Clear();
        }
        for(int i = _pathFindingJobHandles.Length - 1; i >= 0; i--)
        {
            if (_pathFindingJobHandles[i].IsCompleted)
            {
                _pathFindingJobHandles[i].Complete();
                _scheduledPaths[i].IsCalculated = true;
                _scheduledPaths.RemoveAt(i);
                _pathFindingJobHandles.RemoveAt(i);
            }
        }
    }
    public void AddCostEditJob(CostFieldEditJob[] editJobs)
    {
        _editedSectorIndicies.Add(editJobs[0].EditedSectorIndicies);

        if (CostEditScheduled())
        {
            JobHandle lastHandle = _costEditHandles.Last();
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule(lastHandle);

            }
            _costEditHandles.Add(JobHandle.CombineDependencies(combinedHandles));
        }
        else if (PathFindingScheduled())
        {
            JobHandle combinedPathFindingJobHandles = JobHandle.CombineDependencies(_pathFindingJobHandles);
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule(combinedPathFindingJobHandles);
            }
            _costEditHandles.Add(JobHandle.CombineDependencies(combinedHandles));
        }
        else
        {
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule();
            }
            _costEditHandles.Add(JobHandle.CombineDependencies(combinedHandles));
        }

    }
    public void AddPathRequestJob(FlowFieldJobPack jobPack)
    {
        if (CostEditScheduled())
        {
            _awitingPathFindingJobs.Add(jobPack);
        }
        JobHandle pathFindingJobHandle = jobPack.SchedulePack();
        _pathFindingJobHandles.Add(pathFindingJobHandle);
        _scheduledPaths.Add(jobPack.Path);
    }
    public bool CostEditScheduled() => !_costEditHandles.Last().IsCompleted;
    public bool PathFindingScheduled() => _pathFindingJobHandles.Length != 0;
    public bool CostEditJobsCompleted() => !_costEditHandles.IsEmpty() && _costEditHandles.Last().IsCompleted;

    void ScheduleAllAwaitingPathFinding()
    {
        for (int i = 0; i < _awitingPathFindingJobs.Count; i++)
        {
            _pathFindingJobHandles.Add(_awitingPathFindingJobs.At(i).SchedulePack());
            _scheduledPaths.Add(_awitingPathFindingJobs.At(i).Path);
        }
        _awitingPathFindingJobs.Clear();
    }
    bool ScheduledPathJobsCompleted()
    {
        for (int i = 0; i < _pathFindingJobHandles.Length; i++)
        {
            if (!_pathFindingJobHandles[i].IsCompleted)
            {
                return false;
            }
        }
        return true;
    }
    public enum PathfindingJobSchedulerState : byte
    {
        Idle,
        CostEditScheduled,
        PathFindingScheduled
    }
}
