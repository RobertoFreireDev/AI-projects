using System.Diagnostics;
using SqliteViz.Core.Plan;
using SqliteViz.Core.Runner;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class RunnerTests
{
    [Test]
    public static void TimeoutStopsInfiniteRecursiveCte()
    {
        using var s = new DbSession();
        var sw = Stopwatch.StartNew();
        var r = s.Run("SELECT 1;\nWITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n) SELECT count(*) FROM n;");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
        var e = Assert.NotNull(r.Error);
        Assert.Equal(SqlRunner.TimeoutMessage, e.Message);
        Assert.Equal(1, r.Statements.Count);
        Assert.Equal(2, e.Line);
        // The session is still usable afterwards.
        Assert.Equal("5", s.Scalar("SELECT count(*) FROM departments;"));
    }

    [Test]
    public static void RowsAreTruncatedAndCounted()
    {
        using var s = new DbSession();
        var st = s.RunOk("WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n LIMIT 5000) SELECT i FROM n;").Statements[0];
        Assert.Equal(RunLimits.MaxKeptRows, st.Rows.Count);
        Assert.Equal(5000, st.TotalRows);
        Assert.True(st.Truncated);
        Assert.False(st.TotalCapped);

        var capped = s.RunOk("WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n) SELECT i FROM n;").Statements[0];
        Assert.Equal(RunLimits.MaxCountedRows, capped.TotalRows);
        Assert.True(capped.TotalCapped);
    }

    [Test]
    public static void StopsAtFirstErrorAndKeepsEarlierResults()
    {
        using var s = new DbSession();
        var r = s.Run("SELECT 1;\nSELECT 2;\nSELECT * FROM nope;\nSELECT 4;");
        Assert.Equal(2, r.Statements.Count);
        var e = Assert.NotNull(r.Error);
        Assert.Equal(2, e.StatementIndex);
        Assert.Contains("no such table: nope", e.Message);
        Assert.Equal(3, e.Line);
    }

    [Test]
    public static void ErrorLineAndColumnMapping()
    {
        using var s = new DbSession();
        var e = s.RunError("SELECT 1;\n\n-- comment\nSELECT name,\n       no_such_column\nFROM customers;");
        Assert.Equal(5, e.Line);
        Assert.Equal(8, e.Column);
        Assert.Equal(4, s.RunError("SELECT 1;\nSELECT 2;\n\nSELEC 3;").Line);
    }

    [Test]
    public static void ErrorLineWithBaseLine()
    {
        using var s = new DbSession();
        var r = SqlRunner.RunAsync(s, "SELECT\n  bogus;", baseLine: 20, baseColumn: 3).GetAwaiter().GetResult();
        var e = Assert.NotNull(r.Error);
        Assert.Equal(21, e.Line);
        Assert.Equal(3, e.Column);
    }

    [Test]
    public static void RuntimeErrorsAreReported()
    {
        using var s = new DbSession();
        var e = s.RunError("DELETE FROM orders WHERE order_id = 1;");
        Assert.Contains("Shipped orders cannot be deleted", e.Message);
        Assert.Contains("FOREIGN KEY constraint failed", s.RunError("DELETE FROM customers WHERE customer_id = 1;").Message);
        Assert.Contains("CHECK constraint failed", s.RunError("UPDATE orders SET status = 'lost' WHERE order_id = 4;").Message);
        Assert.Contains("cannot store TEXT value in INTEGER column", s.RunError("INSERT INTO events VALUES (99, 'yesterday', 'x', NULL, '{}');").Message);
    }

    [Test]
    public static void StorageClassesAndDisplay()
    {
        using var s = new DbSession();
        var row = s.RunOk("SELECT NULL, 1, 1.0, 2.5, 'text', x'0a1b', 1e300 * 1e300, zeroblob(40), printf('%.*c', 300, 'x');").Statements[0].Rows[0].Cells;
        Assert.Equal(StorageClass.Null, row[0].Class);
        Assert.Equal("NULL", row[0].Text);
        Assert.Equal(new ResultCell(StorageClass.Integer, "1"), row[1]);
        Assert.Equal(new ResultCell(StorageClass.Real, "1.0"), row[2]);
        Assert.Equal("2.5", row[3].Text);
        Assert.Equal(StorageClass.Text, row[4].Class);
        Assert.Equal("x'0A1B' (2 bytes)", row[5].Text);
        Assert.Equal("Inf", row[6].Text);
        Assert.Equal("x'00000000000000000000000000000000…' (40 bytes)", row[7].Text);
        Assert.True(row[8].Truncated);
        Assert.Equal(RunLimits.MaxDisplayChars + 1, row[8].Text.Length);
    }

    [Test]
    public static void KindsAndRowsAffected()
    {
        using var s = new DbSession();
        var r = s.RunOk("""
            SELECT 1;
            UPDATE products SET stock = stock + 1 WHERE category_id = 4;
            WITH x AS (SELECT 1) DELETE FROM tags WHERE tag_id = 6;
            CREATE TABLE t (x);
            PRAGMA user_version;
            BEGIN; COMMIT;
            EXPLAIN QUERY PLAN SELECT * FROM orders WHERE customer_id = 1;
            INSERT INTO t VALUES (1), (2) RETURNING x;
            """);
        var kinds = r.Statements.Select(x => x.Kind).ToList();
        Assert.Equal("Query,Dml,Dml,Ddl,Pragma,Transaction,Transaction,QueryPlan,Dml", string.Join(",", kinds));
        Assert.Equal(3L, r.Statements[1].RowsAffected);
        Assert.Equal(1L, r.Statements[2].RowsAffected);
        Assert.Null(r.Statements[0].RowsAffected);
        Assert.Equal(2L, r.Statements[8].RowsAffected);
        Assert.Equal(2, r.Statements[8].Rows.Count);
    }

    [Test]
    public static void QueryPlanTree()
    {
        using var s = new DbSession();
        var plan = Assert.NotNull(s.RunOk("""
            EXPLAIN QUERY PLAN
            SELECT c.name, (SELECT count(*) FROM orders o WHERE o.customer_id = c.customer_id)
            FROM customers c ORDER BY c.city;
            """).Statements[0].Plan);
        var all = Flatten(plan).ToList();
        Assert.True(all.Any(n => n.Detail.StartsWith("SCAN c") && n.Tag == PlanTag.Warn), "scan");
        Assert.True(all.Any(n => n.Detail.Contains("COVERING INDEX") && n.Tag == PlanTag.Best), "covering");
        Assert.True(all.Any(n => n.Detail.Contains("TEMP B-TREE") && n.Tag == PlanTag.Warn), "temp b-tree");
        Assert.True(plan.Children.Any(n => n.Children.Count > 0), "nested");

        Assert.Equal(PlanTag.Good, QueryPlanParser.Classify("SEARCH o USING INDEX idx_orders_customer_date (customer_id=?)"));
        Assert.Equal(PlanTag.Warn, QueryPlanParser.Classify("SEARCH t USING AUTOMATIC COVERING INDEX (x=?)"));
    }

    [Test]
    public static void TransactionStaysOpen()
    {
        using var s = new DbSession();
        Assert.True(s.RunOk("BEGIN; UPDATE tags SET name = 'x' || name;").InTransaction);
        Assert.True(s.RunOk("SELECT 1;").InTransaction);
        Assert.False(s.RunOk("ROLLBACK;").InTransaction);
    }

    [Test]
    public static void TooManyStatements()
    {
        using var s = new DbSession();
        var r = s.Run(string.Concat(Enumerable.Repeat("SELECT 1;\n", RunLimits.MaxStatements + 1)));
        Assert.Equal(0, r.Statements.Count);
        Assert.Contains("limit is", Assert.NotNull(r.Error).Message);
    }

    [Test]
    public static void SqlLengthLimit()
    {
        using var s = new DbSession();
        var e = s.RunError("SELECT '" + new string('x', RunLimits.MaxSqlLength) + "';");
        Assert.Contains("too", e.Message.ToLowerInvariant());
    }

    [Test]
    public static void ConcurrentRunsAreSerialized()
    {
        using var s = new DbSession();
        var tasks = Enumerable.Range(0, 8).Select(i => SqlRunner.RunAsync(s, $"INSERT INTO tags (name) VALUES ('t{i}'); SELECT count(*) FROM tags;")).ToArray();
        Task.WaitAll(tasks);
        Assert.True(tasks.All(t => t.Result.Error is null));
        Assert.Equal("16", s.Scalar("SELECT count(*) FROM tags;"));
    }

    private static IEnumerable<PlanNode> Flatten(PlanNode node) => node.Children.SelectMany(Flatten).Prepend(node);
}
