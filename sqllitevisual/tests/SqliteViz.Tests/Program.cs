using SqliteViz.Core.Samples;
using SqliteViz.Core.Session;
using SqliteViz.Tests;

// "--dump <sample-id>" prints every result of a sample on a fresh database (for checking sample comments).
if (args is ["--dump", var id])
{
    using var session = new DbSession();
    var result = session.Run(SampleCatalog.Get(id).Sql);
    foreach (var st in result.Statements)
    {
        Console.WriteLine($"-- line {st.Line} [{st.Kind}] rows={st.TotalRows} affected={st.RowsAffected}");
        if (st.Columns.Count > 0) Console.WriteLine("   " + string.Join(" | ", st.Columns.Select(c => c.Name)));
        foreach (var row in st.Rows.Take(8)) Console.WriteLine("   " + string.Join(" | ", row.Cells.Select(c => c.Text)));
    }
    Console.WriteLine(result.Error is { } e ? $"ERROR line {e.Line}: {e.Message}" : "OK");
    return 0;
}

return TestRunner.Run(args.FirstOrDefault());
