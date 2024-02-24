using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataIndexManager
    {
        const short _maxBucketSize = 512;
        const short _maxBucketCount = short.MaxValue;
    }
}
