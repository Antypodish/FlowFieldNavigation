using System.Text;
using System;

public class DynamicArray<T>
{
    public int Count { get; private set; }

    T[] _array;
    int _endIndex;
    int Size;

    public DynamicArray(int initialSize)
    {
        _array = new T[initialSize];
        _endIndex = -1;
        Size = initialSize;
        Count = 0;
    }
    public bool IsFull()
    {
        if (Count == Size)
        {
            return true;
        }
        return false;
    }
    public bool IsEmpty()
    {
        if (Count == 0)
        {
            return true;
        }
        return false;
    }
    public void Add(T element)
    {
        if (Count == Size)
        {
            Resize();
        }
        _endIndex++;
        _array[_endIndex] = element;
        Count++;
    }
    public void Set(int index, T data)
    {
        if (_endIndex == -1) { return; }
        if(index > _endIndex) { return; }
        _array[index] = data;
    }
    public void Add(T[] elements)
    {
        for(int i = 0; i < elements.Length; i++)
        {
            if (Count == Size)
            {
                Resize();
            }
            _endIndex++;
            _array[_endIndex] = elements[i];
            Count++;
        }
    }
    public void AddIfNew(T element)
    {
        if (Count == Size)
        {
            Resize();
        }
        for (int i = 0; i < Count; i++)
        {
            if (_array[i].Equals(element))
            {
                return;
            }
        }
        _endIndex++;
        _array[_endIndex] = element;
        Count++;
    }
    public T PushFirstToLast()
    {
        T toReturn = _array[0];
        int index = 1;
        while(index < Count)
        {
            _array[index - 1] = _array[index];
            index++;
        }
        _array[index - 1] = toReturn;
        return toReturn;
    }
    public bool Exist(T data)
    {
        for (int i = 0; i < Count; i++)
        {
            if (_array[i].Equals(data))
            {
                return true;
            }
        }
        return false;
    }
    public T At(int index)
    {
        if (index > _endIndex)
        {
            throw new IndexOutOfRangeException();
        }
        return _array[index];
    }
    public void Remove(T element)
    {
        int index = 0;
        while (index < Count)
        {
            if (element.Equals(_array[index]))
            {
                break;
            }
            index++;
        }
        if (index == Count)
        {
            return;
        }
        for (int i = index + 1; i < Count; i++)
        {
            _array[i - 1] = _array[i];
        }
        _endIndex--;
        Count--;
    }
    public void RemoveAll()
    {
        _endIndex = -1;
        Count = 0;
    }
    public T[] ToArray()
    {
        T[] array = new T[Count];
        for (int i = 0; i < Count; i++)
        {
            array[i] = _array[i];
        }
        return array;
    }
    public void CopyFrom(T[] array)
    {
        if(array.Length > _array.Length)
        {
            _array = new T[array.Length];
            Size = array.Length;
        }
        _endIndex = -1;
        while(_endIndex < array.Length - 1)
        {
            _endIndex++;
            _array[_endIndex] = array[_endIndex];
        }
        Count = _endIndex + 1;
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Count; i++)
        {
            sb.Append(" {" + _array[i].GetHashCode() + "}");
        }
        return sb.ToString();
    }

    void Resize()
    {
        T[] arr = new T[Size * 2];
        for(int i=0; i < Count; i++)
        {
            arr[i] = _array[i];
        }
        _array = arr;
        Size *= 2;
    }
}
