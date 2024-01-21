using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

internal struct UnsafeListReadOnly<T> where T : unmanaged
{
    UnsafeList<T> _listData;
    internal int Length
    {
        get { return _listData.Length; }
        set { _listData.Length = value; }
    }

    internal UnsafeListReadOnly(int initialCapacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
    {
        _listData = new UnsafeList<T>(initialCapacity, allocator, options);
    }
    internal unsafe UnsafeListReadOnly(T* ptr, int length)
    {
        _listData = new UnsafeList<T>(ptr, length);
    }

    internal T this[int index]
    {
        get
        {
            return _listData[index];
        }
    }
    internal void Dispose()
    {
        _listData.Dispose();
    }
}
