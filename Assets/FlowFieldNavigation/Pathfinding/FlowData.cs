using Unity.Burst;
using Unity.Mathematics;


namespace FlowFieldNavigation
{
    internal struct FlowData
    {
        byte _flow;

        internal float2 GetFlow(float tileSize)
        {
            int verticalMag = (_flow >> 4) & 0b0000_0111;
            int horizontalMag = _flow & 0b0000_0111;

            bool isVerticalNegative = (_flow & 0b1000_0000) == 0b1000_0000;
            bool isHorizontalNegative = (_flow & 0b0000_1000) == 0b0000_1000;

            verticalMag = math.select(verticalMag, -(verticalMag + 1), isVerticalNegative);
            horizontalMag = math.select(horizontalMag, -(horizontalMag + 1), isHorizontalNegative);

            return math.normalizesafe(new float2(horizontalMag * tileSize, verticalMag * tileSize));
        }
        internal void SetFlow(int curGeneralIndex, int targetGeneralIndex, int fieldColAmount)
        {
            int verticalDif = (targetGeneralIndex / fieldColAmount - curGeneralIndex / fieldColAmount);//-1
            int horizontalDif = targetGeneralIndex - (curGeneralIndex + verticalDif * fieldColAmount);//+1

            if (verticalDif > 7 || verticalDif < -7 || horizontalDif > 7 || horizontalDif < -7) { return; }
            bool isVerticalNegative = verticalDif < 0;
            bool isHorizontalNegative = horizontalDif < 0;

            byte verticalBits = (byte)math.select(verticalDif << 4, ((math.abs(verticalDif) - 1) << 4) | 0b1000_0000, isVerticalNegative);
            byte horizontalBits = (byte)math.select(horizontalDif, (math.abs(horizontalDif) - 1) | 0b0000_1000, isHorizontalNegative);
            _flow = (byte)(0 | verticalBits | horizontalBits);
        }
        internal bool IsValid()
        {
            return _flow != 0b0000_00000;
        }
    }

}