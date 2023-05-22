using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class PathfindingJobScheduler
{
    Queue<JobHandle> _costEditHandles;
    DynamicArray<FlowFieldJobPack> _awitingPathFindingJobs;
    NativeList<JobHandle> _pathFindingJobHandles;
    public PathfindingJobScheduler()
    {
        _costEditHandles = new Queue<JobHandle>(10);
        _awitingPathFindingJobs = new DynamicArray<FlowFieldJobPack>(10);
        _pathFindingJobHandles = new NativeList<JobHandle>(10, Allocator.Persistent);
    }
    public void Update()
    {
        CheckHandleCollections();
        if (AllCostEditsCompleted())
        {
            ScheduleAllAwaitingPathFinding();
        }
    }
    public void LateUpdate()
    {
        if (AllCostEditsCompleted())
        {
            ScheduleAllAwaitingPathFinding();
        }
    }
    public void AddCostEditJob(CostFieldEditJob[] editJobs)
    {
        if (CostEditScheduled())
        {
            JobHandle lastHandle = _costEditHandles.Rear();
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule(lastHandle);

            }
            _costEditHandles.Enqueue(JobHandle.CombineDependencies(combinedHandles));
        }
        else if (PathFindingScheduled())
        {
            JobHandle combinedPathFindingJobHandles = JobHandle.CombineDependencies(_pathFindingJobHandles);
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule(combinedPathFindingJobHandles);
            }
            _costEditHandles.Enqueue(JobHandle.CombineDependencies(combinedHandles));
        }
        else
        {
            NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(editJobs.Length, Allocator.Temp);
            for (int j = 0; j < editJobs.Length; j++)
            {
                combinedHandles[j] = editJobs[j].Schedule();
            }
            _costEditHandles.Enqueue(JobHandle.CombineDependencies(combinedHandles));
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
    }
    public bool CostEditScheduled() => !_costEditHandles.Rear().IsCompleted;
    public bool PathFindingScheduled() => _pathFindingJobHandles.Length != 0;
    public bool AllCostEditsCompleted() => _costEditHandles.Rear().IsCompleted;

    void ScheduleAllAwaitingPathFinding()
    {
        for (int i = 0; i < _awitingPathFindingJobs.Count; i++)
        {
            _pathFindingJobHandles.Add(_awitingPathFindingJobs.At(i).SchedulePack());
        }
        _awitingPathFindingJobs.RemoveAll();
    }
    void CheckHandleCollections()
    {
        if (_costEditHandles.Rear().IsCompleted)
        {
            _costEditHandles.Clear();
        }

        bool isAllComplete = true;
        for(int i = 0; i < _pathFindingJobHandles.Length; i++)
        {
            if (!_pathFindingJobHandles[i].IsCompleted)
            {
                isAllComplete = false;
                break;
            }
        }
        if (isAllComplete)
        {
            _pathFindingJobHandles.Clear();
        }
    }
    public enum PathfindingJobSchedulerState : byte
    {
        Idle,
        CostEditScheduled,
        PathFindingScheduled
    }
}
