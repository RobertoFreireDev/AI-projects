namespace SqliteViz.Core.Schema;

/// <param name="Hidden">0 normal, 1 hidden (virtual table), 2 generated VIRTUAL, 3 generated STORED (pragma_table_xinfo).</param>
public sealed record ColumnInfo(
    string Name,
    string DeclaredType,
    int PrimaryKeyPosition,
    bool NotNull,
    string? DefaultValue,
    int Hidden)
{
    public bool IsPrimaryKey => PrimaryKeyPosition > 0;
    public bool IsGenerated => Hidden is 2 or 3;
    public string? GeneratedKind => Hidden switch { 2 => "VIRTUAL", 3 => "STORED", _ => null };
}

public sealed record ForeignKeyInfo(
    int Id,
    IReadOnlyList<string> FromColumns,
    string ToTable,
    IReadOnlyList<string> ToColumns,
    string OnDelete,
    string OnUpdate);

/// <param name="Origin">"c" CREATE INDEX, "u" UNIQUE constraint, "pk" PRIMARY KEY.</param>
/// <param name="Columns">Key columns; expressions show as "&lt;expr&gt;".</param>
public sealed record IndexInfo(
    string Name,
    bool Unique,
    bool Partial,
    string Origin,
    IReadOnlyList<string> Columns,
    string? Sql);

public sealed record TableInfo(
    string Schema,
    string Name,
    string Type,
    bool Strict,
    bool WithoutRowId,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys,
    IReadOnlyList<IndexInfo> Indexes,
    long? RowCount,
    string? Sql)
{
    public bool IsVirtual => Type == "virtual";
    public string QualifiedName => Schema == "main" ? Name : $"{Schema}.{Name}";
}

public sealed record ViewInfo(string Schema, string Name, IReadOnlyList<string> Columns, string? Sql);

public sealed record TriggerInfo(string Name, string Table, string? Sql);

public sealed record SchemaSnapshot(
    IReadOnlyList<TableInfo> Tables,
    IReadOnlyList<ViewInfo> Views,
    IReadOnlyList<TriggerInfo> Triggers)
{
    public static readonly SchemaSnapshot Empty = new([], [], []);

    public TableInfo? FindTable(string name) =>
        Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Table, view and column names for autocomplete.</summary>
    public IReadOnlyList<SchemaWord> Words()
    {
        var words = new List<SchemaWord>();
        var columns = new Dictionary<string, SchemaWord>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tables)
        {
            words.Add(new SchemaWord(t.Name, t.IsVirtual ? "virtual table" : "table"));
            foreach (var c in t.Columns)
                columns.TryAdd(c.Name, new SchemaWord(c.Name, $"{t.Name}.{(c.DeclaredType.Length > 0 ? c.DeclaredType.ToLowerInvariant() : "col")}"));
        }
        foreach (var v in Views)
        {
            words.Add(new SchemaWord(v.Name, "view"));
            foreach (var c in v.Columns) columns.TryAdd(c, new SchemaWord(c, $"{v.Name} col"));
        }
        words.AddRange(columns.Values);
        return words;
    }
}

public sealed record SchemaWord(string Value, string Meta);
