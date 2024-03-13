using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct PortalTraversalIndex
    {
        internal static PortalTraversalIndex Invalid { get; }= new PortalTraversalIndex(-1, false);
        bool _isGoal;
        int _index;
        internal PortalTraversalIndex(int index, bool isGoal)
        {
            _index = index;
            _isGoal = isGoal;
        }
        internal int GetIndex(out bool isGoal)
        {
            isGoal = _isGoal;
            return _index;
        }
        internal int GetIndexUnchecked() => _index;
        internal bool IsGoal() => _isGoal;
        internal bool IsValid() => _index >= 0;
    }
}
