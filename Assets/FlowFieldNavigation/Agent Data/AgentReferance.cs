using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct AgentReferance
    {
        int _referance;

        internal AgentReferance(int index, AgentReferanceType referanceType)
        {
            index = math.select(index | -0b_10000000_00000000_00000000_00000000, index, referanceType == AgentReferanceType.NormalReferance);
            _referance = index;
        }
        internal int GetIndex()
        {
            return _referance << 1 >> 1;
        }
        internal AgentReferanceType GetReferanceType()
        {
            return _referance < 0 ? AgentReferanceType.AccumulationReferance : AgentReferanceType.NormalReferance;
        }
    }
    internal enum AgentReferanceType : byte
    {
        NormalReferance = 0,
        AccumulationReferance = 1,
    }
}
