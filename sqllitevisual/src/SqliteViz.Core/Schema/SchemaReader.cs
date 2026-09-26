using Microsoft.Data.Sqlite;
using SqliteViz.Core.Runner;

namespace SqliteViz.Core.Schema;

/// <summary>
/// Builds a <see cref="SchemaSnapshot"/> from the pragma table-valued functions and sqlite_schema. Runs as the host
/// (authorizer bypassed by the session) and never while user SQL is executing.
/// </summary>
public static class SchemaReader
{
    public static SchemaSnapshot Read(SqliteConnection connection)
    {
        var tables = new List<TableInfo>();
        var views = new List<ViewInfo>();
        var list = Query(connection,
            """
            SELECT schema, name, type, wr, strict FROM pragma_table_list
            WHERE schema IN ('main', 'temp') AND type IN ('table', 'view', 'virtual')
              AND name NOT LIKE 'sqlite\_%' ESCAPE '\'
            ORDER BY schema = 'temp', name
            """,
            r => (Schema: r.GetString(0), Name: r.GetString(1), Type: r.GetString(2), Wr: r.GetInt32(3) != 0, Strict: r.GetInt32(4) != 0));

        foreach (var t in list)
        {
            var sql = SqlOf(connection, t.Schema, t.Name);
            if (t.Type == "view")
            {
                var cols = Query(connection, "SELECT name FROM pragma_table_info($name, $schema) ORDER BY cid",
                    r => r.GetString(0), ("$name", t.Name), ("$schema", t.Schema));
                views.Add(new ViewInfo(t.Schema, t.Name, cols, sql));
                continue;
            }

            var columns = Query(connection,
                "SELECT name, type, pk, \"notnull\", dflt_value, hidden FROM pragma_table_xinfo($name, $schema) ORDER BY cid",
                r => new ColumnInfo(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3) != 0,
                    r.IsDBNull(4) ? null : r.GetString(4), r.GetInt32(5)),
                ("$name", t.Name), ("$schema", t.Schema));

            var foreignKeys = ReadForeignKeys(connection, t.Schema, t.Name);
            var indexes = t.Type == "virtual" ? [] : ReadIndexes(connection, t.Schema, t.Name);
            long? rowCount;
            try
            {
                rowCount = Convert.ToInt64(Scalar(connection,
                    $"SELECT count(*) FROM {SqlText.QuoteIdentifier(t.Schema)}.{SqlText.QuoteIdentifier(t.Name)}"));
            }
            catch (SqliteException)
            {
                rowCount = null;
            }
            tables.Add(new TableInfo(t.Schema, t.Name, t.Type, t.Strict, t.Wr, columns, foreignKeys, indexes, rowCount, sql));
        }

        var triggers = Query(connection,
            """
            SELECT name, tbl_name, sql FROM sqlite_schema WHERE type = 'trigger'
            UNION ALL
            SELECT name, tbl_name, sql FROM sqlite_temp_schema WHERE type = 'trigger'
            ORDER BY 2, 1
            """,
            r => new TriggerInfo(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));

        return new SchemaSnapshot(tables, views, triggers);
    }

    private static List<ForeignKeyInfo> ReadForeignKeys(SqliteConnection connection, string schema, string table)
    {
        var rows = Query(connection,
            """SELECT id, seq, "table", "from", "to", on_update, on_delete FROM pragma_foreign_key_list($name, $schema) ORDER BY id, seq""",
            r => (Id: r.GetInt32(0), Table: r.GetString(2), From: r.GetString(3), To: r.IsDBNull(4) ? null : r.GetString(4),
                OnUpdate: r.GetString(5), OnDelete: r.GetString(6)),
            ("$name", table), ("$schema", schema));

        var result = new List<ForeignKeyInfo>();
        foreach (var group in rows.GroupBy(r => r.Id))
        {
            var first = group.First();
            var to = group.Select(r => r.To).ToList();
            if (to.Any(c => c is null))
            {
                // "REFERENCES parent" without columns means the parent's primary key.
                var pk = Query(connection, "SELECT name FROM pragma_table_info($name, $schema) WHERE pk > 0 ORDER BY pk",
                    r => r.GetString(0), ("$name", first.Table), ("$schema", schema));
                to = pk.Count == group.Count() ? pk.Cast<string?>().ToList() : to.Select(c => c ?? "?").Cast<string?>().ToList();
            }
            result.Add(new ForeignKeyInfo(first.Id, group.Select(r => r.From).ToList(), first.Table,
                to.Select(c => c!).ToList(), first.OnDelete, first.OnUpdate));
        }
        return result;
    }

    private static List<IndexInfo> ReadIndexes(SqliteConnection connection, string schema, string table)
    {
        var list = Query(connection,
            """SELECT name, "unique", origin, partial FROM pragma_index_list($name, $schema) ORDER BY seq DESC""",
            r => (Name: r.GetString(0), Unique: r.GetInt32(1) != 0, Origin: r.GetString(2), Partial: r.GetInt32(3) != 0),
            ("$name", table), ("$schema", schema));

        var result = new List<IndexInfo>();
        foreach (var ix in list)
        {
            var cols = Query(connection,
                "SELECT cid, name, \"desc\" FROM pragma_index_xinfo($name, $schema) WHERE key = 1 ORDER BY seqno",
                r => r.GetInt32(0) == -2 ? "<expr>" : (r.IsDBNull(1) ? "rowid" : r.GetString(1)) + (r.GetInt32(2) != 0 ? " DESC" : ""),
                ("$name", ix.Name), ("$schema", schema));
            result.Add(new IndexInfo(ix.Name, ix.Unique, ix.Partial, ix.Origin, cols, SqlOf(connection, schema, ix.Name)));
        }
        return result;
    }

    private static string? SqlOf(SqliteConnection connection, string schema, string name)
    {
        var table = schema == "temp" ? "sqlite_temp_schema" : "sqlite_schema";
        return Scalar(connection, $"SELECT sql FROM {table} WHERE name = $name", ("$name", name)) as string;
    }

    private static List<T> Query<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map,
        params (string Name, object Value)[] parameters)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        using var reader = cmd.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(map(reader));
        return result;
    }

    private static object? Scalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteScalar();
    }
}
