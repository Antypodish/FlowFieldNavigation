using System;

namespace FlowFieldNavigation
{
    [Flags]
    internal enum IntegrationMark : byte
    {
        None = 0,
        LOSPass = 1,
        LOSBlock = 2,
        LOSC = 4,
        Awaiting = 8,
        Integrated = 16,
    }

}