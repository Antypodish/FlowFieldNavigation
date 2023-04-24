using System;
using System.Text;
using UnityEngine;

public class Array<T>
{
    public int Count { get; private set; }

    T[] _array;
    int _endIndex;
    readonly int Size;

    public Array(int size)
    {
        _array = new T[size];
        _endIndex = -1;
        Size = size;
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
        if(Count == 0)
        {
            return true;
        }
        return false;
    }
    public void Add(T element)
    {
        if (Count == Size)
        {
            return;
        }
        _endIndex++;
        _array[_endIndex] = element;
        Count++;
    }
    public void Add(T[] elements)
    {
        for (int i = 0; i < elements.Length; i++)
        {
            if (Count == Size)
            {
                return;
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
            return;
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
    public bool Exist(T data)
    {
        for(int i=0; i<Count; i++)
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
        while(index < Count)
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
        for(int i = index + 1; i < Count; i++)
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
    public void CopyTo(Array<T> destination)
    {
        destination.RemoveAll();
        for(int i =0; i < Count; i++)
        {
            destination.Add(_array[i]);
        }
    }
    public void CopyFrom(T[] array)
    {
        _endIndex = -1;
        while (_endIndex < _array.Length - 1)
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
}
