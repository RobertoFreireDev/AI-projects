using AlgoViz.Api;

namespace AlgoViz.Core.Visual;

public readonly record struct Point(double X, double Y);

public sealed record TreeLayoutNode(int Id, Point Position, bool Detached);

public sealed record TreeLayoutEdge(int Parent, int Child);

/// <summary>Positions in slot units: x is the leaf slot, y is the depth. Views multiply by their spacing.</summary>
public sealed record TreeLayoutResult(IReadOnlyList<TreeLayoutNode> Nodes, IReadOnlyList<TreeLayoutEdge> Edges, double Width, double Height);

/// <summary>Pure layout functions shared by the views.</summary>
public static class Layout
{
    /// <summary>
    /// Simple tidy layout: leaves get consecutive x slots, parents are centered over their children, y is the depth.
    /// In a binary tree an empty slot next to a real child takes a slot, so left/right stays visible.
    /// Several roots (e.g. detached subtrees) are placed side by side with a one-slot gap.
    /// </summary>
    public static TreeLayoutResult Tree(IEnumerable<(int Root, bool Detached)> roots, Func<int, IReadOnlyList<int?>> children, bool binary)
    {
        var nodes = new List<TreeLayoutNode>();
        var edges = new List<TreeLayoutEdge>();
        var nextSlot = 0.0;
        var maxDepth = -1;
        var visited = new HashSet<int>();

        double Place(int id, int depth, bool detached)
        {
            visited.Add(id);
            maxDepth = Math.Max(maxDepth, depth);
            var kids = children(id);
            var hasChild = kids.Any(k => k is { } c && !visited.Contains(c));
            double x;
            if (!hasChild) x = nextSlot++;
            else
            {
                var xs = new List<double>();
                foreach (var kid in kids)
                {
                    if (kid is { } c && !visited.Contains(c))
                    {
                        edges.Add(new TreeLayoutEdge(id, c));
                        xs.Add(Place(c, depth + 1, detached));
                    }
                    else if (binary) xs.Add(nextSlot++);
                }
                x = (xs[0] + xs[^1]) / 2;
            }
            nodes.Add(new TreeLayoutNode(id, new Point(x, depth), detached));
            return x;
        }

        var first = true;
        foreach (var (root, detached) in roots)
        {
            if (visited.Contains(root)) continue;
            if (!first) nextSlot += 1;
            first = false;
            Place(root, 0, detached);
        }
        return new TreeLayoutResult(nodes, edges, nextSlot, maxDepth + 1);
    }

    public static TreeLayoutResult Tree(TreeState state)
    {
        var roots = new List<(int, bool)>();
        if (state.Root is { } root) roots.Add((root, false));
        roots.AddRange(state.Detached.Select(d => (d, true)));
        return Tree(roots, id => state.Nodes.TryGetValue(id, out var n) ? n.Children : [], state.Binary);
    }

    /// <summary>Items as a complete binary tree: index i has children 2i+1 and 2i+2 (heap view). Ids are indices.</summary>
    public static TreeLayoutResult Heap(int count) =>
        count == 0
            ? new TreeLayoutResult([], [], 0, 0)
            : Tree([(0, false)], i => [2 * i + 1 < count ? 2 * i + 1 : null, 2 * i + 2 < count ? 2 * i + 2 : null], binary: true);

    /// <summary>
    /// Graph positions inside a width × height box: the user's x/y (scaled to fit) when every node has them,
    /// otherwise a circle in insertion order.
    /// </summary>
    public static IReadOnlyDictionary<int, Point> Graph(GraphState state, double width, double height, double padding)
    {
        var nodes = state.Nodes;
        var result = new Dictionary<int, Point>();
        if (nodes.Count == 0) return result;
        var innerW = width - 2 * padding;
        var innerH = height - 2 * padding;

        if (nodes.All(n => n.X is not null && n.Y is not null))
        {
            double minX = nodes.Min(n => n.X!.Value), maxX = nodes.Max(n => n.X!.Value);
            double minY = nodes.Min(n => n.Y!.Value), maxY = nodes.Max(n => n.Y!.Value);
            var spanX = Math.Max(maxX - minX, 1);
            var spanY = Math.Max(maxY - minY, 1);
            var scale = Math.Min(innerW / spanX, innerH / spanY);
            var offsetX = padding + (innerW - spanX * scale) / 2;
            var offsetY = padding + (innerH - spanY * scale) / 2;
            foreach (var n in nodes)
                result[n.Id] = new Point(offsetX + (n.X!.Value - minX) * scale, offsetY + (n.Y!.Value - minY) * scale);
            return result;
        }

        var radius = Math.Min(innerW, innerH) / 2;
        var center = new Point(width / 2, height / 2);
        for (var i = 0; i < nodes.Count; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / nodes.Count;
            result[nodes[i].Id] = nodes.Count == 1
                ? center
                : new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
        }
        return result;
    }

    /// <summary>Slot of each node in a linked list's display order (horizontal, left to right).</summary>
    public static IReadOnlyDictionary<int, int> LinkedList(LinkedListState state) =>
        state.Order.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);

    /// <summary>Stack slots grow upwards: the top element is row 0.</summary>
    public static int StackRow(int index, int count) => count - 1 - index;

    /// <summary>Set members wrap into rows of <paramref name="perRow"/>.</summary>
    public static (int Row, int Col) Wrap(int index, int perRow) => (index / perRow, index % perRow);

    public static bool IsSequence(StructureKind kind) =>
        kind is StructureKind.Array or StructureKind.List or StructureKind.Stack or StructureKind.Queue or StructureKind.Deque;
}
