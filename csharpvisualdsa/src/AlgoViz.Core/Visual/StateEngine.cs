using System.Collections.Immutable;
using AlgoViz.Api;

namespace AlgoViz.Core.Visual;

/// <summary>
/// Pure, deterministic replay: <c>Apply(frame, op)</c> never mutates its input, and applying ops 0..N to
/// <see cref="Frame.Empty"/> always yields the same frame N. Ops that don't fit the state are ignored.
/// </summary>
public static class StateEngine
{
    public static Frame Replay(IEnumerable<VizOp> ops) => ops.Aggregate(Frame.Empty, Apply);

    public static Frame Apply(Frame frame, VizOp op)
    {
        var next = frame with { Index = frame.Index + 1, Op = op, Highlights = [] };
        switch (op.Kind)
        {
            case OpKind.Create:
                return Create(next, op);
            case OpKind.Log:
                return next with { Log = next.Log.Add(new LogLine(op.Text ?? "", op.Line)) };
            case OpKind.Var:
                return SetVar(next, op);
            case OpKind.Step:
                return next;
        }

        var index = next.Structures.FindIndex(s => s.Id == op.StructureId);
        if (index < 0) return next;
        var (state, highlight, usedIds) = next.Structures[index] switch
        {
            SequenceState s => Sequence(s, op, next.NextElementId),
            LinkedListState s => (LinkedList(s, op), NodeHighlight(op), 0),
            TreeState s => (Tree(s, op), NodeHighlight(op), 0),
            GraphState s => Graph(s, op),
            GridState s => Grid(s, op),
            MapState s => Map(s, op),
            var s => (s, null, 0),
        };
        return next with
        {
            Structures = next.Structures.SetItem(index, state),
            Highlights = highlight is null ? [] : [highlight],
            NextElementId = next.NextElementId + usedIds,
        };
    }

    private static Frame Create(Frame frame, VizOp op)
    {
        var label = op.Text ?? "";
        var values = VizOp.Unpack(op.Value);
        var kind = (StructureKind)(op.A ?? 0);
        StructureState state = kind switch
        {
            StructureKind.Array or StructureKind.List or StructureKind.Stack or StructureKind.Queue or StructureKind.Deque =>
                new SequenceState(op.StructureId, label, kind,
                    [.. values.Select((v, i) => new Element(frame.NextElementId + i, v))],
                    ImmutableDictionary<int, string>.Empty, ImmutableSortedDictionary<string, int>.Empty, false),
            StructureKind.LinkedList =>
                new LinkedListState(op.StructureId, label, ImmutableDictionary<int, LinkedNodeState>.Empty, [], null),
            StructureKind.Tree or StructureKind.BinaryTree =>
                new TreeState(op.StructureId, label, kind, ImmutableDictionary<int, TreeNodeState>.Empty, null, [],
                    ImmutableDictionary<int, string>.Empty),
            StructureKind.Graph =>
                new GraphState(op.StructureId, label, op.B == 1, [], [], ImmutableDictionary<int, string>.Empty,
                    ImmutableDictionary<(int, int), string>.Empty),
            StructureKind.Grid => CreateGrid(op.StructureId, label, op.B ?? 1, values),
            StructureKind.Map or StructureKind.Set => new MapState(op.StructureId, label, kind, []),
            _ => throw new InvalidOperationException($"Unknown structure kind {kind}."),
        };
        var used = state is SequenceState ? values.Length : 0;
        return frame with { Structures = frame.Structures.Add(state), NextElementId = frame.NextElementId + used };
    }

    private static GridState CreateGrid(int id, string label, int rows, string[] cells)
    {
        rows = Math.Max(1, rows);
        return new GridState(id, label, rows, cells.Length / rows, [.. cells], ImmutableDictionary<int, string>.Empty);
    }

    private static Frame SetVar(Frame frame, VizOp op)
    {
        var name = op.Text ?? "";
        var value = new WatchVar(name, op.Value ?? "∅", frame.Index);
        var i = frame.Vars.FindIndex(v => v.Name == name);
        return frame with { Vars = i < 0 ? frame.Vars.Add(value) : frame.Vars.SetItem(i, value) };
    }

