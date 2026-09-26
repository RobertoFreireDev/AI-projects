using System.Runtime.CompilerServices;

namespace AlgoViz.Api;

public sealed class VizGrid<T> : VizStructure
{
    private readonly T[,] cells;

    internal VizGrid(string label, T[,] cells, int line)
        : base(label, StructureKind.Grid, cells.GetLength(0), Pack(cells.Cast<T>()), line)
    {
        this.cells = cells;
    }

    public int Rows => cells.GetLength(0);

    public int Cols => cells.GetLength(1);

    public bool InBounds(int r, int c) => (uint)r < (uint)Rows && (uint)c < (uint)Cols;

    public T Get(int r, int c, [CallerLineNumber] int line = 0)
    {
        Check(r, c);
        var value = cells[r, c];
        Record(OpKind.Read, line, a: r, b: c, value: Viz.Format(value));
        return value;
    }

    public void Set(int r, int c, T value, [CallerLineNumber] int line = 0)
    {
        Check(r, c);
        cells[r, c] = value;
        Record(OpKind.Write, line, a: r, b: c, value: Viz.Format(value));
    }

    public void Mark(int r, int c, VizColor color = VizColor.Active, [CallerLineNumber] int line = 0)
    {
        Check(r, c);
        Record(OpKind.Mark, line, a: r, b: c, text: color.Name());
    }

    public void Unmark(int r, int c, [CallerLineNumber] int line = 0)
    {
        Check(r, c);
        Record(OpKind.Unmark, line, a: r, b: c);
    }

    private void Check(int r, int c)
    {
        if (!InBounds(r, c))
            throw new IndexOutOfRangeException($"Cell ({r}, {c}) is outside '{Label}' ({Rows}×{Cols}).");
    }
}
