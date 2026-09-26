using SqliteViz.Core.Plan;
using SqliteViz.Core.Schema;

namespace SqliteViz.Core.Runner;

public enum StatementKind { Query, Dml, Ddl, Pragma, QueryPlan, Transaction, Other }

/// <summary>SQLite storage class of a single value (from sqlite3_column_type, not the declared type).</summary>
public enum StorageClass { Null, Integer, Real, Text, Blob }

public sealed record ResultColumn(string Name, string? DeclaredType);

/// <param name="Text">Display text, already truncated for display.</param>
/// <param name="Truncated">True when <paramref name="Text"/> was shortened.</param>
public sealed record ResultCell(StorageClass Class, string Text, bool Truncated = false)
{
    public static readonly ResultCell Null = new(StorageClass.Null, "NULL");
}

public sealed record ResultRow(IReadOnlyList<ResultCell> Cells);

public sealed record StatementResult(
    int Index,
    int Line,
    string Sql,
    StatementKind Kind,
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<ResultRow> Rows,
    int TotalRows,
    bool Truncated,
    long? RowsAffected,
    PlanNode? Plan,
    TimeSpan Elapsed)
{
    /// <summary>Last line of the statement in the user's script (inclusive).</summary>
    public int EndLine => Line + SqlText.CountLines(Sql) - 1;

    /// <summary>True when stepping stopped at <see cref="RunLimits.MaxCountedRows"/>, so the real total is unknown.</summary>
    public bool TotalCapped => TotalRows >= RunLimits.MaxCountedRows;
}

/// <param name="StatementIndex">Index of the failing statement, or null when the run failed before any statement (e.g. too many statements).</param>
/// <param name="Line">1-based line in the user's script.</param>
/// <param name="Column">1-based column, or null when SQLite did not report an offset.</param>
public sealed record SqlError(
    string Message,
    int Code,
    int? StatementIndex,
    string? Sql,
    int Line,
    int? Column)
{
    public int EndLine => Sql is null ? Line : Line + SqlText.CountLines(Sql) - 1;
}

public sealed record RunResult(
    IReadOnlyList<StatementResult> Statements,
    SqlError? Error,
    SchemaSnapshot Schema,
    TimeSpan Elapsed,
    bool InTransaction);

public static class RunLimits
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    public const int MaxKeptRows = 1_000;
    public const int MaxCountedRows = 100_000;
    public const int MaxStatements = 100;
    public const int MaxDisplayChars = 200;
    public const int MaxBlobPreviewBytes = 16;
    public const long MaxDatabaseBytes = 64L * 1024 * 1024;
    public const int MaxValueLength = 1_000_000;
    public const int MaxSqlLength = 100_000;
}