    private static Highlight? NodeHighlight(VizOp op) => op.Kind switch
    {
        OpKind.Visit => new Highlight(op.StructureId, HighlightKind.Visit, op.NodeId, null, null),
        OpKind.Read => new Highlight(op.StructureId, HighlightKind.Read, op.NodeId, null, null),
        OpKind.Insert or OpKind.Write or OpKind.Move or OpKind.Link when op.NodeId is not null =>
            new Highlight(op.StructureId, HighlightKind.Change, op.NodeId, null, null),
        _ => null,
    };

    private static Highlight At(VizOp op, HighlightKind kind, int? a, int? b = null) =>
        new(op.StructureId, kind, null, a, b);

    // ---- Array, List, Stack, Queue, Deque ----

    private static (StructureState, Highlight?, int) Sequence(SequenceState s, VizOp op, int nextId)
    {
        var count = s.Items.Count;
        bool Valid(int? i) => i is { } v && v >= 0 && v < count;

        switch (op.Kind)
        {
            case OpKind.Read when Valid(op.A):
                return (s, At(op, HighlightKind.Read, op.A), 0);
            case OpKind.Compare when Valid(op.A) && Valid(op.B):
                return (s, At(op, HighlightKind.Compare, op.A, op.B), 0);
            case OpKind.Write when Valid(op.A):
                var i = op.A!.Value;
                return (s with { Items = s.Items.SetItem(i, s.Items[i] with { Value = op.Value ?? "" }) },
                    At(op, HighlightKind.Change, i), 0);
            case OpKind.Swap when Valid(op.A) && Valid(op.B):
                var (a, b) = (op.A!.Value, op.B!.Value);
                return (s with { Items = s.Items.SetItem(a, s.Items[b]).SetItem(b, s.Items[a]) },
                    At(op, HighlightKind.Change, a, b), 0);
            case OpKind.Insert or OpKind.Push or OpKind.Enqueue when op.A is { } at && at >= 0 && at <= count:
                return (s with
                {
                    Items = s.Items.Insert(at, new Element(nextId, op.Value ?? "")),
                    Marks = ShiftMarks(s.Marks, at, +1),
                }, At(op, HighlightKind.Change, at), 1);
            case OpKind.Remove when op.Text == "*":
                return (s with { Items = [], Marks = ImmutableDictionary<int, string>.Empty }, null, 0);
            case OpKind.Remove or OpKind.Pop or OpKind.Dequeue when Valid(op.A):
                var r = op.A!.Value;
                return (s with { Items = s.Items.RemoveAt(r), Marks = ShiftMarks(s.Marks.Remove(r), r, -1) }, null, 0);
            case OpKind.Mark when op.A is null && op.Text == "view:tree":
                return (s with { ShowAsTree = true }, null, 0);
            case OpKind.Mark when Valid(op.A):
                return (s with { Marks = s.Marks.SetItem(op.A!.Value, op.Text ?? "active") }, null, 0);
            case OpKind.Unmark when op.A is { } u:
                return (s with { Marks = s.Marks.Remove(u) }, null, 0);
            case OpKind.Pointer when op.Text is { } name:
                return (s with { Pointers = op.A is { } p ? s.Pointers.SetItem(name, p) : s.Pointers.Remove(name) }, null, 0);
            default:
                return (s, null, 0);
        }
    }

    /// <summary>Moves marks at or after <paramref name="from"/> by <paramref name="delta"/> after an insert/remove.</summary>
    private static ImmutableDictionary<int, string> ShiftMarks(ImmutableDictionary<int, string> marks, int from, int delta) =>
        marks.IsEmpty
            ? marks
            : marks.ToImmutableDictionary(kv => kv.Key >= from ? kv.Key + delta : kv.Key, kv => kv.Value);

    // ---- LinkedList ----

