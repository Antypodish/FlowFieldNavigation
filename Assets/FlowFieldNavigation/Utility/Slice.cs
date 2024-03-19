namespace FlowFieldNavigation
{
    internal struct Slice
    {
        internal int Index { get; }
        internal int Count { get; }

        internal Slice(int index, int count) { Index = index; Count = count; }
    }
}