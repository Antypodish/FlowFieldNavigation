using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowFieldNavigation
{
    internal struct PortalTraversalDataRecord
    {
        internal int PortalIndex;
        internal int NextIndex;
        internal float DistanceFromTarget;
        internal PortalTraversalMark Mark;
    }
}
