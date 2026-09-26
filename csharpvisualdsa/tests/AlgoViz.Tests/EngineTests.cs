using System.Text;
using AlgoViz.Api;
using AlgoViz.Core.Runner;
using AlgoViz.Core.Visual;

namespace AlgoViz.Tests;

/// <summary>State engine: pure, deterministic replay; checkpointed seeking; per-structure semantics.</summary>
public static class EngineTests
{
    private const string Mixed = """
        var a = Viz.Array("a", 5, 3, 8, 1);
        a.Swap(0, 3);
        a.Mark(1, VizColor.Done);
        a.Pointer("i", 2);
        var l = Viz.List<int>("l", 1, 2, 3);
        l.Mark(2, VizColor.Warn);
        l.Insert(0, 9);
        l.RemoveAt(1);
        var s = Viz.Stack<int>("s");
        for (int i = 0; i < 30; i++) s.Push(i);
        for (int i = 0; i < 25; i++) s.Pop();
        var ll = Viz.LinkedList<int>("ll");
        for (int i = 0; i < 4; i++) ll.AddLast(i);
        LinkedNode<int> prev = null;
        var cur = ll.Head;
        while (cur != null) { var next = cur.Next; ll.SetNext(cur, prev); prev = cur; cur = next; }
        ll.SetHead(prev);
        var t = Viz.BinaryTree<int>("t");
        var r = t.SetRoot(5);
        var x = r.SetLeft(3);
        x.SetLeft(1);
        r.SetLeft(x.Right);
        x.SetRight(r);
        t.SetRoot(x);
        var g = Viz.Graph("g");
        for (int i = 0; i < 5; i++) g.AddNode(i);
        for (int i = 0; i < 4; i++) g.AddEdge(i, i + 1, i);
        g.MarkEdge(1, 2, VizColor.Path);
        g.RemoveNode(2);
        var m = Viz.Map<string, int>("m");
        m.Set("a", 1); m.Set("b", 2); m.Set("a", 3); m.Remove("b");
        var grid = Viz.Grid("grid", 3, 3, '.');
        grid.Set(1, 1, '#');
        grid.Mark(0, 0);
        Viz.Var("k", 1);
        Viz.Var("k", 2);
        Viz.Log("done");
        """;

    public static IEnumerable<(string, Func<Task>)> All() =>
    [
        ("engine: replay is deterministic", Replay),
        ("engine: seeking backwards equals replay from 0", SeekEqualsReplay),
        ("engine: transient vs persistent highlights", Highlights),
        ("engine: sequence semantics", () => Sync(Sequences)),
        ("engine: linked list reversal", LinkedList),
        ("engine: binary tree rotation and detach", () => Sync(BinaryTree)),
        ("engine: graph, map, grid, vars", GraphMapGrid),
        ("layout: tree, heap and graph", () => Sync(Layouts)),
    ];

    private static Task Sync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static async Task<IReadOnlyList<VizOp>> Ops(string code)
    {
        var result = await new ScriptRunner().RunAsync(code);
        Check.True(!result.HasCompileErrors, string.Join("; ", result.CompileErrors));
        Check.True(result.Error is null, result.Error?.Message);
        return result.Ops;
    }

    private static async Task Replay()
    {
        var ops = await Ops(Mixed);
        Check.True(ops.Count > 100, "enough ops to cross checkpoints");
        Check.Equal(Dump(StateEngine.Replay(ops)), Dump(StateEngine.Replay(ops)));
        var first = StateEngine.Apply(Frame.Empty, ops[0]);
        Check.Equal(0, Frame.Empty.Index);
        Check.Equal(0, Frame.Empty.Structures.Count, "Apply never mutates its input");
        Check.Equal(1, first.Index);
    }

    private static async Task SeekEqualsReplay()
    {
        var ops = await Ops(Mixed);
        var playback = new Playback(ops);
        Check.Equal(ops.Count + 1, playback.FrameCount);
        var expected = new List<string>();
        var frame = Frame.Empty;
        expected.Add(Dump(frame));
        foreach (var op in ops) expected.Add(Dump(frame = StateEngine.Apply(frame, op)));

        playback.Seek(playback.LastFrame);
        for (var i = playback.LastFrame; i >= 0; i--)
            Check.Equal(expected[i], Dump(playback.Seek(i)), $"step back to {i}");
        var random = new Random(42);
        for (var n = 0; n < 200; n++)
        {
            var i = random.Next(playback.FrameCount);
            Check.Equal(expected[i], Dump(playback.Seek(i)), $"seek to {i}");
        }
        Check.True(playback.Seek(1_000_000).Index == playback.LastFrame && playback.AtEnd);
    }

