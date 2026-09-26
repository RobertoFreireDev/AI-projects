using System.Globalization;

namespace SqliteViz.Core.Schema;

public sealed record ErColumn(string Name, string Type, bool PrimaryKey, bool ForeignKey);

public sealed record ErBox(string Table, bool IsVirtual, int Layer, double X, double Y, double Width, double Height, IReadOnlyList<ErColumn> Columns)
{
    public double RowY(int index) => Y + ErLayout.HeaderHeight + index * ErLayout.RowHeight + ErLayout.RowHeight / 2;
}

/// <param name="Path">SVG path data.</param>
/// <param name="FromX">Child end (the table holding the foreign key).</param>
/// <param name="FromDirection">+1 when the line leaves the child box to the right, -1 to the left.</param>
/// <param name="ToX">Parent end (the referenced table).</param>
/// <param name="OneToOne">The FK columns are also unique in the child, so the child end is "one" instead of "many".</param>
public sealed record ErEdge(
    string FromTable, string ToTable, string Label, string Path,
    double FromX, double FromY, int FromDirection,
    double ToX, double ToY, int ToDirection,
    bool OneToOne, bool Optional);

public sealed record ErDiagram(IReadOnlyList<ErBox> Boxes, IReadOnlyList<ErEdge> Edges, double Width, double Height)
{
    public static readonly ErDiagram Empty = new([], [], 0, 0);
}

/// <summary>
/// Pure layout for the ER diagram: tables are layered by foreign-key depth (referenced tables on the left),
/// and tables in each layer are ordered by the barycenter of their neighbours to reduce crossings.
/// </summary>
public static class ErLayout
{
    public const double HeaderHeight = 26;
    public const double RowHeight = 19;
    public const double CharWidth = 7.4;
    public const double Padding = 24;
    public const double LayerGap = 110;
    public const double BoxGap = 30;
    public const double MinWidth = 150;

