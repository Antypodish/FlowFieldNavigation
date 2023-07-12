using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static UnityEngine.GraphicsBuffer;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    List<CostFieldEditJob[]> costEditRequests = new List<CostFieldEditJob[]>();
    NativeList<JobHandle> scheduledJobs;
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer, _pathfindingManager);
        scheduledJobs = new NativeList<JobHandle>(Allocator.Persistent);
    }

    public void RoutineUpdate(float deltaTime)
    {
        //COMPLETE ALL SCHEDULED JOBS
        JobHandle.CompleteAll(scheduledJobs);
        _dirCalculator.SendDirections();
        scheduledJobs.Clear();
        
        //SCHEDULE COST EDITS
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < costEditRequests.Count; i++)
        {
            if (scheduledJobs.Length == 0)
            {
                for (int j = 0; j < costEditRequests[i].Length; j++)
                {
                    editHandles.Add(costEditRequests[i][j].Schedule());
                }
            }
            else
            {
                for (int j = 0; j < costEditRequests[i].Length; j++)
                {
                    editHandles.Add(costEditRequests[i][j].Schedule(scheduledJobs[scheduledJobs.Length - 1]));
                }
            }
            JobHandle combinedHandle = JobHandle.CombineDependencies(editHandles);
            scheduledJobs.Add(combinedHandle);
            editHandles.Clear();
        }
        costEditRequests.Clear();

        //SCHEDULE MOVEMENT DATA CALCULATION
        AgentMovementDataCalculationJob movDataJob = _dirCalculator.CalculateDirections(out TransformAccessArray transformsToSchedule);
        if (scheduledJobs.IsEmpty) { scheduledJobs.Add(movDataJob.Schedule(transformsToSchedule)); }
        else { scheduledJobs.Add(movDataJob.Schedule(transformsToSchedule, scheduledJobs[scheduledJobs.Length - 1])); }
    }
    public void RequestCostEdit(int2 startingPoint, int2 endPoint, byte newCost)
    {
        Index2 b1 = new Index2(startingPoint.y, startingPoint.x);
        Index2 b2 = new Index2(endPoint.y, endPoint.x);
        CostFieldEditJob[] costEditJobs = _pathfindingManager.CostFieldProducer.GetEditJobs(new BoundaryData(b1, b2), newCost);
        costEditRequests.Add(costEditJobs);
    }
    public Path RequestPath(NativeArray<Vector3> sources, Vector2 destination, int offset)
    {
        return _pathfindingManager.PathProducer.ProducePath(sources, destination, offset);
    }
}
