namespace FlowFieldNavigation
{
    internal struct AgentIndexReferance
    {
        ushort _bucketIndex;
        ushort _localIndex;
        internal AgentIndexReferance(short bucketIndex, short localIndex)
        {
            _bucketIndex = (ushort)(bucketIndex + 1);
            _localIndex = (ushort)(localIndex + 1);
        }
        internal bool TryGetIndex(out int bucketIndex, out int localIndex)
        {
            bool succesfull = _bucketIndex != 0;
            bucketIndex = _bucketIndex;
            localIndex = _localIndex;
            return succesfull;
        }
    }
}
