using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

public sealed class VizStack<T> : VizStructure
{
    private readonly List<T> items = [];

    internal VizStack(string label, int line) : base(label, StructureKind.Stack, 0, null, line) { }

    public int Count => items.Count;

    public bool IsEmpty => items.Count == 0;

    public void Push(T value, [CallerLineNumber] int line = 0)
    {
        CheckSize(items.Count + 1);
        items.Add(value);
        Record(OpKind.Push, line, a: items.Count - 1, value: Viz.Format(value));
    }

    public T Pop([CallerLineNumber] int line = 0)
    {
        var value = Top();
        items.RemoveAt(items.Count - 1);
        Record(OpKind.Pop, line, a: items.Count, value: Viz.Format(value));
        return value;
    }

    public T Peek([CallerLineNumber] int line = 0)
    {
        var value = Top();
        Record(OpKind.Read, line, a: items.Count - 1, value: Viz.Format(value));
        return value;
    }

    private T Top() => items.Count > 0 ? items[^1] : throw new InvalidOperationException($"Stack '{Label}' is empty.");
}

public sealed class VizQueue<T> : VizStructure
{
    private readonly List<T> items = [];

    internal VizQueue(string label, int line) : base(label, StructureKind.Queue, 0, null, line) { }

    public int Count => items.Count;

    public bool IsEmpty => items.Count == 0;

    public void Enqueue(T value, [CallerLineNumber] int line = 0)
    {
        CheckSize(items.Count + 1);
        items.Add(value);
        Record(OpKind.Enqueue, line, a: items.Count - 1, value: Viz.Format(value));
    }

    public T Dequeue([CallerLineNumber] int line = 0)
    {
        var value = Front();
        items.RemoveAt(0);
        Record(OpKind.Dequeue, line, a: 0, value: Viz.Format(value));
        return value;
    }

    public T Peek([CallerLineNumber] int line = 0)
    {
        var value = Front();
        Record(OpKind.Read, line, a: 0, value: Viz.Format(value));
        return value;
    }

    private T Front() => items.Count > 0 ? items[0] : throw new InvalidOperationException($"Queue '{Label}' is empty.");
}

public sealed class VizDeque<T> : VizStructure
{
    private readonly List<T> items = [];

    internal VizDeque(string label, int line) : base(label, StructureKind.Deque, 0, null, line) { }

    public int Count => items.Count;

    public bool IsEmpty => items.Count == 0;

    public void PushFront(T value, [CallerLineNumber] int line = 0) => PushAt(0, value, line);

    public void PushBack(T value, [CallerLineNumber] int line = 0) => PushAt(items.Count, value, line);

    public T PopFront([CallerLineNumber] int line = 0) => PopAt(0, line);

    public T PopBack([CallerLineNumber] int line = 0) => PopAt(items.Count - 1, line);

    public T PeekFront([CallerLineNumber] int line = 0) => PeekAt(0, line);

    public T PeekBack([CallerLineNumber] int line = 0) => PeekAt(items.Count - 1, line);

    private void PushAt(int i, T value, int line)
    {
        CheckSize(items.Count + 1);
        items.Insert(i, value);
        Record(OpKind.Push, line, a: i, value: Viz.Format(value));
    }

    private T PopAt(int i, int line)
    {
        CheckNotEmpty();
        var value = items[i];
        items.RemoveAt(i);
        Record(OpKind.Pop, line, a: i, value: Viz.Format(value));
        return value;
    }

    private T PeekAt(int i, int line)
    {
        CheckNotEmpty();
        var value = items[i];
        Record(OpKind.Read, line, a: i, value: Viz.Format(value));
        return value;
    }

    private void CheckNotEmpty()
    {
        if (items.Count == 0) throw new InvalidOperationException($"Deque '{Label}' is empty.");
    }
}
