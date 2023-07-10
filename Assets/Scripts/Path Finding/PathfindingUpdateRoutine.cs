using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    List<CostFieldEditJob[]> costEditRequests = new List<CostFieldEditJob[]>();
    List<JobHandle> scheduledJobs = new List<JobHandle>();
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer, _pathfindingManager);
    }

    public void RoutineUpdate(float deltaTime)
    {
        //COMPLETE ALL SCHEDULED JOBS
        for(int i = 0; i < scheduledJobs.Count; i++)
        {
            scheduledJobs[i].Complete();
        }
        scheduledJobs.Clear();

        //SCHEDULE COST EDITS
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < costEditRequests.Count; i++)
        {
            if (scheduledJobs.Count == 0)
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
                    editHandles.Add(costEditRequests[i][j].Schedule(scheduledJobs[scheduledJobs.Count - 1]));
                }
            }
            JobHandle combinedHandle = JobHandle.CombineDependencies(editHandles);
            scheduledJobs.Add(combinedHandle);
            editHandles.Clear();
        }
        costEditRequests.Clear();
        //_dirCalculator.CalculateDirections();
    }
    public void RequestCostEdit(int2 startingPoint, int2 endPoint, byte newCost)
    {
        Index2 b1 = new Index2(startingPoint.y, startingPoint.x);
        Index2 b2 = new Index2(endPoint.y, endPoint.x);
        CostFieldEditJob[] costEditJobs = _pathfindingManager.CostFieldProducer.GetEditJobs(new BoundaryData(b1, b2), newCost);
        costEditRequests.Add(costEditJobs);
    }
}