    private static LinkedListState LinkedList(LinkedListState s, VizOp op)
    {
        var nodes = s.Nodes;
        switch (op.Kind)
        {
            case OpKind.Insert when op.NodeId is { } id && !nodes.ContainsKey(id):
                if (op.A is { } after && nodes.TryGetValue(after, out var prev))
                {
                    nodes = nodes.Add(id, new LinkedNodeState(id, op.Value ?? "", prev.Next)).SetItem(after, prev with { Next = id });
                    return s with { Nodes = nodes, Order = s.Order.Insert(s.Order.IndexOf(after) + 1, id) };
                }
                nodes = nodes.Add(id, new LinkedNodeState(id, op.Value ?? "", s.Head));
                var headAt = s.Head is { } h ? Math.Max(0, s.Order.IndexOf(h)) : 0;
                return s with { Nodes = nodes, Order = s.Order.Insert(headAt, id), Head = id };
            case OpKind.Remove when op.NodeId is { } id && nodes.TryGetValue(id, out var removed):
                foreach (var n in nodes.Values.Where(n => n.Next == id))
                    nodes = nodes.SetItem(n.Id, n with { Next = removed.Next });
                return s with
                {
                    Nodes = nodes.Remove(id),
                    Order = s.Order.Remove(id),
                    Head = s.Head == id ? removed.Next : s.Head,
                };
            case OpKind.Link when op.NodeId is null:
                var head = op.A is { } newHead && nodes.ContainsKey(newHead) ? newHead : (int?)null;
                return s with { Head = head, Order = ChainFirst(nodes, head, s.Order) };
            case OpKind.Link when op.NodeId is { } id && nodes.TryGetValue(id, out var node):
                var next = op.A is { } target && nodes.ContainsKey(target) ? target : (int?)null;
                return s with { Nodes = nodes.SetItem(id, node with { Next = next }) };
            case OpKind.Write when op.NodeId is { } id && nodes.TryGetValue(id, out var written):
                return s with { Nodes = nodes.SetItem(id, written with { Value = op.Value ?? "" }) };
            default:
                return s;
        }
    }

    /// <summary>Display order after a head change: the chain from the head, then the rest in their old order.</summary>
    private static ImmutableList<int> ChainFirst(ImmutableDictionary<int, LinkedNodeState> nodes, int? head, ImmutableList<int> order)
    {
        var chain = new List<int>();
        var seen = new HashSet<int>();
        for (var n = head; n is { } id && nodes.ContainsKey(id) && seen.Add(id); n = nodes[id].Next)
            chain.Add(id);
        return [.. chain, .. order.Where(id => !seen.Contains(id))];
    }

    // ---- Tree and BinaryTree ----

    private static TreeState Tree(TreeState s, VizOp op)
    {
        switch (op.Kind)
        {
            case OpKind.Insert when op.NodeId is { } id && !s.Nodes.ContainsKey(id):
                var node = new TreeNodeState(id, op.Value ?? "", op.A, s.Binary ? [null, null] : []);
                if (op.A is not { } parentId)
                {
                    return s with
                    {
                        Nodes = ImmutableDictionary<int, TreeNodeState>.Empty.Add(id, node),
                        Root = id,
                        Detached = [],
                        Marks = ImmutableDictionary<int, string>.Empty,
                    };
                }
                if (!s.Nodes.ContainsKey(parentId)) return s;
                return AttachChild(s with { Nodes = s.Nodes.Add(id, node) }, id, parentId, op.B ?? 0);
            case OpKind.Move when op.NodeId is { } id && s.Nodes.ContainsKey(id):
                if (op.A is not { } newParent)
                {
                    if (s.Root == id) return s;
                    var detached = Detach(s, id);
                    return detached with
                    {
                        Root = id,
                        Detached = detached.Root is { } oldRoot ? detached.Detached.Add(oldRoot) : detached.Detached,
                    };
                }
                if (!s.Nodes.ContainsKey(newParent) || IsInSubtree(s, newParent, id)) return s;
                if (s.Binary && s.Nodes[newParent].Children[op.B ?? 0] == id) return s;
                return AttachChild(Detach(s, id), id, newParent, op.B ?? 0);
            case OpKind.Unlink when op.NodeId is { } parent && s.Binary && s.Nodes.TryGetValue(parent, out var p):
                return p.Children[op.B ?? 0] is { } child ? DetachToFloating(s, child) : s;
            case OpKind.Remove when op.NodeId is { } id && s.Nodes.ContainsKey(id):
                var without = Detach(s, id);
                var doomed = Subtree(s, id);
                return without with
                {
                    Nodes = without.Nodes.RemoveRange(doomed),
                    Marks = without.Marks.RemoveRange(doomed),
                };
            case OpKind.Write when op.NodeId is { } id && s.Nodes.TryGetValue(id, out var written):
                return s with { Nodes = s.Nodes.SetItem(id, written with { Value = op.Value ?? "" }) };
            case OpKind.Mark when op.NodeId is { } id && s.Nodes.ContainsKey(id):
                return s with { Marks = s.Marks.SetItem(id, op.Text ?? "active") };
            case OpKind.Unmark when op.NodeId is { } id:
                return s with { Marks = s.Marks.Remove(id) };
            default:
                return s;
        }
    }

