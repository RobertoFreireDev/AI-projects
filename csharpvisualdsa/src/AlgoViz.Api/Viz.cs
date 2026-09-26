using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

/// <summary>Entry point of the Viz API: factories for every structure plus global log/watch/step ops.</summary>
public static class Viz
{
    private const int MaxValueLength = 12;
    private const int MaxLogLength = 200;

    public static VizArray<T> Array<T>(string label, params T[] values) =>
        new(label, [.. values], Recorder.CallerLine());

    public static VizArray<T> Array<T>(string label, int size)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), "Size can't be negative.");
        VizStructure.CheckSize(size);
        return new(label, [.. Enumerable.Repeat(default(T)!, size)], Recorder.CallerLine());
    }

    public static VizList<T> List<T>(string label, params T[] values) =>
        new(label, [.. values], Recorder.CallerLine());

    public static VizStack<T> Stack<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizQueue<T> Queue<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizDeque<T> Deque<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizLinkedList<T> LinkedList<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizTree<T> Tree<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizBinaryTree<T> BinaryTree<T>(string label, [CallerLineNumber] int line = 0) => new(label, line);

    public static VizGraph Graph(string label, bool directed = false, [CallerLineNumber] int line = 0) =>
        new(label, directed, line);

    public static VizGrid<T> Grid<T>(string label, int rows, int cols, T fill = default!, [CallerLineNumber] int line = 0)
    {
        if (rows < 1 || cols < 1) throw new ArgumentOutOfRangeException(nameof(rows), "A grid needs at least one row and one column.");
        VizStructure.CheckSize(rows * cols);
        var cells = new T[rows, cols];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                cells[r, c] = fill;
        return new(label, cells, line);
    }

    /// <summary>A grid from jagged rows; every row must have the same length.</summary>
    public static VizGrid<T> Grid<T>(string label, T[][] rows, [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length == 0 || rows[0].Length == 0) throw new ArgumentException("A grid needs at least one row and one column.", nameof(rows));
        var cols = rows[0].Length;
        if (rows.Any(r => r.Length != cols)) throw new ArgumentException("Every row must have the same length.", nameof(rows));
        VizStructure.CheckSize(rows.Length * cols);
        var cells = new T[rows.Length, cols];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < cols; c++)
                cells[r, c] = rows[r][c];
        return new(label, cells, line);
    }

    /// <summary>A character grid from strings, e.g. a maze: <c>Viz.CharGrid("m", "..#", "#..")</c>.</summary>
    public static VizGrid<char> CharGrid(string label, params string[] rows) =>
        Grid(label, rows.Select(r => r.ToCharArray()).ToArray(), Recorder.CallerLine());

    public static VizMap<TKey, TValue> Map<TKey, TValue>(string label, [CallerLineNumber] int line = 0) where TKey : notnull =>
        new(label, line);

    public static VizSet<T> Set<T>(string label, [CallerLineNumber] int line = 0) where T : notnull => new(label, line);

    public static void Log(object? message, [CallerLineNumber] int line = 0)
    {
        var text = message switch
        {
            null => "∅",
            string s => s,
            _ => Format(message, int.MaxValue),
        };
        if (text.Length > MaxLogLength) text = text[..(MaxLogLength - 1)] + "…";
        Recorder.Current.Append(OpKind.Log, 0, null, null, null, null, text, line);
    }

    /// <summary>Shows <paramref name="value"/> in the watch panel under <paramref name="name"/>.</summary>
    public static void Var(string name, object? value, [CallerLineNumber] int line = 0) =>
        Recorder.Current.Append(OpKind.Var, 0, null, null, null, Format(value), name, line);

    /// <summary>A named checkpoint shown as a marker on the timeline.</summary>
    public static void Step(string label, [CallerLineNumber] int line = 0) =>
        Recorder.Current.Append(OpKind.Step, 0, null, null, null, null, label, line);

    /// <summary>The display string for a value: <c>ToString()</c>, truncated to 12 chars, <c>null</c> → ∅.</summary>
    public static string Format(object? value) => Format(value, MaxValueLength);

    private static string Format(object? value, int maxLength)
    {
        var text = value switch
        {
            null => "∅",
            string s => s.Length == 0 ? "\"\"" : s,
            bool b => b ? "true" : "false",
            double d => FormatNumber(d),
            float f => FormatNumber(f),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable e => "[" + string.Join(",", e.Cast<object?>().Take(8).Select(v => Format(v))) + "]",
            _ => value.ToString() ?? "∅",
        };
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }

    private static string FormatNumber(double d) => d switch
    {
        double.PositiveInfinity => "∞",
        double.NegativeInfinity => "-∞",
        _ => d.ToString("0.###", CultureInfo.InvariantCulture),
    };

    /// <summary>Guard injected at the start of every method, local function and lambda. Not for scripts.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void __Enter() => Recorder.Active?.Enter();

    /// <summary>Guard injected at the start of every loop body. Not for scripts.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void __Tick() => Recorder.Active?.Tick();
}
