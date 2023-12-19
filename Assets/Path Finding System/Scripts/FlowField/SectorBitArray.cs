using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
public struct SectorBitArray
{
    public UnsafeList<int4> _bits;
    const int arrayVectorSize = 128;
    const int arrayComponentSize = 32;

    public int BitLength
    {
        get { return _bits.Length * arrayVectorSize; }
    }
    public int ArrayLength
    {
        get { return _bits.Length; }
    }
    public SectorBitArray(int totalSectorCount, Allocator allocator, NativeArrayOptions option = NativeArrayOptions.ClearMemory)
    {
        int length = totalSectorCount / arrayVectorSize;
        length = math.select(length + 1, length, totalSectorCount % arrayVectorSize == 0);
        _bits = new UnsafeList<int4>(length, allocator, option);
        _bits.Length = length;
    }

    public void SetSector(int sectorIndex)
    {
        int vectorIndex = sectorIndex / arrayVectorSize;
        int vectorOverflow = sectorIndex % arrayVectorSize;
        int componentIndex = vectorOverflow / arrayComponentSize;
        int componentOverflow = vectorOverflow % arrayComponentSize;

        int mask = 1 << componentOverflow;
        int4 bits = _bits[vectorIndex];
        bits[componentIndex] |= mask;
        _bits[vectorIndex] = bits;
    }
    public bool HasBit(int sectorIndex)
    {
        int vectorIndex = sectorIndex / arrayVectorSize;
        int vectorOverflow = sectorIndex % arrayVectorSize;
        int componentIndex = vectorOverflow / arrayComponentSize;
        int componentOverflow = vectorOverflow % arrayComponentSize;

        int mask = 1 << componentOverflow;
        int4 bits = _bits[vectorIndex];

        return (bits[componentIndex] & mask) == mask;
    }
    public bool DoesMatchWith(SectorBitArray examinedArray)
    {
        if (examinedArray.BitLength != BitLength) { return false; }
        UnsafeList<int4> innerExaminedArray = examinedArray._bits;

        for(int i = 0; i < _bits.Length; i++)
        {
            int4 lh = _bits[i];
            int4 rh = innerExaminedArray[i];
            bool match = (lh & rh).Equals(rh);
            if (match) { return true; }
        }
        return false;
    }
    public void Dispose()
    {
        _bits.Dispose();
    }
    public void Clear()
    {
        for (int i = 0; i < _bits.Length; i++)
        {
            _bits[i] = 0;
        }
    }
    public JobHandle GetCleaningHandle()
    {
        SectorBitArrayCleanJob cleanJob = new SectorBitArrayCleanJob()
        {
            InnerList = _bits,
        };
        return cleanJob.Schedule();
    }


    [BurstCompile]
    struct SectorBitArrayCleanJob : IJob
    {
        public UnsafeList<int4> InnerList;
        public void Execute()
        {
            for(int i = 0; i < InnerList.Length; i++)
            {
                InnerList[i] = 0;
            }
        }
    }
}
