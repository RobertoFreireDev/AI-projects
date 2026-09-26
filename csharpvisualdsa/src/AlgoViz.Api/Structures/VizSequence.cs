using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

/// <summary>Index-based operations shared by <see cref="VizArray{T}"/> and <see cref="VizList{T}"/>.</summary>
public abstract class VizSequence<T> : VizStructure
{
    private protected readonly List<T> items;

    private protected VizSequence(string label, StructureKind kind, List<T> items, int line)
        : base(label, kind, Checked(items).Count, Pack(items), line)
    {
        this.items = items;
    }

    public int Length => items.Count;

    public int Count => items.Count;

    public bool IsEmpty => items.Count == 0;

    public T Get(int i, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        var value = items[i];
        Record(OpKind.Read, line, a: i, value: Viz.Format(value));
        return value;
    }

    public void Set(int i, T value, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        items[i] = value;
        Record(OpKind.Write, line, a: i, value: Viz.Format(value));
    }

    public void Swap(int i, int j, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        CheckIndex(j);
        (items[i], items[j]) = (items[j], items[i]);
        Record(OpKind.Swap, line, a: i, b: j);
    }

    /// <summary>Compares item <paramref name="i"/> with item <paramref name="j"/>: negative, zero or positive.</summary>
    public int Compare(int i, int j, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        CheckIndex(j);
        var result = Comparer<T>.Default.Compare(items[i], items[j]);
        Record(OpKind.Compare, line, a: i, b: j, value: result < 0 ? "<" : result > 0 ? ">" : "=");
        return result;
    }

    public void Mark(int i, VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        Record(OpKind.Mark, line, a: i, text: color.Name());
    }

    public void Unmark(int i, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        Record(OpKind.Unmark, line, a: i);
    }

    /// <summary>Shows a named arrow under index <paramref name="i"/> (-1 and Length are allowed).</summary>
    public void Pointer(string name, int i, [CallerLineNumber] int line = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (i < -1 || i > items.Count)
            throw new ArgumentOutOfRangeException(nameof(i), $"Pointer index {i} is outside -1..{items.Count}.");
        Record(OpKind.Pointer, line, a: i, text: name);
    }

    public void RemovePointer(string name, [CallerLineNumber] int line = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Record(OpKind.Pointer, line, text: name);
    }

    /// <summary>Also renders the items as a complete binary tree (index i has children 2i+1 and 2i+2), e.g. for heaps.</summary>
    public void ShowAsTree([CallerLineNumber] int line = 0) => Record(OpKind.Mark, line, text: "view:tree");

    /// <summary>A copy of the items. Silent.</summary>
    public T[] ToArray() => [.. items];

    public override string ToString() => $"{Label}[{string.Join(", ", items)}]";

    private protected void CheckIndex(int i)
    {
        if ((uint)i >= (uint)items.Count)
            throw new IndexOutOfRangeException($"Index {i} is out of range for '{Label}' (length {items.Count}).");
    }

    private static List<T> Checked(List<T> items)
    {
        CheckSize(items.Count);
        return items;
    }
}

public sealed class VizArray<T> : VizSequence<T>
{
    internal VizArray(string label, List<T> items, int line) : base(label, StructureKind.Array, items, line) { }
}

public sealed class VizList<T> : VizSequence<T>
{
    internal VizList(string label, List<T> items, int line) : base(label, StructureKind.List, items, line) { }

    public void Add(T value, [CallerLineNumber] int line = 0) => Insert(items.Count, value, line);

    public void Insert(int i, T value, [CallerLineNumber] int line = 0)
    {
        if ((uint)i > (uint)items.Count)
            throw new IndexOutOfRangeException($"Index {i} is out of range for '{Label}' (count {items.Count}).");
        CheckSize(items.Count + 1);
        items.Insert(i, value);
        Record(OpKind.Insert, line, a: i, value: Viz.Format(value));
    }

    public void RemoveAt(int i, [CallerLineNumber] int line = 0)
    {
        CheckIndex(i);
        var value = items[i];
        items.RemoveAt(i);
        Record(OpKind.Remove, line, a: i, value: Viz.Format(value));
    }

    /// <summary>Removes the first occurrence of <paramref name="value"/>; false when it isn't there.</summary>
    public bool Remove(T value, [CallerLineNumber] int line = 0)
    {
        var i = items.IndexOf(value);
        if (i >= 0) items.RemoveAt(i);
        Record(OpKind.Remove, line, a: i >= 0 ? i : null, value: Viz.Format(value));
        return i >= 0;
    }

    public void Clear([CallerLineNumber] int line = 0)
    {
        items.Clear();
        Record(OpKind.Remove, line, text: "*");
    }

    /// <summary>Index of <paramref name="value"/> or -1. Silent.</summary>
    public int IndexOf(T value) => items.IndexOf(value);
}
