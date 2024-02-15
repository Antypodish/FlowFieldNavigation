using Unity.Mathematics;


namespace FlowFieldNavigation
{
    public struct ObstacleRequest
    {
        public float2 Position;
        public float2 HalfSize;

        public ObstacleRequest(float2 pos, float2 halfSize) { Position = pos; HalfSize = halfSize; }
    }


}