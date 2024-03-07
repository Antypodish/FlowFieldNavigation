using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

namespace FlowFieldNavigation
{
    internal struct SectorBitArray
    {
        internal UnsafeList<int4> _bits;
        const int arrayVectorSize = 128;
        const int arrayComponentSize = 32;

        internal int BitLength
        {
            get { return _bits.Length * arrayVectorSize; }
        }
        internal int ArrayLength
        {
            get { return _bits.Length; }
        }
        internal SectorBitArray(int totalSectorCount, Allocator allocator, NativeArrayOptions option = NativeArrayOptions.ClearMemory)
        {
            int length = totalSectorCount / arrayVectorSize;
            length = math.select(length + 1, length, totalSectorCount % arrayVectorSize == 0);
            _bits = new UnsafeList<int4>(length, allocator, option);
            _bits.Length = length;
        }

        internal void SetSector(int sectorIndex)
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
        internal bool HasBit(int sectorIndex)
        {
            int vectorIndex = sectorIndex / arrayVectorSize;
            int vectorOverflow = sectorIndex % arrayVectorSize;
            int componentIndex = vectorOverflow / arrayComponentSize;
            int componentOverflow = vectorOverflow % arrayComponentSize;

            int mask = 1 << componentOverflow;
            int4 bits = _bits[vectorIndex];

            return (bits[componentIndex] & mask) == mask;
        }
        internal bool DoesMatchWith(SectorBitArray examinedPathArray)
        {
            if (examinedPathArray.BitLength != BitLength) { return false; }
            UnsafeList<int4> innerExaminedArray = examinedPathArray._bits;

            for (int i = 0; i < _bits.Length; i++)
            {
                int4 lh = _bits[i];
                int4 rh = innerExaminedArray[i];
                bool match = !(lh & rh).Equals(0);
                if (match && !lh.Equals(0)) { return true; }
            }
            return false;
        }
        internal void Dispose()
        {
            _bits.Dispose();
        }
        internal void Clear()
        {
            for (int i = 0; i < _bits.Length; i++)
            {
                _bits[i] = 0;
            }
        }
        internal JobHandle GetCleaningHandle()
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
            internal UnsafeList<int4> InnerList;
            public void Execute()
            {
                for (int i = 0; i < InnerList.Length; i++)
                {
                    InnerList[i] = 0;
                }
            }
        }
    }


}