    public static ErDiagram Compute(SchemaSnapshot schema)
    {
        var tables = schema.Tables.Where(t => t.Schema == "main").ToList();
        if (tables.Count == 0) return ErDiagram.Empty;
        var byName = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        // Parents (referenced tables) of each table, ignoring self references and unknown tables.
        var parents = tables.ToDictionary(t => t.Name, t => t.ForeignKeys
            .Select(fk => byName.TryGetValue(fk.ToTable, out var p) ? p.Name : null)
            .OfType<string>().Where(p => !string.Equals(p, t.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct().ToList(), StringComparer.OrdinalIgnoreCase);
        var children = tables.ToDictionary(t => t.Name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (child, ps) in parents)
            foreach (var p in ps) children[p].Add(child);

        var layerOf = AssignLayers(tables.Select(t => t.Name).ToList(), parents);
        // Tables with no relationships go last in their layer so they do not push related tables apart.
        bool Isolated(string n) => parents[n].Count == 0 && children[n].Count == 0;
        var layers = layerOf.GroupBy(kv => kv.Value).OrderBy(g => g.Key)
            .Select(g => g.Select(kv => kv.Key).OrderBy(Isolated).ThenBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()).ToList();
        OrderLayers(layers, parents, children);

        // Place boxes.
        var boxes = new Dictionary<string, ErBox>(StringComparer.OrdinalIgnoreCase);
        var x = Padding;
        var height = 0.0;
        for (var l = 0; l < layers.Count; l++)
        {
            var layerBoxes = layers[l].Select(n => MakeColumns(byName[n])).ToList();
            var width = layerBoxes.Max(b => BoxWidth(b.Table.Name, b.Columns));
            var y = Padding;
            foreach (var (table, columns) in layerBoxes)
            {
                var h = HeaderHeight + Math.Max(1, columns.Count) * RowHeight + 4;
                boxes[table.Name] = new ErBox(table.Name, table.IsVirtual, l, x, y, width, h, columns);
                y += h + BoxGap;
            }
            height = Math.Max(height, y - BoxGap + Padding);
            x += width + LayerGap;
        }
        var totalWidth = x - LayerGap + Padding;

        var edges = new List<ErEdge>();
        foreach (var table in tables)
        foreach (var fk in table.ForeignKeys)
        {
            if (!byName.TryGetValue(fk.ToTable, out var parent)) continue;
            edges.Add(MakeEdge(boxes[table.Name], table, boxes[parent.Name], parent, fk));
        }

        return new ErDiagram(layers.SelectMany(l => l).Select(n => boxes[n]).ToList(), edges, totalWidth, Math.Max(height, 100));
    }

    /// <summary>Layer = 1 + deepest parent layer. Cycles are cut where the DFS finds a table already on the stack.</summary>
    internal static Dictionary<string, int> AssignLayers(List<string> names, Dictionary<string, List<string>> parents)
    {
        var layer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int Visit(string n)
        {
            if (layer.TryGetValue(n, out var known)) return known;
            if (!onStack.Add(n)) return -1;
            var depth = 0;
            foreach (var p in parents[n])
            {
                var pl = Visit(p);
                if (pl >= 0) depth = Math.Max(depth, pl + 1);
            }
            onStack.Remove(n);
            layer[n] = depth;
            return depth;
        }

        foreach (var n in names) Visit(n);
        return layer;
    }

    private static void OrderLayers(List<List<string>> layers, Dictionary<string, List<string>> parents, Dictionary<string, List<string>> children)
    {
        bool Isolated(string n) => parents[n].Count == 0 && children[n].Count == 0;
        var position = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        void Index()
        {
            foreach (var layer in layers)
                for (var i = 0; i < layer.Count; i++) position[layer[i]] = i;
        }
        Index();

        void Sort(int l, Func<string, IEnumerable<string>> neighbours)
        {
            var current = layers[l];
            var keyed = current.Select((n, i) =>
            {
                var ns = neighbours(n).Where(position.ContainsKey).ToList();
                return (Name: n, Key: ns.Count > 0 ? ns.Average(m => position[m]) : i + (Isolated(n) ? current.Count : 0));
            }).ToList();
            layers[l] = keyed.OrderBy(k => k.Key).Select(k => k.Name).ToList();
            for (var i = 0; i < layers[l].Count; i++) position[layers[l][i]] = i;
        }

        for (var sweep = 0; sweep < 4; sweep++)
        {
            for (var l = 1; l < layers.Count; l++) Sort(l, n => parents[n]);
            for (var l = layers.Count - 2; l >= 0; l--) Sort(l, n => children[n]);
        }
    }

    private static (TableInfo Table, List<ErColumn> Columns) MakeColumns(TableInfo table)
    {
        var fkColumns = new HashSet<string>(table.ForeignKeys.SelectMany(f => f.FromColumns), StringComparer.OrdinalIgnoreCase);
        var columns = table.Columns
            .Where(c => c.Hidden != 1)
            .Select(c => new ErColumn(c.Name, c.DeclaredType, c.IsPrimaryKey, fkColumns.Contains(c.Name)))
            .ToList();
        return (table, columns);
    }

    private static double BoxWidth(string name, List<ErColumn> columns)
    {
        var longest = Math.Max(name.Length + 2, columns.Select(c => c.Name.Length + c.Type.Length + 6).DefaultIfEmpty(0).Max());
        return Math.Max(MinWidth, Math.Ceiling(longest * CharWidth + 20));
    }

    private static ErEdge MakeEdge(ErBox childBox, TableInfo child, ErBox parentBox, TableInfo parent, ForeignKeyInfo fk)
    {
        var fromRow = Math.Max(0, IndexOf(childBox, fk.FromColumns[0]));
        var toRow = Math.Max(0, IndexOf(parentBox, fk.ToColumns.FirstOrDefault() ?? ""));
        var fy = childBox.RowY(fromRow);
        var ty = parentBox.RowY(toRow);
        var oneToOne = IsUnique(child, fk.FromColumns);
        var optional = fk.FromColumns.Any(c => child.Columns.FirstOrDefault(x => x.Name == c) is { NotNull: false, IsPrimaryKey: false });
        var label = $"{child.Name}({string.Join(", ", fk.FromColumns)}) → {parent.Name}({string.Join(", ", fk.ToColumns)})"
            + $" ON DELETE {fk.OnDelete}";

        double fx, tx;
        int fd, td;
        string path;
        if (ReferenceEquals(childBox, parentBox))
        {
            // Self reference: a loop on the right side.
            fx = tx = childBox.X + childBox.Width;
            fd = td = 1;
            var loop = 34 + Math.Abs(fy - ty) * 0.2;
            path = $"M {F(fx)} {F(fy)} C {F(fx + loop)} {F(fy)}, {F(tx + loop)} {F(ty)}, {F(tx)} {F(ty)}";
        }
        else
        {
            if (parentBox.X + parentBox.Width <= childBox.X) { fx = childBox.X; fd = -1; tx = parentBox.X + parentBox.Width; td = 1; }
            else if (parentBox.X >= childBox.X + childBox.Width) { fx = childBox.X + childBox.Width; fd = 1; tx = parentBox.X; td = -1; }
            else { fx = childBox.X + childBox.Width; fd = 1; tx = parentBox.X + parentBox.Width; td = 1; }
            var reach = fd == td ? 40 + Math.Abs(fy - ty) * 0.15 : Math.Max(40, Math.Abs(tx - fx) / 2);
            path = $"M {F(fx)} {F(fy)} C {F(fx + fd * reach)} {F(fy)}, {F(tx + td * reach)} {F(ty)}, {F(tx)} {F(ty)}";
        }
        return new ErEdge(child.Name, parent.Name, label, path, fx, fy, fd, tx, ty, td, oneToOne, optional);
    }

    private static int IndexOf(ErBox box, string column)
    {
        for (var i = 0; i < box.Columns.Count; i++)
            if (string.Equals(box.Columns[i].Name, column, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>True when the FK columns are the child's whole primary key or exactly a unique index.</summary>
    private static bool IsUnique(TableInfo table, IReadOnlyList<string> columns)
    {
        var set = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        var pk = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        if (pk.Count == set.Count && pk.All(set.Contains)) return true;
        return table.Indexes.Any(ix => ix.Unique && !ix.Partial && ix.Columns.Count == set.Count && ix.Columns.All(set.Contains));
    }

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
}
