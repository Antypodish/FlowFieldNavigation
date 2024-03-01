namespace FlowFieldNavigation
{
    internal struct AgentDataWrite
    {
        AgentDataWriteFlags _writeFlags;
        int _agentDataReferanceIndex;
        int _reqPathIndex;
        AgentStatus _agentStatus;
        bool _navigationMovementFlag;
        float _speed;

        internal AgentDataWrite(int agentDataRefIndex)
        {
            _agentDataReferanceIndex = agentDataRefIndex;
            _reqPathIndex = 0;
            _agentStatus = 0;
            _navigationMovementFlag = false;
            _speed = 0;
            _writeFlags = AgentDataWriteFlags.AgentDataReferanceIndexWritten;
        }
        internal void SetReqPathIndex(int reqPathIndex)
        {
            _reqPathIndex = reqPathIndex;
            _writeFlags |= AgentDataWriteFlags.ReqPathIndexWritten;
        }
        internal void SetAgentHoldGround()
        {
            _agentStatus = AgentStatus.HoldGround;
            _writeFlags |= AgentDataWriteFlags.StatusWritten;
        }
        internal void SetAgentStopped()
        {
            _agentStatus = 0;
            _writeFlags |= AgentDataWriteFlags.StatusWritten;
        }
        internal void SetNavigationMovementFlag(bool flag)
        {
            _navigationMovementFlag = flag;
            _writeFlags |= AgentDataWriteFlags.NavigationMovementFlagWritten;
        }
        internal void SetSpeed(float speed)
        {
            _speed = speed;
            _writeFlags |= AgentDataWriteFlags.SpeedWritten;
        }
        internal bool GetStatusIfWritten(out AgentStatus status)
        {
            status = _agentStatus;
            return (_writeFlags & AgentDataWriteFlags.StatusWritten) == AgentDataWriteFlags.StatusWritten;
        }
        internal bool GetSpeedIfWritten(out float speed)
        {
            speed = _speed;
            return (_writeFlags & AgentDataWriteFlags.SpeedWritten) == AgentDataWriteFlags.SpeedWritten;
        }
        internal bool GetNavMovementFlagIfWritten(out bool navMovementFlag)
        {
            navMovementFlag = _navigationMovementFlag;
            return (_writeFlags & AgentDataWriteFlags.NavigationMovementFlagWritten) == AgentDataWriteFlags.NavigationMovementFlagWritten;
        }
        internal AgentDataWriteOutput GetOutput()
        {
            return new AgentDataWriteOutput()
            {
                WriteFlags = _writeFlags,
                AgentDataReferanceIndex = _agentDataReferanceIndex,
                ReqPathIndex = _reqPathIndex,
                AgentStatus = _agentStatus,
                NavigationMovementFlag = _navigationMovementFlag,
                Speed = _speed,
            };
        }
    }

    internal struct AgentDataWriteOutput
    {
        internal AgentDataWriteFlags WriteFlags;
        internal int AgentDataReferanceIndex;
        internal int ReqPathIndex;
        internal AgentStatus AgentStatus;
        internal bool NavigationMovementFlag;
        internal float Speed;
    }

    internal enum AgentDataWriteFlags : byte
    {
        AgentDataReferanceIndexWritten = 1,
        ReqPathIndexWritten = 2,
        StatusWritten = 4,
        NavigationMovementFlagWritten = 8,
        SpeedWritten = 16,
    }
}
