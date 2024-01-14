using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

public struct UnsafeListReadOnly<T> where T : unmanaged
{
    UnsafeList<T> _listData;
    public int Length
    {
        get { return _listData.Length; }
        set { _listData.Length = value; }
    }

    public UnsafeListReadOnly(int initialCapacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
    {
        _listData = new UnsafeList<T>(initialCapacity, allocator, options);
    }
    public unsafe UnsafeListReadOnly(T* ptr, int length)
    {
        _listData = new UnsafeList<T>(ptr, length);
    }

    public T this[int index]
    {
        get
        {
            return _listData[index];
        }
    }
    public void Dispose()
    {
        _listData.Dispose();
    }
}
