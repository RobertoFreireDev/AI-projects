using AlgoViz.Api;

namespace AlgoViz.Tests;

/// <summary>Every Viz API operation records exactly the expected op.</summary>
public static class ApiOpTests
{
    public static IEnumerable<(string, Func<Task>)> All() =>
    [
        ("api: array ops", Sync(ArrayOps)),
        ("api: list ops", Sync(ListOps)),
        ("api: stack, queue, deque ops", Sync(LinearOps)),
        ("api: linked list ops", Sync(LinkedListOps)),
        ("api: n-ary tree ops", Sync(TreeOps)),
        ("api: binary tree ops", Sync(BinaryTreeOps)),
        ("api: graph ops", Sync(GraphOps)),
        ("api: grid ops", Sync(GridOps)),
        ("api: map and set ops", Sync(MapSetOps)),
        ("api: global ops and formatting", Sync(GlobalOps)),
        ("api: silent members record nothing", Sync(SilentMembers)),
        ("api: invalid operations throw and record nothing", Sync(InvalidOps)),
        ("api: max ops limit", Sync(MaxOps)),
    ];

    private static Func<Task> Sync(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    /// <summary>Runs <paramref name="body"/> with a fresh recorder and returns its ops.</summary>
    private static List<VizOp> Record(Action body, int maxOps = Recorder.DefaultMaxOps)
    {
        var recorder = new Recorder(maxOps);
        using (Recorder.Begin(recorder)) body();
        return [.. recorder.Snapshot()];
    }

    private static void Expect(VizOp op, OpKind kind, int? node = null, int? a = null, int? b = null, string? value = null, string? text = null)
    {
        var expected = $"{kind} n={node} a={a} b={b} v={value} t={text}";
        var actual = $"{op.Kind} n={op.NodeId} a={op.A} b={op.B} v={op.Value} t={op.Text}";
        Check.Equal(expected, actual, $"op #{op.Index}");
    }

    private static void ArrayOps()
    {
        var ops = Record(() =>
        {
            var a = Viz.Array("a", 3, 1, 2);
            a.Get(0);
            a.Set(1, 9);
            a.Swap(0, 2);
            a.Compare(0, 1);
            a.Mark(1, VizColor.Done);
            a.Unmark(1);
            a.Pointer("i", 2);
            a.RemovePointer("i");
            a.ShowAsTree();
            Viz.Array<int>("z", 2);
        });
        Check.Equal(11, ops.Count);
        Expect(ops[0], OpKind.Create, a: (int)StructureKind.Array, b: 3, value: VizOp.Pack(["3", "1", "2"]), text: "a");
        Expect(ops[1], OpKind.Read, a: 0, value: "3");
        Expect(ops[2], OpKind.Write, a: 1, value: "9");
        Expect(ops[3], OpKind.Swap, a: 0, b: 2);
        Expect(ops[4], OpKind.Compare, a: 0, b: 1, value: "<");
        Expect(ops[5], OpKind.Mark, a: 1, text: "done");
        Expect(ops[6], OpKind.Unmark, a: 1);
        Expect(ops[7], OpKind.Pointer, a: 2, text: "i");
        Expect(ops[8], OpKind.Pointer, text: "i");
        Expect(ops[9], OpKind.Mark, text: "view:tree");
        Expect(ops[10], OpKind.Create, a: (int)StructureKind.Array, b: 2, value: VizOp.Pack(["0", "0"]), text: "z");
        Check.True(ops.Take(10).All(o => o.StructureId == 1) && ops[10].StructureId == 2);
        Check.True(ops.Select(o => o.Index).SequenceEqual(Enumerable.Range(0, ops.Count)));
        Check.True(ops[1].Line > 0, "[CallerLineNumber] is recorded");
    }

    private static void ListOps()
    {
        var ops = Record(() =>
        {
            var l = Viz.List<int>("l");
            l.Add(5);
            l.Insert(0, 4);
            l.RemoveAt(1);
            Check.True(l.Remove(4));
            Check.True(!l.Remove(7));
            l.Add(1);
            l.Clear();
        });
        Expect(ops[0], OpKind.Create, a: (int)StructureKind.List, b: 0, value: "", text: "l");
        Expect(ops[1], OpKind.Insert, a: 0, value: "5");
        Expect(ops[2], OpKind.Insert, a: 0, value: "4");
        Expect(ops[3], OpKind.Remove, a: 1, value: "5");
        Expect(ops[4], OpKind.Remove, a: 0, value: "4");
        Expect(ops[5], OpKind.Remove, value: "7");
        Expect(ops[6], OpKind.Insert, a: 0, value: "1");
        Expect(ops[7], OpKind.Remove, text: "*");
    }

    private static void LinearOps()
    {
        var ops = Record(() =>
        {
            var s = Viz.Stack<int>("s");
            s.Push(1);
            s.Push(2);
            Check.Equal(2, s.Peek());
            Check.Equal(2, s.Pop());
            var q = Viz.Queue<string>("q");
            q.Enqueue("a");
            q.Enqueue("b");
            Check.Equal("a", q.Peek());
            Check.Equal("a", q.Dequeue());
            var d = Viz.Deque<int>("d");
            d.PushBack(1);
            d.PushFront(0);
            d.PeekFront();
            d.PeekBack();
            d.PopBack();
            d.PopFront();
        });
        Expect(ops[0], OpKind.Create, a: (int)StructureKind.Stack, b: 0, text: "s");
        Expect(ops[1], OpKind.Push, a: 0, value: "1");
        Expect(ops[2], OpKind.Push, a: 1, value: "2");
        Expect(ops[3], OpKind.Read, a: 1, value: "2");
        Expect(ops[4], OpKind.Pop, a: 1, value: "2");
        Expect(ops[5], OpKind.Create, a: (int)StructureKind.Queue, b: 0, text: "q");
        Expect(ops[6], OpKind.Enqueue, a: 0, value: "a");
        Expect(ops[7], OpKind.Enqueue, a: 1, value: "b");
        Expect(ops[8], OpKind.Read, a: 0, value: "a");
        Expect(ops[9], OpKind.Dequeue, a: 0, value: "a");
        Expect(ops[10], OpKind.Create, a: (int)StructureKind.Deque, b: 0, text: "d");
        Expect(ops[11], OpKind.Push, a: 0, value: "1");
        Expect(ops[12], OpKind.Push, a: 0, value: "0");
        Expect(ops[13], OpKind.Read, a: 0, value: "0");
        Expect(ops[14], OpKind.Read, a: 1, value: "1");
        Expect(ops[15], OpKind.Pop, a: 1, value: "1");
        Expect(ops[16], OpKind.Pop, a: 0, value: "0");
    }

    private static void LinkedListOps()
    {
        int n1 = 0, n2 = 0, n3 = 0, n4 = 0;
        var ops = Record(() =>
        {
            var l = Viz.LinkedList<int>("ll");
            var a = l.AddLast(1);
            var b = l.AddLast(2);
            var c = l.AddFirst(0);
            var d = l.InsertAfter(a, 5);
            (n1, n2, n3, n4) = (Id(a), Id(b), Id(c), Id(d));
            l.SetNext(a, null);
            l.SetHead(b);
            b.Visit();
            b.SetValue(7);
            l.Remove(d);
            Check.Equal(1, l.Count);
        });
        Expect(ops[1], OpKind.Insert, node: n1, value: "1");
        Expect(ops[2], OpKind.Insert, node: n2, a: n1, value: "2");
        Expect(ops[3], OpKind.Insert, node: n3, value: "0");
        Expect(ops[4], OpKind.Insert, node: n4, a: n1, value: "5");
        Expect(ops[5], OpKind.Link, node: n1);
        Expect(ops[6], OpKind.Link, a: n2);
        Expect(ops[7], OpKind.Visit, node: n2);
        Expect(ops[8], OpKind.Write, node: n2, value: "7");
        Expect(ops[9], OpKind.Remove, node: n4);
    }

    private static void TreeOps()
    {
        int r = 0, x = 0, y = 0;
        var ops = Record(() =>
        {
            var t = Viz.Tree<string>("t");
            var root = t.SetRoot("r");
            var a = root.AddChild("x");
            var b = root.InsertChild(0, "y");
            (r, x, y) = (Id(root), Id(a), Id(b));
            a.SetValue("x2");
            b.MoveTo(a);
            a.Visit();
            a.Mark(VizColor.Path);
            a.Unmark();
            a.Remove();
            Check.Equal(1, t.Count);
            Check.Throws<InvalidOperationException>(() => b.Visit());
        });
        Expect(ops[1], OpKind.Insert, node: r, value: "r");
        Expect(ops[2], OpKind.Insert, node: x, a: r, b: 0, value: "x");
        Expect(ops[3], OpKind.Insert, node: y, a: r, b: 0, value: "y");
        Expect(ops[4], OpKind.Write, node: x, value: "x2");
        Expect(ops[5], OpKind.Move, node: y, a: x, b: 0);
        Expect(ops[6], OpKind.Visit, node: x);
        Expect(ops[7], OpKind.Mark, node: x, text: "path");
        Expect(ops[8], OpKind.Unmark, node: x);
        Expect(ops[9], OpKind.Remove, node: x);
        Check.Equal(10, ops.Count);
    }

    private static void BinaryTreeOps()
    {
        int r = 0, l = 0, rr = 0;
        var ops = Record(() =>
        {
            var t = Viz.BinaryTree<int>("b");
            var root = t.SetRoot(5);
            var left = root.SetLeft(3);
            var right = root.SetRight(8);
            (r, l, rr) = (Id(root), Id(left), Id(right));
            // Right rotation at the root: 3 becomes the root, 5 its right child.
            root.SetLeft(left.Right);
            left.SetRight(root);
            t.SetRoot(left);
            Check.True(t.Root == left && left.Right == root && root.Right == right && root.Parent == left);
            Check.Throws<InvalidOperationException>(() => root.SetLeft(left)); // would create a cycle
            root.RemoveRight();
            root.SetValue(6);
            left.Visit();
            left.Mark();
            left.Unmark();
            Check.Equal(2, t.Count);
        });
        Expect(ops[1], OpKind.Insert, node: r, value: "5");
        Expect(ops[2], OpKind.Insert, node: l, a: r, b: 0, value: "3");
        Expect(ops[3], OpKind.Insert, node: rr, a: r, b: 1, value: "8");
        Expect(ops[4], OpKind.Unlink, node: r, b: 0);
        Expect(ops[5], OpKind.Move, node: r, a: l, b: 1);
        Expect(ops[6], OpKind.Move, node: l);
        Expect(ops[7], OpKind.Remove, node: rr);
        Expect(ops[8], OpKind.Write, node: r, value: "6");
        Expect(ops[9], OpKind.Visit, node: l);
        Expect(ops[10], OpKind.Mark, node: l, text: "active");
        Expect(ops[11], OpKind.Unmark, node: l);
    }

    private static void GraphOps()
    {
        var ops = Record(() =>
        {
            var g = Viz.Graph("g", directed: true);
            g.AddNode(1, 10, 20);
            g.AddNode(2);
            g.AddEdge(1, 2, 2.5);
            g.AddEdge(2, 1);
            Check.True(g.Neighbors(1).SequenceEqual([2]));
            g.Visit(2);
            g.MarkNode(1, VizColor.Warn);
            g.UnmarkNode(1);
            g.MarkEdge(1, 2, VizColor.Path);
            g.UnmarkEdge(1, 2);
            Check.Equal(2.5, g.Weight(1, 2));
            g.RemoveEdge(2, 1);
            g.RemoveNode(2);
            Check.Equal(0, g.EdgeCount);
        });
        Expect(ops[0], OpKind.Create, a: (int)StructureKind.Graph, b: 1, text: "g");
        Expect(ops[1], OpKind.Insert, node: 1, a: 10, b: 20);
        Expect(ops[2], OpKind.Insert, node: 2);
        Expect(ops[3], OpKind.Link, a: 1, b: 2, value: "2.5");
        Expect(ops[4], OpKind.Link, a: 2, b: 1);
        Expect(ops[5], OpKind.Read, node: 1);
        Expect(ops[6], OpKind.Visit, node: 2);
        Expect(ops[7], OpKind.Mark, node: 1, text: "warn");
        Expect(ops[8], OpKind.Unmark, node: 1);
        Expect(ops[9], OpKind.Mark, a: 1, b: 2, text: "path");
        Expect(ops[10], OpKind.Unmark, a: 1, b: 2);
        Expect(ops[11], OpKind.Unlink, a: 2, b: 1);
        Expect(ops[12], OpKind.Remove, node: 2);
    }

    private static void GridOps()
    {
        var ops = Record(() =>
        {
            var m = Viz.Grid("m", 2, 3, 0);
            m.Set(1, 2, 7);
            Check.Equal(7, m.Get(1, 2));
            m.Mark(0, 1, VizColor.Muted);
            m.Unmark(0, 1);
            var c = Viz.CharGrid("c", "ab", "cd");
            Check.Equal('d', c.Get(1, 1));
        });
        Expect(ops[0], OpKind.Create, a: (int)StructureKind.Grid, b: 2, value: VizOp.Pack(Enumerable.Repeat("0", 6)), text: "m");
        Expect(ops[1], OpKind.Write, a: 1, b: 2, value: "7");
        Expect(ops[2], OpKind.Read, a: 1, b: 2, value: "7");
        Expect(ops[3], OpKind.Mark, a: 0, b: 1, text: "muted");
        Expect(ops[4], OpKind.Unmark, a: 0, b: 1);
        Expect(ops[5], OpKind.Create, a: (int)StructureKind.Grid, b: 2, value: VizOp.Pack(["a", "b", "c", "d"]), text: "c");
    }

    private static void MapSetOps()
    {
        var ops = Record(() =>
        {
            var m = Viz.Map<string, int>("m");
            m.Set("a", 1);
            m.Set("a", 2);
            Check.Equal(2, m.Get("a"));
            Check.True(m.ContainsKey("a"));
            Check.True(!m.ContainsKey("z"));
            Check.True(m.Remove("a"));
            Check.True(!m.Remove("a"));
            var s = Viz.Set<int>("s");
            Check.True(s.Add(4));
            Check.True(!s.Add(4));
            Check.True(s.Contains(4));
            Check.True(s.Remove(4));
            Check.True(!s.Contains(4));
        });
        var id = ops[1].NodeId;
        Check.True(id is not null);
        Expect(ops[1], OpKind.Write, node: id, value: "1", text: "a");
        Expect(ops[2], OpKind.Write, node: id, value: "2", text: "a");
        Expect(ops[3], OpKind.Read, node: id, value: "2", text: "a");
        Expect(ops[4], OpKind.Read, node: id, text: "a");
        Expect(ops[5], OpKind.Read, text: "z");
        Expect(ops[6], OpKind.Remove, node: id, text: "a");
        Expect(ops[7], OpKind.Remove, text: "a");
        var sid = ops[9].NodeId;
        Expect(ops[9], OpKind.Insert, node: sid, value: "4");
        Expect(ops[10], OpKind.Insert, node: sid, value: "4");
        Expect(ops[11], OpKind.Read, node: sid, value: "4");
        Expect(ops[12], OpKind.Remove, node: sid, value: "4");
        Expect(ops[13], OpKind.Read, value: "4");
    }

    private static void GlobalOps()
    {
        var ops = Record(() =>
        {
            Viz.Log("hello");
            Viz.Var("x", 3.5);
            Viz.Step("phase 1");
        });
        Expect(ops[0], OpKind.Log, text: "hello");
        Expect(ops[1], OpKind.Var, value: "3.5", text: "x");
        Expect(ops[2], OpKind.Step, text: "phase 1");
        Check.True(ops.All(o => o.StructureId == 0));

        Check.Equal("∅", Viz.Format(null));
        Check.Equal("\"\"", Viz.Format(""));
        Check.Equal("true", Viz.Format(true));
        Check.Equal("0.333", Viz.Format(1.0 / 3));
        Check.Equal("abcdefghijk…", Viz.Format("abcdefghijklmnop"));
        Check.Equal("[1,2,3]", Viz.Format(new List<int> { 1, 2, 3 }));
    }

    private static void SilentMembers()
    {
        var ops = Record(() =>
        {
            var a = Viz.Array("a", 1, 2);
            _ = a.Length + a.Count;
            _ = a.IsEmpty;
            var t = Viz.BinaryTree<int>("t");
            var root = t.SetRoot(1);
            root.SetLeft(0);
            _ = (t.Root, root.Value, root.Left, root.Right, root.Parent, root.IsLeaf, t.Count);
            var l = Viz.LinkedList<int>("l");
            l.AddLast(1);
            _ = (l.Head, l.Head!.Next, l.Head.Value, l.Count, l.Tail);
            var s = Viz.Stack<int>("s");
            _ = (s.Count, s.IsEmpty);
        });
        Check.Equal(7, ops.Count);
    }

    private static void InvalidOps()
    {
        var ops = Record(() =>
        {
            var a = Viz.Array("a", 1);
            Check.Throws<IndexOutOfRangeException>(() => a.Get(1));
            Check.Throws<InvalidOperationException>(() => Viz.Stack<int>("s").Pop());
            Check.Throws<InvalidOperationException>(() => Viz.Queue<int>("q").Peek());
            Check.Throws<KeyNotFoundException>(() => Viz.Graph("g").AddEdge(1, 2));
            Check.Throws<KeyNotFoundException>(() => Viz.Map<int, int>("m").Get(3));
            Check.Throws<VizLimitException>(() => Viz.Array<int>("big", 100_000));
        });
        Check.True(ops.All(o => o.Kind == OpKind.Create), "only the Create ops were recorded");
    }

    private static void MaxOps()
    {
        var recorder = new Recorder(maxOps: 10);
        using (Recorder.Begin(recorder))
        {
            var a = Viz.Array("a", 1);
            for (var i = 0; i < 9; i++) a.Get(0);
            Check.Throws<VizLimitException>(() => a.Get(0));
        }
        Check.Equal(10, recorder.Snapshot().Count);
    }

    private static int Id<T>(LinkedNode<T> node) => node.Id;

    private static int Id<T>(TreeNode<T> node) => node.Id;

    private static int Id<T>(BinaryNode<T> node) => node.Id;
}
