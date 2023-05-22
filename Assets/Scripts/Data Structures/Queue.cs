using System.Text;

public class Queue<T>
{
    public int Count;

    int _size;
    T[] _elements;
    int _front;
    int _rear;

    public Queue(int size)
    {
        _elements = new T[size + 2];
        _size = size + 2;
        Count = 0;
        _front = 0;
        _rear = 1;
    }
    public bool IsFull()
    {
        if(Count == _size - 2)
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
    public T Dequeue()
    {
        if(Count == 0) { return _elements[_front]; }

        _front = (_front + 1) % _size;
        Count--;
        return _elements[_front];
    }
    public void Enqueue(T element)
    {
        if(Count == _size - 2) { Resize(); }

        _elements[_rear] = element;
        _rear = (_rear + 1) % _size;
        Count++;
    }
    public T PushFirstToLast()
    {
        if(Count == 0) { return _elements[_front]; }

        _front = (_front + 1) % _size;
        T toReturn = _elements[_front];
        _elements[_rear] = toReturn;
        _rear = (_rear + 1) % _size;
        return toReturn;
    }
    public T Rear()
    {
        return _elements[(_size + _rear - 1) % _size];
    }
    public T Front()
    {
        return _elements[(_front + 1) % _size];
    }
    public void Clear()
    {
        Count = 0;
        _front = 0;
        _rear = 1;
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        int i = (_front + 1) % _size;

        while(i != _rear)
        {
            sb.Append(" {" + _elements[i].GetHashCode() + "}");
            i = (i + 1) % _size;
        }
        return sb.ToString();
    }
    void Resize()
    {
        int newSize = (_size - 2) * 2 + 2;
        T[] newArray = new T[newSize];

        int front = _front;
        int rear = _rear;

        if (front < rear)
        {
            while (front < rear)
            {
                newArray[front] = _elements[front];
                front++;
            }
        }
        else
        {
            int size = _size;
            for (int i = 0; i < rear; i++)
            {
                newArray[i] = _elements[i];
            }
            int sizeDiff = newSize - size;
            for(int i = front; i < size; i++)
            {
                newArray[i + sizeDiff] = _elements[i];
            }

            _front = front + sizeDiff;
        }

    }
}
