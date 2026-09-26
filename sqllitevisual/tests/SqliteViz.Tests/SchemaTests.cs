using SqliteViz.Core.Schema;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class SchemaTests
{
    [Test]
    public static void SnapshotDescribesSeed()
    {
        using var s = new DbSession();
        var schema = s.Schema;
        Assert.Equal(15, schema.Tables.Count);
        Assert.Equal("v_employee_tree,v_order_totals", string.Join(",", schema.Views.Select(v => v.Name)));
        Assert.Equal(3, schema.Triggers.Count);

        var events = Assert.NotNull(schema.FindTable("events"));
        Assert.True(events.Strict);
        Assert.True(Assert.NotNull(schema.FindTable("routes")).WithoutRowId);

        var items = Assert.NotNull(schema.FindTable("order_items"));
        Assert.Equal("VIRTUAL", items.Columns.Single(c => c.Name == "line_total").GeneratedKind);
        Assert.Equal(2, items.Columns.Count(c => c.IsPrimaryKey));
        Assert.Equal(29L, items.RowCount);
        Assert.Equal("STORED", Assert.NotNull(schema.FindTable("products")).Columns.Single(c => c.Name == "price_with_tax").GeneratedKind);

        var ship = Assert.NotNull(schema.FindTable("shipment_items")).ForeignKeys.Single();
        Assert.Equal("order_id,product_id", string.Join(",", ship.FromColumns));
        Assert.Equal("NO ACTION", ship.OnDelete);

        var customers = Assert.NotNull(schema.FindTable("customers"));
        var email = customers.Indexes.Single(i => i.Name == "ux_customers_email");
        Assert.True(email.Unique);
        Assert.Equal("<expr>", email.Columns.Single());
        Assert.True(Assert.NotNull(schema.FindTable("orders")).Indexes.Single(i => i.Name == "idx_orders_open").Partial);

        // Implicit "REFERENCES departments" resolves to the parent's primary key.
        var deptFk = Assert.NotNull(schema.FindTable("employees")).ForeignKeys.Single(f => f.ToTable == "departments");
        Assert.Equal("dept_id", deptFk.ToColumns.Single());
    }

    [Test]
    public static void SnapshotRefreshesAfterRun()
    {
        using var s = new DbSession();
        var r = s.RunOk("CREATE TABLE \"odd \"\"name\" (a INTEGER PRIMARY KEY); INSERT INTO \"odd \"\"name\" VALUES (1), (2);");
        var t = Assert.NotNull(r.Schema.FindTable("odd \"name"));
        Assert.Equal(2L, t.RowCount);
        Assert.True(s.Schema.Words().Any(w => w.Value == "odd \"name"));
    }

    [Test]
    public static void ErLayoutLayersByForeignKeyDepth()
    {
        using var s = new DbSession();
        var diagram = ErLayout.Compute(s.Schema);
        Assert.Equal(15, diagram.Boxes.Count);
        var box = diagram.Boxes.ToDictionary(b => b.Table);
        Assert.Equal(0, box["customers"].Layer);
        Assert.True(box["orders"].Layer > box["customers"].Layer, "orders right of customers");
        Assert.True(box["order_items"].Layer > box["orders"].Layer, "order_items right of orders");
        Assert.True(box["shipment_items"].Layer > box["order_items"].Layer, "shipment_items right of order_items");

        var fkCount = s.Schema.Tables.Sum(t => t.ForeignKeys.Count);
        Assert.Equal(fkCount, diagram.Edges.Count);
        Assert.True(diagram.Edges.Single(e => e.FromTable == "customer_profiles").OneToOne, "profile is one-to-one");
        Assert.False(diagram.Edges.Single(e => e.FromTable == "orders").OneToOne, "orders is one-to-many");
        Assert.True(diagram.Edges.Any(e => e.FromTable == "employees" && e.ToTable == "employees"), "self reference");

        // No two boxes overlap.
        foreach (var a in diagram.Boxes)
        foreach (var b in diagram.Boxes)
            if (a != b)
                Assert.False(a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height, $"{a.Table} overlaps {b.Table}");
    }

    [Test]
    public static void ErLayoutHandlesCycles()
    {
        using var s = new DbSession();
        s.RunOk("CREATE TABLE a (id INTEGER PRIMARY KEY, b_id REFERENCES b); CREATE TABLE b (id INTEGER PRIMARY KEY, a_id REFERENCES a);");
        var diagram = ErLayout.Compute(s.Schema);
        Assert.Equal(17, diagram.Boxes.Count);
    }
}
