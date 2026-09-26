using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

/// <summary>A graph with integer node ids. Undirected by default.</summary>
public sealed class VizGraph : VizStructure
{
    private readonly Dictionary<int, List<int>> adjacency = [];
    private readonly List<int> order = [];
    private readonly Dictionary<(int, int), double?> weights = [];

    internal VizGraph(string label, bool directed, int line) : base(label, StructureKind.Graph, directed ? 1 : 0, null, line)
    {
        Directed = directed;
    }

    public bool Directed { get; }

    public int NodeCount => order.Count;

    public int EdgeCount => weights.Count;

    public IReadOnlyList<int> Nodes => order.ToArray();

    public bool HasNode(int id) => adjacency.ContainsKey(id);

    public bool HasEdge(int a, int b) => weights.ContainsKey(Key(a, b));

    /// <summary>The weight of edge a–b (1 when it has none). Silent.</summary>
    public double Weight(int a, int b) =>
        weights.TryGetValue(Key(a, b), out var w) ? w ?? 1 : throw new InvalidOperationException($"There is no edge {a}–{b}.");

    /// <summary>Adds node <paramref name="id"/>; give x/y (pixels) to place it yourself, otherwise it's laid out in a circle.</summary>
    public void AddNode(int id, int? x = null, int? y = null, [CallerLineNumber] int line = 0)
    {
        if (adjacency.ContainsKey(id)) throw new InvalidOperationException($"Node {id} already exists.");
        CheckSize(order.Count + 1);
        adjacency[id] = [];
        order.Add(id);
        Record(OpKind.Insert, line, id, a: x, b: y);
    }

    public void RemoveNode(int id, [CallerLineNumber] int line = 0)
    {
        CheckNode(id);
        foreach (var key in weights.Keys.Where(k => k.Item1 == id || k.Item2 == id).ToList())
            weights.Remove(key);
        foreach (var list in adjacency.Values) list.RemoveAll(n => n == id);
        adjacency.Remove(id);
        order.Remove(id);
        Record(OpKind.Remove, line, id);
    }

    /// <summary>Adds edge a–b (a→b when directed), or updates its weight if it exists.</summary>
    public void AddEdge(int a, int b, double? weight = null, [CallerLineNumber] int line = 0)
    {
        CheckNode(a);
        CheckNode(b);
        var key = Key(a, b);
        if (!weights.ContainsKey(key))
        {
            CheckSize(weights.Count + 1);
            adjacency[a].Add(b);
            if (!Directed && a != b) adjacency[b].Add(a);
        }
        weights[key] = weight;
        Record(OpKind.Link, line, a: a, b: b, value: weight is null ? null : Viz.Format(weight.Value));
    }

    public void RemoveEdge(int a, int b, [CallerLineNumber] int line = 0)
    {
        if (!weights.Remove(Key(a, b))) throw new InvalidOperationException($"There is no edge {a}–{b}.");
        adjacency[a].Remove(b);
        if (!Directed) adjacency[b].Remove(a);
        Record(OpKind.Unlink, line, a: a, b: b);
    }

    /// <summary>The nodes reachable over one edge, in insertion order.</summary>
    public IReadOnlyList<int> Neighbors(int id, [CallerLineNumber] int line = 0)
    {
        CheckNode(id);
        Record(OpKind.Read, line, id);
        return adjacency[id].ToArray();
    }

    public void Visit(int id, [CallerLineNumber] int line = 0)
    {
        CheckNode(id);
        Record(OpKind.Visit, line, id);
    }

    public void MarkNode(int id, VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        CheckNode(id);
        Record(OpKind.Mark, line, id, text: color.Name());
    }

    public void UnmarkNode(int id, [CallerLineNumber] int line = 0)
    {
        CheckNode(id);
        Record(OpKind.Unmark, line, id);
    }

    public void MarkEdge(int a, int b, VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        CheckEdge(a, b);
        Record(OpKind.Mark, line, a: a, b: b, text: color.Name());
    }

    public void UnmarkEdge(int a, int b, [CallerLineNumber] int line = 0)
    {
        CheckEdge(a, b);
        Record(OpKind.Unmark, line, a: a, b: b);
    }

    private (int, int) Key(int a, int b) => Directed || a <= b ? (a, b) : (b, a);

    private void CheckNode(int id)
    {
        if (!adjacency.ContainsKey(id)) throw new KeyNotFoundException($"Node {id} doesn't exist in '{Label}'. Call AddNode first.");
    }

    private void CheckEdge(int a, int b)
    {
        if (!weights.ContainsKey(Key(a, b))) throw new InvalidOperationException($"There is no edge {a}–{b}.");
    }
}