    /// <summary>Puts detached node <paramref name="id"/> under <paramref name="parentId"/>; a displaced binary child becomes detached.</summary>
    private static TreeState AttachChild(TreeState s, int id, int parentId, int slot)
    {
        var parent = s.Nodes[parentId];
        if (s.Binary)
        {
            slot = Math.Clamp(slot, 0, 1);
            if (parent.Children[slot] is { } displaced)
            {
                s = DetachToFloating(s, displaced);
                parent = s.Nodes[parentId];
            }
            parent = parent with { Children = parent.Children.SetItem(slot, id) };
        }
        else
        {
            parent = parent with { Children = parent.Children.Insert(Math.Clamp(slot, 0, parent.Children.Count), id) };
        }
        return s with { Nodes = s.Nodes.SetItem(parentId, parent).SetItem(id, s.Nodes[id] with { Parent = parentId }) };
    }

    private static TreeState DetachToFloating(TreeState s, int id)
    {
        var detached = Detach(s, id);
        return detached with { Detached = detached.Detached.Add(id) };
    }

    /// <summary>Unhooks a node from its parent, the root slot or the detached list (the node itself stays).</summary>
    private static TreeState Detach(TreeState s, int id)
    {
        var node = s.Nodes[id];
        if (node.Parent is { } parentId && s.Nodes.TryGetValue(parentId, out var parent))
        {
            var children = s.Binary
                ? parent.Children.Select(c => c == id ? null : c).ToImmutableList()
                : parent.Children.Remove(id);
            return s with { Nodes = s.Nodes.SetItem(parentId, parent with { Children = children }).SetItem(id, node with { Parent = null }) };
        }
        if (s.Root == id) return s with { Root = null };
        return s with { Detached = s.Detached.Remove(id) };
    }

    private static bool IsInSubtree(TreeState s, int candidate, int root)
    {
        for (int? n = candidate; n is { } id && s.Nodes.TryGetValue(id, out var node); n = node.Parent)
            if (id == root) return true;
        return false;
    }

    private static List<int> Subtree(TreeState s, int root)
    {
        var result = new List<int>();
        var stack = new Stack<int>([root]);
        while (stack.TryPop(out var id))
        {
            if (!s.Nodes.TryGetValue(id, out var node)) continue;
            result.Add(id);
            foreach (var c in node.Children)
                if (c is { } child) stack.Push(child);
        }
        return result;
    }

    // ---- Graph ----

