using Unity.Mathematics;

namespace FlowFieldNavigation
{
    public struct AgentDataIndex
    {
        int _referance;

        internal AgentDataIndex(int index, AgentIndexType referanceType)
        {
            index++;
            index = math.select(index | -0b_10000000_00000000_00000000_00000000, index, referanceType == AgentIndexType.DataIndex);
            _referance = index;
        }
        public AgentIndexType GetIndexType()
        {
            return _referance < 0 ? AgentIndexType.AccumulationIndex : AgentIndexType.DataIndex;
        }
        public bool TryGetIndex(out int index)
        {
            bool succesfull = _referance != 0;
            index = (_referance << 1 >> 1) - 1;
            return succesfull;
        }
        public bool IsValid()
        {
            return _referance != 0;
        }
    }
    public enum AgentIndexType : byte
    {
        DataIndex = 0,
        AccumulationIndex = 1,
    }
}
