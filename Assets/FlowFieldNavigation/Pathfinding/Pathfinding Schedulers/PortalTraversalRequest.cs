namespace FlowFieldNavigation
{
	internal struct PortalTraversalRequest
	{
		internal int PathIndex;
		internal Slice SourceSlice;

		internal PortalTraversalRequest(int pathIndex, Slice sourceSlice)
		{
			PathIndex = pathIndex;
			SourceSlice = sourceSlice;
		}
	}
}