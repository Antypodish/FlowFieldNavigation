public class Node<T>
{
    public T data;
    public Node<T> next;

    public Node(T data)
    {
        this.data = data;
    }
}
public class LinkedListIterator<T>
{
    Node<T> _node;
    
    public LinkedListIterator(Node<T> head)
    {
        _node = head;
    }
    public T Next()
    {
        T data = _node.data;
        _node = _node.next;
        return data;
    }
}
public class LinkedList<T>
{
    public int Count { get; private set; }

    Node<T> _head;
    Node<T> _tail;
    public bool IsEmpty()
    {
        if (_head == null)
        {
            return true;
        }
        return false;
    }
    public void AddToTail(T data)
    {
        Count++;
        if (_head == null)
        {
            _head = new Node<T>(data);
            _tail = _head;
            return;
        }
        _tail.next = new Node<T>(data);
        _tail = _tail.next;
    }
    public void AddToHead(T data)
    {
        Node<T> newHead = new Node<T>(data);
        newHead.next = _head;
        _head = newHead;
    }
    public T GetHead()
    {
        return _head.data;
    }
    public T GetTail()
    {
        return _tail.data;
    }
    public LinkedListIterator<T> GetIterator()
    {
        return new LinkedListIterator<T>(_head);
    }
    public void RemoveHead()
    {
        _head=_head.next;
    }
    public void Remove(T data)
    {
        if (IsEmpty())
        {
            return;
        }

        if (data.Equals(_head.data))
        {
            _head = _head.next;
            Count--;
            return;
        }
        Node<T> temp = _head;
        while (temp.next != null)
        {
            if (temp.next.data.Equals(data))
            {
                temp.next = temp.next.next;
                Count--;
                return;
            }
            temp = temp.next;
        }
    }
    public void RemoveAll()
    {
        _head = null;
        _tail = null;
        Count = 0;
    }
    public T PushHeadToTail()
    {
        T toReturn = _head.data;
        _tail.next = _head;
        _head = _head.next;
        return toReturn;
    }
    public T[] ToArray()
    {
        T[] array = new T[Count];
        Node<T> temp = _head;
        int i = 0;
        while (temp != null)
        {
            array[i]=temp.data;
            i++;
            temp = temp.next;
        }
        return array;
    }
}