    private static async Task Highlights()
    {
        var ops = await Ops("""
            var a = Viz.Array("a", 1, 2, 3);
            a.Compare(0, 1);
            a.Mark(2, VizColor.Done);
            a.Get(1);
            """);
        var playback = new Playback(ops);
        var compare = playback.Seek(2);
        Check.True(compare.Highlights.Single() is { Kind: HighlightKind.Compare, A: 0, B: 1 });
        var marked = playback.Seek(3);
        Check.Equal(0, marked.Highlights.Count, "Compare highlight is gone after its frame");
        var read = playback.Seek(4);
        Check.True(read.Highlights.Single() is { Kind: HighlightKind.Read, A: 1 });
        Check.Equal("done", ((SequenceState)read.Structures[0]).Marks[2], "Mark persists");
        Check.Equal(4, read.Line, "frame line is the op's source line");
    }

    private static void Sequences()
    {
        var ops = new List<VizOp>();
        void Op(OpKind kind, int? a = null, int? b = null, string? value = null, string? text = null) =>
            ops.Add(new VizOp(ops.Count, kind, 1, null, a, b, value, text, 1));
        ops.Add(new VizOp(0, OpKind.Create, 1, null, (int)StructureKind.List, 3, VizOp.Pack(["a", "b", "c"]), "l", 1));
        Op(OpKind.Mark, 2, text: "done");
        Op(OpKind.Swap, 0, 2);
        Op(OpKind.Insert, 1, value: "x");
        Op(OpKind.Remove, 0);
        Op(OpKind.Write, 0, value: "y");
        var s = (SequenceState)StateEngine.Replay(ops).Structures[0];
        Check.Equal("y,b,a", string.Join(",", s.Items.Select(e => e.Value)));
        Check.Equal("done", s.Marks[2], "mark shifted right by the insert and left by the remove");
        Check.Equal("4,2,1", string.Join(",", s.Items.Select(e => e.Id)), "ids follow their elements and are never reused");
    }

    private static async Task LinkedList()
    {
        var ops = await Ops("""
            var ll = Viz.LinkedList<int>("ll");
            for (int i = 1; i <= 3; i++) ll.AddLast(i);
            LinkedNode<int> prev = null;
            var cur = ll.Head;
            while (cur != null) { var next = cur.Next; ll.SetNext(cur, prev); prev = cur; cur = next; }
            ll.SetHead(prev);
            """);
        var s = (LinkedListState)StateEngine.Replay(ops).Structures[0];
        var values = new List<string>();
        for (var n = s.Head; n is { } id; n = s.Nodes[id].Next) values.Add(s.Nodes[id].Value);
        Check.Equal("3,2,1", string.Join(",", values));
        Check.Equal("3,2,1", string.Join(",", s.Order.Select(id => s.Nodes[id].Value)), "display order follows the new head");
    }

    private static void BinaryTree()
    {
        // Mirrors ApiOpTests.BinaryTreeOps: right rotation at the root.
        var ops = new List<VizOp>();
        void Op(OpKind kind, int? node, int? a = null, int? b = null, string? value = null) =>
            ops.Add(new VizOp(ops.Count, kind, 1, node, a, b, value, null, 1));
        ops.Add(new VizOp(0, OpKind.Create, 1, null, (int)StructureKind.BinaryTree, 0, null, "t", 1));
        Op(OpKind.Insert, 1, value: "5");
        Op(OpKind.Insert, 2, 1, 0, "3");
        Op(OpKind.Insert, 3, 1, 1, "8");
        Op(OpKind.Unlink, 1, b: 0);
        var mid = (TreeState)StateEngine.Replay(ops).Structures[0];
        Check.True(mid.Detached.SequenceEqual([2]), "unlinked child is detached, not deleted");
        Op(OpKind.Move, 1, 2, 1);
        Op(OpKind.Move, 2);
        var s = (TreeState)StateEngine.Replay(ops).Structures[0];
        Check.Equal(2, s.Root);
        Check.Equal(0, s.Detached.Count);
        Check.True(s.Nodes[2].Children.SequenceEqual([null, 1]));
        Check.True(s.Nodes[1].Children.SequenceEqual([null, 3]));
        Op(OpKind.Remove, 1);
        s = (TreeState)StateEngine.Replay(ops).Structures[0];
        Check.Equal(1, s.Nodes.Count, "removing a node removes its subtree");
    }

