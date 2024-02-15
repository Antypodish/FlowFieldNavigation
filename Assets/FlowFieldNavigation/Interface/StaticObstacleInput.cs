using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldNavigation
{
    public struct StaticObstacleInput
    {
        public StaticObstacle Obstacle;
        public Transform Transform;
    }
    public struct StaticObstacle
    {
        internal float3 LBL;
        internal float3 LTL;
        internal float3 LTR;
        internal float3 LBR;
        internal float3 UBL;
        internal float3 UTL;
        internal float3 UTR;
        internal float3 UBR;
    }

}