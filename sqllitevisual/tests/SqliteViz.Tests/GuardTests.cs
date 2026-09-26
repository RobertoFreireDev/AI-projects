using SqliteViz.Core.Guard;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class GuardTests
{
    private static void Blocked(string sql, string message)
    {
        using var s = new DbSession();
        var error = s.RunError(sql);
        Assert.Equal(message, error.Message, sql);
    }

    [Test] public static void Attach() => Blocked("ATTACH DATABASE 'x.db' AS x;", SqlGuard.AttachMessage);
    [Test] public static void AttachMemory() => Blocked("/* sneaky */ attach ':memory:' AS m;", SqlGuard.AttachMessage);
    [Test] public static void Detach() => Blocked("DETACH DATABASE main;", SqlGuard.AttachMessage);
    [Test] public static void Vacuum() => Blocked("VACUUM;", SqlGuard.VacuumMessage);
    [Test] public static void VacuumInto() => Blocked("-- copy\nVACUUM INTO '/tmp/copy.db';", SqlGuard.VacuumMessage);
    [Test] public static void ExplainVacuum() => Blocked("EXPLAIN VACUUM;", SqlGuard.VacuumMessage);
    [Test] public static void LoadExtension() => Blocked("SELECT load_extension('/tmp/evil.so');", SqlGuard.ExtensionMessage);
    [Test] public static void WritableSchema() => Blocked("PRAGMA writable_schema = 1;", SqlGuard.PragmaMessage("writable_schema = 1"));
    [Test] public static void JournalMode() => Blocked("PRAGMA journal_mode = OFF;", SqlGuard.PragmaMessage("journal_mode = OFF"));
    [Test] public static void JournalModeRead() => Blocked("PRAGMA journal_mode;", SqlGuard.PragmaMessage("journal_mode"));
    [Test] public static void TempStoreWrite() => Blocked("PRAGMA temp_store = FILE;", SqlGuard.PragmaMessage("temp_store = FILE"));
    [Test] public static void MaxPageCountWrite() => Blocked("PRAGMA max_page_count = 999999999;", SqlGuard.PragmaMessage("max_page_count = 999999999"));
    [Test] public static void MmapSize() => Blocked("PRAGMA mmap_size = 1000000;", SqlGuard.PragmaMessage("mmap_size = 1000000"));
    [Test] public static void TrustedSchema() => Blocked("PRAGMA trusted_schema = 1;", SqlGuard.PragmaMessage("trusted_schema = 1"));
    [Test] public static void UnknownPragma() => Blocked("PRAGMA no_such_pragma;", SqlGuard.PragmaMessage("no_such_pragma"));
    [Test] public static void DisallowedPragmaFunction() => Blocked("SELECT * FROM pragma_journal_mode;", SqlGuard.PragmaMessage("journal_mode"));
    [Test] public static void DisallowedPragmaFunctionInSubquery() => Blocked("SELECT (SELECT cache_size FROM pragma_cache_size) AS c;", SqlGuard.PragmaMessage("cache_size"));
    [Test] public static void WriteSchemaTable() => Blocked("DELETE FROM sqlite_schema;", SqlGuard.SchemaTableMessage);
    [Test] public static void UpdateSqliteMaster() => Blocked("UPDATE sqlite_master SET sql = '' WHERE name = 'orders';", SqlGuard.SchemaTableMessage);
    [Test] public static void DisallowedVirtualTable() => Blocked("CREATE VIRTUAL TABLE f USING fts4(body);", SqlGuard.VirtualTableMessage);

    [Test]
    public static void DeniedInsideTriggerBody()
    {
        using var s = new DbSession();
        // Trigger bodies are authorized when the trigger fires, as part of the statement that fires it.
        s.RunOk("CREATE TRIGGER t AFTER INSERT ON tags BEGIN SELECT load_extension('x'); END;");
        var error = s.RunError("INSERT INTO tags (name) VALUES ('boom');");
        Assert.Equal(SqlGuard.ExtensionMessage, error.Message);
    }

    [Test]
    public static void AllowedPragmasWork()
    {
        using var s = new DbSession();
        var r = s.RunOk("""
            PRAGMA foreign_keys;
            PRAGMA foreign_keys = OFF;
            PRAGMA foreign_keys = ON;
            PRAGMA user_version = 7;
            PRAGMA user_version;
            PRAGMA table_info(orders);
            PRAGMA table_xinfo('order_items');
            PRAGMA index_list(orders);
            PRAGMA foreign_key_list(order_items);
            PRAGMA foreign_key_check;
            PRAGMA integrity_check;
            PRAGMA quick_check;
            PRAGMA page_size;
            PRAGMA temp_store;
            PRAGMA max_page_count;
            PRAGMA compile_options;
            PRAGMA recursive_triggers = OFF;
            PRAGMA case_sensitive_like = ON;
            PRAGMA analysis_limit = 100;
            PRAGMA optimize;
            SELECT name, type FROM pragma_table_info('orders');
            SELECT count(*) FROM pragma_function_list;
            """);
        Assert.Equal("7", r.Statements[4].Rows[0].Cells[0].Text);
        Assert.Equal(6, r.Statements[5].Rows.Count);
        Assert.Equal("ok", r.Statements[10].Rows[0].Cells[0].Text);
        Assert.Equal("2", r.Statements[13].Rows[0].Cells[0].Text); // temp_store = MEMORY
        Assert.Equal("16384", r.Statements[14].Rows[0].Cells[0].Text);
    }

    [Test]
    public static void AllowedDdlAndTransactions()
    {
        using var s = new DbSession();
        s.RunOk("""
            CREATE TABLE t (x);
            ALTER TABLE t ADD COLUMN y;
            CREATE INDEX t_x ON t (x);
            CREATE VIEW v AS SELECT * FROM t;
            BEGIN; INSERT INTO t VALUES (1, 2); SAVEPOINT a; DELETE FROM t; ROLLBACK TO a; RELEASE a; COMMIT;
            DELETE FROM t;
            DROP VIEW v; DROP TABLE t;
            CREATE VIRTUAL TABLE docs USING fts5(body);
            CREATE VIRTUAL TABLE boxes USING rtree(id, minx, maxx);
            ANALYZE;
            """);
    }
}
