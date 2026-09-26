using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class SessionTests
{
    [Test]
    public static void TwoSessionsAreIsolated()
    {
        using var a = new DbSession();
        using var b = new DbSession();
        a.RunOk("DELETE FROM product_tags; CREATE TABLE only_in_a (x);");
        Assert.Equal("0", a.Scalar("SELECT count(*) FROM product_tags;"));
        Assert.Equal("19", b.Scalar("SELECT count(*) FROM product_tags;"));
        Assert.Contains("no such table", b.RunError("SELECT * FROM only_in_a;").Message);
    }

    [Test]
    public static async Task ResetRestoresSeed()
    {
        using var s = new DbSession();
        var before = s.RunOk("SELECT 1;").Schema;
        s.RunOk("BEGIN; DROP TABLE shipment_items; DELETE FROM order_items; CREATE TABLE junk (x);");
        Assert.True(s.InTransaction);
        await s.ResetAsync();
        Assert.False(s.InTransaction);
        var after = s.RunOk("SELECT 1;").Schema;
        Assert.Equal(string.Join(",", before.Tables.Select(t => $"{t.Name}:{t.RowCount}")),
            string.Join(",", after.Tables.Select(t => $"{t.Name}:{t.RowCount}")));
        Assert.Equal("29", s.Scalar("SELECT count(*) FROM order_items;"));
    }

    [Test]
    public static void SeedIsIdempotent()
    {
        using var s = new DbSession();
        s.RunOk(Core.Samples.SampleCatalog.Seed.Sql);
        s.RunOk(Core.Samples.SampleCatalog.Seed.Sql);
        Assert.Equal("18", s.Scalar("SELECT count(*) FROM orders;"));
    }
}