    private static async Task GraphMapGrid()
    {
        var frame = StateEngine.Replay(await Ops(Mixed));
        var g = (GraphState)frame.Structures.Single(s => s.Label == "g");
        Check.Equal(4, g.Nodes.Count);
        Check.Equal("0-1,3-4", string.Join(",", g.Edges.Select(e => $"{e.From}-{e.To}")));
        Check.Equal(0, g.EdgeMarks.Count, "marks of removed edges are dropped");
        var m = (MapState)frame.Structures.Single(s => s.Label == "m");
        Check.Equal("a=3", string.Join(",", m.Entries.Select(e => $"{e.Key}={e.Value}")));
        var grid = (GridState)frame.Structures.Single(s => s.Label == "grid");
        Check.Equal((3, 3), (grid.Rows, grid.Cols));
        Check.Equal("#", grid.Cells[4]);
        Check.Equal("active", grid.Marks[0]);
        Check.Equal("k=2", string.Join(",", frame.Vars.Select(v => $"{v.Name}={v.Value}")));
        Check.Equal("done", frame.Log.Single().Text);
    }

    private static void Layouts()
    {
        // Heap of 5: root centered over its children, leaves on consecutive slots.
        var heap = Layout.Heap(5);
        var p = heap.Nodes.ToDictionary(n => n.Id, n => n.Position);
        Check.Equal(3, (int)heap.Height);
        Check.True(p[3].X < p[4].X && p[4].X < p[2].X, "in-order left to right");
        Check.Equal((p[1].X + p[2].X) / 2, p[0].X);
        Check.Equal(4, heap.Edges.Count);

        var graph = new GraphState(1, "g", false, [new(1, null, null), new(2, null, null), new(3, null, null)], [],
            System.Collections.Immutable.ImmutableDictionary<int, string>.Empty,
            System.Collections.Immutable.ImmutableDictionary<(int, int), string>.Empty);
        var circle = Layout.Graph(graph, 200, 200, 20);
        Check.Equal(100.0, Math.Round(circle[1].X, 6));
        Check.Equal(20.0, Math.Round(circle[1].Y, 6));
        var placed = Layout.Graph(graph with { Nodes = [new(1, 0, 0), new(2, 10, 0)] }, 200, 100, 20);
        Check.True(placed[1].X < placed[2].X && placed[1].Y == placed[2].Y);
    }

    /// <summary>A canonical text form of a frame, for equality checks.</summary>
    private static string Dump(Frame frame)
    {
        var sb = new StringBuilder();
        sb.Append($"#{frame.Index} line={frame.Line} next={frame.NextElementId}\n");
        foreach (var s in frame.Structures)
        {
            sb.Append($"{s.Id}:{s.Kind}:{s.Label} ");
            sb.Append(s switch
            {
                SequenceState q => $"[{string.Join(",", q.Items.Select(e => $"{e.Id}={e.Value}"))}] marks={Join(q.Marks)} ptr={Join(q.Pointers)} tree={q.ShowAsTree}",
                LinkedListState l => $"head={l.Head} order={string.Join(",", l.Order)} {string.Join(",", l.Nodes.OrderBy(n => n.Key).Select(n => $"{n.Key}={n.Value.Value}>{n.Value.Next}"))}",
                TreeState t => $"root={t.Root} det={string.Join(",", t.Detached)} {string.Join(";", t.Nodes.OrderBy(n => n.Key).Select(n => $"{n.Key}={n.Value.Value}^{n.Value.Parent}[{string.Join(",", n.Value.Children)}]"))} marks={Join(t.Marks)}",
                GraphState g => $"{string.Join(",", g.Nodes)} {string.Join(",", g.Edges)} {Join(g.NodeMarks)} {Join(g.EdgeMarks)}",
                GridState g => $"{string.Join(",", g.Cells)} {Join(g.Marks)}",
                MapState m => string.Join(",", m.Entries),
                _ => "",
            });
            sb.Append('\n');
        }
        sb.Append($"hl={string.Join(",", frame.Highlights)}\nvars={string.Join(",", frame.Vars)}\nlog={string.Join(",", frame.Log)}");
        return sb.ToString();
    }

    private static string Join<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> items) =>
        string.Join(",", items.Select(kv => $"{kv.Key}={kv.Value}").Order(StringComparer.Ordinal));
}