    private static (StructureState, Highlight?, int) Graph(GraphState s, VizOp op)
    {
        bool HasNode(int? id) => id is { } v && s.Nodes.Any(n => n.Id == v);
        int EdgeIndex(int a, int b) => s.Edges.FindIndex(e => s.EdgeKey(e.From, e.To) == s.EdgeKey(a, b));

        switch (op.Kind)
        {
            case OpKind.Insert when op.NodeId is { } id && !HasNode(id):
                return (s with { Nodes = s.Nodes.Add(new GraphNodeState(id, op.A, op.B)) },
                    new Highlight(s.Id, HighlightKind.Change, id, null, null), 0);
            case OpKind.Remove when HasNode(op.NodeId):
                var removed = op.NodeId!.Value;
                return (s with
                {
                    Nodes = s.Nodes.RemoveAll(n => n.Id == removed),
                    Edges = s.Edges.RemoveAll(e => e.From == removed || e.To == removed),
                    NodeMarks = s.NodeMarks.Remove(removed),
                    EdgeMarks = s.EdgeMarks.RemoveRange(s.EdgeMarks.Keys.Where(k => k.Item1 == removed || k.Item2 == removed)),
                }, null, 0);
            case OpKind.Link when HasNode(op.A) && HasNode(op.B):
                var (a, b) = (op.A!.Value, op.B!.Value);
                var edge = new GraphEdgeState(a, b, op.Value);
                var existing = EdgeIndex(a, b);
                return (s with { Edges = existing < 0 ? s.Edges.Add(edge) : s.Edges.SetItem(existing, edge) },
                    At(op, HighlightKind.Change, a, b), 0);
            case OpKind.Unlink when op.A is { } ua && op.B is { } ub && EdgeIndex(ua, ub) is var ui && ui >= 0:
                return (s with { Edges = s.Edges.RemoveAt(ui), EdgeMarks = s.EdgeMarks.Remove(s.EdgeKey(ua, ub)) }, null, 0);
            case OpKind.Read when HasNode(op.NodeId):
                return (s, new Highlight(s.Id, HighlightKind.Read, op.NodeId, null, null), 0);
            case OpKind.Visit when HasNode(op.NodeId):
                return (s, new Highlight(s.Id, HighlightKind.Visit, op.NodeId, null, null), 0);
            case OpKind.Mark when op.NodeId is { } id:
                return (s with { NodeMarks = s.NodeMarks.SetItem(id, op.Text ?? "active") }, null, 0);
            case OpKind.Mark when op.A is { } ma && op.B is { } mb:
                return (s with { EdgeMarks = s.EdgeMarks.SetItem(s.EdgeKey(ma, mb), op.Text ?? "active") }, null, 0);
            case OpKind.Unmark when op.NodeId is { } id:
                return (s with { NodeMarks = s.NodeMarks.Remove(id) }, null, 0);
            case OpKind.Unmark when op.A is { } ea && op.B is { } eb:
                return (s with { EdgeMarks = s.EdgeMarks.Remove(s.EdgeKey(ea, eb)) }, null, 0);
            default:
                return (s, null, 0);
        }
    }

    // ---- Grid ----

    private static (StructureState, Highlight?, int) Grid(GridState s, VizOp op)
    {
        if (op.A is not { } r || op.B is not { } c || r < 0 || c < 0 || r >= s.Rows || c >= s.Cols) return (s, null, 0);
        var cell = r * s.Cols + c;
        return op.Kind switch
        {
            OpKind.Read => (s, At(op, HighlightKind.Read, r, c), 0),
            OpKind.Write => (s with { Cells = s.Cells.SetItem(cell, op.Value ?? "") }, At(op, HighlightKind.Change, r, c), 0),
            OpKind.Mark => (s with { Marks = s.Marks.SetItem(cell, op.Text ?? "active") }, null, 0),
            OpKind.Unmark => (s with { Marks = s.Marks.Remove(cell) }, null, 0),
            _ => (s, null, 0),
        };
    }

    // ---- Map and Set ----

    private static (StructureState, Highlight?, int) Map(MapState s, VizOp op)
    {
        var index = op.NodeId is { } id ? s.Entries.FindIndex(e => e.Id == id) : -1;
        Highlight Mark(HighlightKind kind) => new(s.Id, kind, op.NodeId, null, null);
        switch (op.Kind)
        {
            case OpKind.Write or OpKind.Insert when op.NodeId is { } entryId:
                if (index >= 0 && op.Kind == OpKind.Insert) return (s, Mark(HighlightKind.Read), 0);
                var entry = new EntryState(entryId, s.Kind == StructureKind.Map ? op.Text ?? "" : null, op.Value ?? "");
                return (s with { Entries = index < 0 ? s.Entries.Add(entry) : s.Entries.SetItem(index, entry) },
                    Mark(HighlightKind.Change), 0);
            case OpKind.Read:
                return (s, index >= 0 ? Mark(HighlightKind.Read) : null, 0);
            case OpKind.Remove when index >= 0:
                return (s with { Entries = s.Entries.RemoveAt(index) }, null, 0);
            default:
                return (s, null, 0);
        }
    }
}
