using SqliteViz.Core.Runner;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

internal static class Helpers
{
    public static RunResult Run(this DbSession session, string sql) => SqlRunner.RunAsync(session, sql).GetAwaiter().GetResult();

    /// <summary>Runs and asserts there was no error.</summary>
    public static RunResult RunOk(this DbSession session, string sql)
    {
        var result = session.Run(sql);
        if (result.Error is { } e) throw new AssertionException($"unexpected error at line {e.Line}: {e.Message}\n  in: {e.Sql}");
        return result;
    }

    /// <summary>Runs and returns the error, asserting there was one.</summary>
    public static SqlError RunError(this DbSession session, string sql) =>
        session.Run(sql).Error ?? throw new AssertionException($"expected an error from: {sql}");

    /// <summary>The single value of the last statement.</summary>
    public static string Scalar(this DbSession session, string sql) => session.RunOk(sql).Statements[^1].Rows[0].Cells[0].Text;
}
