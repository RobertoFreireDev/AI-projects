using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

public sealed class VizLinkedList<T> : VizStructure
{
    private readonly List<LinkedNode<T>> nodes = [];

    internal VizLinkedList(string label, int line) : base(label, StructureKind.LinkedList, 0, null, line) { }

    public LinkedNode<T>? Head { get; private set; }

    public LinkedNode<T>? Tail => Walk().LastOrDefault();

    /// <summary>Number of nodes reachable from <see cref="Head"/>.</summary>
    public int Count => Walk().Count();

    public bool IsEmpty => Head is null;

    public LinkedNode<T> AddFirst(T value, [CallerLineNumber] int line = 0)
    {
        var node = NewNode(value);
        node.Next = Head;
        Head = node;
        Record(OpKind.Insert, line, node.Id, value: Viz.Format(value));
        return node;
    }

    public LinkedNode<T> AddLast(T value, [CallerLineNumber] int line = 0)
    {
        var tail = Tail;
        if (tail is null) return AddFirst(value, line);
        var node = NewNode(value);
        tail.Next = node;
        Record(OpKind.Insert, line, node.Id, a: tail.Id, value: Viz.Format(value));
        return node;
    }

    public LinkedNode<T> InsertAfter(LinkedNode<T> node, T value, [CallerLineNumber] int line = 0)
    {
        Own(node);
        var added = NewNode(value);
        added.Next = node.Next;
        node.Next = added;
        Record(OpKind.Insert, line, added.Id, a: node.Id, value: Viz.Format(value));
        return added;
    }

    /// <summary>Removes <paramref name="node"/>; every node pointing to it now points to its next.</summary>
    public void Remove(LinkedNode<T> node, [CallerLineNumber] int line = 0)
    {
        Own(node);
        foreach (var n in nodes)
            if (n.Next == node) n.Next = node.Next;
        if (Head == node) Head = node.Next;
        nodes.Remove(node);
        node.Next = null;
        node.Removed = true;
        Record(OpKind.Remove, line, node.Id);
    }

    /// <summary>Points <paramref name="node"/>.Next at <paramref name="next"/> (or nothing), e.g. to reverse a list.</summary>
    public void SetNext(LinkedNode<T> node, LinkedNode<T>? next, [CallerLineNumber] int line = 0)
    {
        Own(node);
        if (next is not null) Own(next);
        node.Next = next;
        Record(OpKind.Link, line, node.Id, a: next?.Id);
    }

    public void SetHead(LinkedNode<T>? node, [CallerLineNumber] int line = 0)
    {
        if (node is not null) Own(node);
        Head = node;
        Record(OpKind.Link, line, a: node?.Id);
    }

    private LinkedNode<T> NewNode(T value)
    {
        CheckSize(nodes.Count + 1);
        var node = new LinkedNode<T>(this, NewNodeId(), value);
        nodes.Add(node);
        return node;
    }

    internal void Own(LinkedNode<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Owner != this || node.Removed)
            throw new InvalidOperationException($"That node is not in '{Label}' (removed or from another list).");
    }

    private IEnumerable<LinkedNode<T>> Walk()
    {
        var steps = 0;
        for (var n = Head; n is not null; n = n.Next)
        {
            if (++steps > nodes.Count) throw new InvalidOperationException($"'{Label}' has a cycle.");
            yield return n;
        }
    }
}

public sealed class LinkedNode<T>
{
    internal LinkedNode(VizLinkedList<T> owner, int id, T value)
    {
        Owner = owner;
        Id = id;
        Value = value;
    }

    public T Value { get; private set; }

    public LinkedNode<T>? Next { get; internal set; }

    internal VizLinkedList<T> Owner { get; }

    internal int Id { get; }

    internal bool Removed { get; set; }

    public void Visit([CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Owner.Record(OpKind.Visit, line, Id);
    }

    public void SetValue(T value, [CallerLineNumber] int line = 0)
    {
        Owner.Own(this);
        Value = value;
        Owner.Record(OpKind.Write, line, Id, value: Viz.Format(value));
    }

    public override string ToString() => Viz.Format(Value);
}
