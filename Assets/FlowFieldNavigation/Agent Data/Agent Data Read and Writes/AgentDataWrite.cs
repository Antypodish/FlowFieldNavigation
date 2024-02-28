namespace FlowFieldNavigation
{
    internal struct AgentDataWrite
    {
        AgentDataWriteFlags _writeFlags;
        int _agentDataReferanceIndex;
        int _reqPathIndex;

        internal AgentDataWrite(int agentDataRefIndex)
        {
            _agentDataReferanceIndex = agentDataRefIndex;
            _reqPathIndex = 0;
            _writeFlags = AgentDataWriteFlags.AgentDataReferanceIndexWritten;
        }
        internal void SetReqPathIndex(int reqPathIndex)
        {
            _reqPathIndex = reqPathIndex;
            _writeFlags |= AgentDataWriteFlags.ReqPathIndexWritten;
        }
        internal AgentDataWriteOutput GetOutput()
        {
            return new AgentDataWriteOutput()
            {
                WriteFlags = _writeFlags,
                AgentDataReferanceIndex = _agentDataReferanceIndex,
                ReqPathIndex = _reqPathIndex,
            };
        }
    }

    internal struct AgentDataWriteOutput
    {
        internal AgentDataWriteFlags WriteFlags;
        internal int AgentDataReferanceIndex;
        internal int ReqPathIndex;
    }

    internal enum AgentDataWriteFlags : byte
    {
        AgentDataReferanceIndexWritten = 1,
        ReqPathIndexWritten = 2,
    }
}
