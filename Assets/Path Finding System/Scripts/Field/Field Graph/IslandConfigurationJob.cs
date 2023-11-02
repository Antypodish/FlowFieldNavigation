using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct IslandConfigurationJob : IJob
{
    [ReadOnly] public NativeArray<PortalToPortal> PortalEdges;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<PortalNode> PortalNodes;
    public NativeList<IslandData> Islands;
    public void Execute()
    {
        UnsafeStack<int> dfsStack = new UnsafeStack<int>(0);
        for(int i = 0; i < WindowNodes.Length; i++)
        {
            int portalStart = WindowNodes[i].PorPtr;
            int portalCount = WindowNodes[i].PorCnt;
            for(int j = portalStart; j < portalStart + portalCount; j++)
            {
                PortalNode firstPortalNode = PortalNodes[j];
                if (firstPortalNode.IsIslandValid()) { continue; }
                int suitableIndex = GetSuitableIslandIndex();
                RunDepthFirstSearch(j, suitableIndex, dfsStack);
            }
        }
    }

    int GetSuitableIslandIndex()
    {
        for(int i = 1; i < Islands.Length; i++)
        {
            if(Islands[i] == IslandData.Removed)
            {
                Islands[i] = IslandData.Uptodate;
                return i;
            }
        }
        Islands.Add(IslandData.Uptodate);
        return Islands.Length - 1;
    }

    void RunDepthFirstSearch(int startNodeIndex, int islandIndex, UnsafeStack<int> stack)
    {

        //HANDLE FIRST
        PortalNode startNode = PortalNodes[startNodeIndex];
        startNode.IslandIndex = islandIndex;
        PortalNodes[startNodeIndex] = startNode;
        stack.Push(startNodeIndex);

        while (!stack.IsEmpty())
        {
            int curPortalIndex = stack.Pop();
            PortalNode curNode = PortalNodes[curPortalIndex];

            //PUSH NEIGHBOURS
            int p1NeighbourStart = curNode.Portal1.PorToPorPtr;
            int p1NeighbourCount = curNode.Portal1.PorToPorCnt;
            int p2NeighbourStart = curNode.Portal2.PorToPorPtr;
            int p2NeighbourCount = curNode.Portal2.PorToPorCnt;

            for(int i = p1NeighbourStart; i < p1NeighbourStart + p1NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid()) { continue; }
                neighbourPortal.IslandIndex = islandIndex;
                PortalNodes[portalEdge.Index] = neighbourPortal;
                stack.Push(portalEdge.Index);
            }
            for (int i = p2NeighbourStart; i < p2NeighbourStart + p2NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid()) { continue; }
                neighbourPortal.IslandIndex = islandIndex;
                PortalNodes[portalEdge.Index] = neighbourPortal;
                stack.Push(portalEdge.Index);
            }
        }
    }







    private struct UnsafeStack<T> where T : unmanaged
    {
        UnsafeList<T> _data;
        public UnsafeStack(int placeHolderDataDoingNothing)
        {
            _data = new UnsafeList<T>(0, Allocator.Temp);
        }
        public void Push(T item)
        {
            _data.Add(item);
        }
        public T Pop()
        {
            if(_data.Length == 0) { return default(T); }
            T item = _data[_data.Length - 1];
            _data.Length = _data.Length - 1;
            return item;
        }
        public bool IsEmpty()
        {
            return _data.IsEmpty;
        }
        public void Clear()
        {
            _data.Clear();
        }
    }
}
