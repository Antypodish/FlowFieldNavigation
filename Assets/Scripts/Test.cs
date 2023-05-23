using Unity.Jobs;
using UnityEngine;

public class Test : MonoBehaviour
{
    JobHandle jh;
    private void Start()
    {
        myjob job = new myjob();
        jh = job.Schedule();
    }
    private void Update()
    {
        Debug.Log(jh.IsCompleted);
    }
}

struct myjob : IJob
{
    public void Execute()
    {
        for (int i = 0; i < 100000; i++)
        {
            for(int j = 0; j < 1000000; j++)
            {
                int k = j;
            }
        }
    }
}