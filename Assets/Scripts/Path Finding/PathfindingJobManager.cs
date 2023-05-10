using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class PathfindingJobManager
{
    Queue<CostFieldEditJob[]> CostEditJobs = new Queue<CostFieldEditJob[]>(10);
    Queue<JobHandle> CostEditHandles = new Queue<JobHandle>(10);

    public PathfindingJobManager()
    {

    }
    public void Update()
    {
        //hanlde queue logic
        int CostEditJobsCount = CostEditJobs.Count;
        for (int i = 0; i < CostEditJobsCount; i++)
        {
            CostFieldEditJob[] costEditJob = CostEditJobs.Dequeue();
            if (CostEditHandles.Count == 0)
            {
                NativeArray<JobHandle> handle = new NativeArray<JobHandle>(costEditJob.Length, Allocator.Temp);
                for(int j = 0; j < costEditJob.Length; j++)
                {
                    handle[j] = costEditJob[j].Schedule();
                }
                //CostEditHandles.Enqueue(JobHandle.CombineDependencies(handle));
                JobHandle.CombineDependencies(handle).Complete();
                NativeArray<WindowPair> windows = costEditJob[0].debugArray;
                NativeArray<int> count = costEditJob[0].windowCount;
                Debug.Log("count: " + count[0]);
                for(int index = 0; index < count[0]; index++)
                {
                    Debug.Log("botleft: " + windows[index].Data.Window.BottomLeftBoundary + "\ntopright: " + windows[index].Data.Window.TopRightBoundary);
                }
            }
            else
            {
                JobHandle lastHandle = CostEditHandles.Rear();
                NativeArray<JobHandle> handle = new NativeArray<JobHandle>(costEditJob.Length, Allocator.Temp);
                for (int j = 0; j < costEditJob.Length; j++)
                {
                    handle[j] = costEditJob[j].Schedule(lastHandle);

                }
                CostEditHandles.Enqueue(JobHandle.CombineDependencies(handle));
            }
        }
    }
    public void AddCostEditJob(CostFieldEditJob[] editJobs)
    {
        CostEditJobs.Enqueue(editJobs);
    }

}
