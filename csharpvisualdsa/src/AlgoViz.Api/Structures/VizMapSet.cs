using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

public sealed class VizMap<TKey, TValue> : VizStructure where TKey : notnull
{
    private readonly Dictionary<TKey, (int Id, TValue Value)> entries = [];

    internal VizMap(string label, int line) : base(label, StructureKind.Map, 0, null, line) { }

    public int Count => entries.Count;

    public bool IsEmpty => entries.Count == 0;

    public IReadOnlyList<TKey> Keys => entries.Keys.ToArray();

    public void Set(TKey key, TValue value, [CallerLineNumber] int line = 0)
    {
        int id;
        if (entries.TryGetValue(key, out var entry)) id = entry.Id;
        else
        {
            CheckSize(entries.Count + 1);
            id = NewNodeId();
        }
        entries[key] = (id, value);
        Record(OpKind.Write, line, id, value: Viz.Format(value), text: Viz.Format(key));
    }

    public TValue Get(TKey key, [CallerLineNumber] int line = 0)
    {
        if (!entries.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Key {Viz.Format(key)} is not in '{Label}'.");
        Record(OpKind.Read, line, entry.Id, value: Viz.Format(entry.Value), text: Viz.Format(key));
        return entry.Value;
    }

    public bool ContainsKey(TKey key, [CallerLineNumber] int line = 0)
    {
        var found = entries.TryGetValue(key, out var entry);
        Record(OpKind.Read, line, found ? entry.Id : null, text: Viz.Format(key));
        return found;
    }

    public bool Remove(TKey key, [CallerLineNumber] int line = 0)
    {
        var found = entries.Remove(key, out var entry);
        Record(OpKind.Remove, line, found ? entry.Id : null, text: Viz.Format(key));
        return found;
    }
}

public sealed class VizSet<T> : VizStructure where T : notnull
{
    private readonly Dictionary<T, int> ids = [];

    internal VizSet(string label, int line) : base(label, StructureKind.Set, 0, null, line) { }

    public int Count => ids.Count;

    public bool IsEmpty => ids.Count == 0;

    public IReadOnlyList<T> Items => ids.Keys.ToArray();

    /// <summary>Adds <paramref name="value"/>; false when it was already there.</summary>
    public bool Add(T value, [CallerLineNumber] int line = 0)
    {
        var added = !ids.TryGetValue(value, out var id);
        if (added)
        {
            CheckSize(ids.Count + 1);
            id = NewNodeId();
            ids[value] = id;
        }
        Record(OpKind.Insert, line, id, value: Viz.Format(value));
        return added;
    }

    public bool Contains(T value, [CallerLineNumber] int line = 0)
    {
        var found = ids.TryGetValue(value, out var id);
        Record(OpKind.Read, line, found ? id : null, value: Viz.Format(value));
        return found;
    }

    public bool Remove(T value, [CallerLineNumber] int line = 0)
    {
        var found = ids.Remove(value, out var id);
        Record(OpKind.Remove, line, found ? id : null, value: Viz.Format(value));
        return found;
    }
}
