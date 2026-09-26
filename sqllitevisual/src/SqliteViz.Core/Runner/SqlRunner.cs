using System.Diagnostics;
using System.Globalization;
using System.Text;
using SQLitePCL;
using SqliteViz.Core.Guard;
using SqliteViz.Core.Plan;
using SqliteViz.Core.Schema;
using SqliteViz.Core.Session;

namespace SqliteViz.Core.Runner;

/// <summary>
/// Runs a script against a session: split, pre-check, then prepare and step each statement in order (autocommit
/// unless the user wrote BEGIN), stopping at the first error, and finally refresh the schema snapshot.
/// </summary>
public static class SqlRunner
{
    public const string TimeoutMessage = "Query timed out after 5 s";

    /// <param name="baseLine">Editor line where <paramref name="script"/> starts (when running a selection).</param>
    /// <param name="baseColumn">Editor column where the first line of <paramref name="script"/> starts.</param>
    public static async Task<RunResult> RunAsync(DbSession session, string script, int baseLine = 1, int baseColumn = 1)
    {
        await session.Lock.WaitAsync();
        try
        {
            // SQLite blocks; keep it off the circuit's synchronization context.
            return await Task.Run(() => RunLocked(session, script, baseLine, baseColumn));
        }
        finally
        {
            session.Lock.Release();
        }
    }

    /// <summary>Runs with the session lock already held.</summary>
    internal static RunResult RunLocked(DbSession session, string script, int baseLine = 1, int baseColumn = 1)
    {
        var total = Stopwatch.StartNew();
        var db = session.Handle;
        var results = new List<StatementResult>();
        SqlError? error = null;

        var pieces = SqlSplitter.Split(script, baseLine, baseColumn);
        if (pieces.Count > RunLimits.MaxStatements)
        {
            var extra = pieces[RunLimits.MaxStatements];
            error = new SqlError(
                $"This script has {pieces.Count} statements; the limit is {RunLimits.MaxStatements} per run. Run it in parts.",
                raw.SQLITE_TOOBIG, null, null, extra.Line, extra.Column);
        }
        else
        {
            using var watchdog = new Watchdog(db, RunLimits.Timeout);
            for (var i = 0; i < pieces.Count; i++)
            {
                var outcome = Execute(session, db, pieces[i], results.Count, watchdog);
                if (outcome.Error is { } e) { error = e; break; }
                if (outcome.Result is { } r) results.Add(r);
            }
        }

        SchemaSnapshot schema;
        session.HostMode = true;
        try { schema = SchemaReader.Read(session.Connection); }
        finally { session.HostMode = false; }
        session.Schema = schema;

        return new RunResult(results, error, schema, total.Elapsed, raw.sqlite3_get_autocommit(db) == 0);
    }

    private readonly record struct Outcome(StatementResult? Result, SqlError? Error);

    private static Outcome Execute(DbSession session, sqlite3 db, SqlPiece piece, int index, Watchdog watchdog)
    {
        var sw = Stopwatch.StartNew();
        if (SqlGuard.PreCheck(piece.Sql) is { } friendly)
            return new(null, new SqlError(friendly, raw.SQLITE_AUTH, index, piece.Sql, piece.Line, piece.Column));
        if (watchdog.Fired)
            return new(null, new SqlError(TimeoutMessage, raw.SQLITE_INTERRUPT, index, piece.Sql, piece.Line, piece.Column));

        session.LastDenial = null;
        session.DmlStatement = SqlGuard.IsDmlStatement(piece.Sql);
        var rc = raw.sqlite3_prepare_v3(db, piece.Sql, 0, out var stmt, out string _);
        using (stmt)
        {
            if (rc != raw.SQLITE_OK)
                return new(null, ErrorFrom(session, db, piece, index, watchdog, withOffset: true));
            if (stmt is null || stmt.IsInvalid)
                return new(null, null); // only comments/whitespace: nothing to run

            var kind = KindOf(piece.Sql, stmt);
            var columnCount = raw.sqlite3_column_count(stmt);
            var columns = new List<ResultColumn>(columnCount);
            for (var c = 0; c < columnCount; c++)
                columns.Add(new ResultColumn(
                    raw.sqlite3_column_name(stmt, c).utf8_to_string() ?? $"column{c + 1}",
                    raw.sqlite3_column_decltype(stmt, c).utf8_to_string()));

            var rows = new List<ResultRow>();
            var planRows = kind == StatementKind.QueryPlan ? new List<(int, int, string)>() : null;
            var totalRows = 0;
            var truncated = false;
            while (true)
            {
                rc = raw.sqlite3_step(stmt);
                if (rc == raw.SQLITE_DONE) break;
                if (rc != raw.SQLITE_ROW)
                    return new(null, ErrorFrom(session, db, piece, index, watchdog, withOffset: false));

                totalRows++;
                if (rows.Count < RunLimits.MaxKeptRows) rows.Add(ReadRow(stmt, columnCount));
                else truncated = true;
                if (planRows is not null && columnCount >= 4)
                    planRows.Add((raw.sqlite3_column_int(stmt, 0), raw.sqlite3_column_int(stmt, 1),
                        raw.sqlite3_column_text(stmt, 3).utf8_to_string() ?? ""));
                if (totalRows >= RunLimits.MaxCountedRows) { truncated = true; break; }
            }

            long? affected = kind == StatementKind.Dml ? raw.sqlite3_changes(db) : null;
            var plan = planRows is not null ? QueryPlanParser.Parse(planRows) : null;
            return new(new StatementResult(index, piece.Line, piece.Sql, kind, columns, rows, totalRows, truncated,
                affected, plan, sw.Elapsed), null);
        }
    }

    private static SqlError ErrorFrom(DbSession session, sqlite3 db, SqlPiece piece, int index, Watchdog watchdog, bool withOffset)
    {
        var code = raw.sqlite3_extended_errcode(db);
        var message = raw.sqlite3_errmsg(db).utf8_to_string() ?? "Unknown error";
        if ((code & 0xff) == raw.SQLITE_INTERRUPT && watchdog.Fired) message = TimeoutMessage;
        else if (session.LastDenial is { } denial) message = denial;
        else message = SqlGuard.FriendlyMessage(message);

        var (line, column) = (piece.Line, (int?)null);
        if (withOffset && NativeMethods.ErrorOffset(db) is { } byteOffset)
            (line, column) = Locate(piece, byteOffset);
        return new SqlError(message, code, index, piece.Sql, line, column);
    }

    /// <summary>Maps a UTF-8 byte offset inside a statement to an editor line and column.</summary>
    internal static (int Line, int Column) Locate(SqlPiece piece, int byteOffset)
    {
        var bytes = Encoding.UTF8.GetBytes(piece.Sql);
        var prefix = Encoding.UTF8.GetString(bytes, 0, Math.Clamp(byteOffset, 0, bytes.Length));
        var lastNewline = prefix.LastIndexOf('\n');
        var newlines = prefix.Count(ch => ch == '\n');
        return lastNewline < 0
            ? (piece.Line, piece.Column + prefix.Length)
            : (piece.Line + newlines, prefix.Length - lastNewline);
    }

    internal static StatementKind KindOf(string sql, sqlite3_stmt stmt)
    {
        var words = SqlText.LeadingKeywords(sql, 3);
        return words.ElementAtOrDefault(0) switch
        {
            "EXPLAIN" => words.ElementAtOrDefault(1) == "QUERY" ? StatementKind.QueryPlan : StatementKind.Query,
            "SELECT" or "VALUES" => StatementKind.Query,
            "WITH" => raw.sqlite3_stmt_readonly(stmt) != 0 ? StatementKind.Query : StatementKind.Dml,
            "INSERT" or "UPDATE" or "DELETE" or "REPLACE" => StatementKind.Dml,
            "CREATE" or "DROP" or "ALTER" or "REINDEX" or "ANALYZE" => StatementKind.Ddl,
            "PRAGMA" => StatementKind.Pragma,
            "BEGIN" or "COMMIT" or "END" or "ROLLBACK" or "SAVEPOINT" or "RELEASE" => StatementKind.Transaction,
            _ => StatementKind.Other,
        };
    }

    private static ResultRow ReadRow(sqlite3_stmt stmt, int columnCount)
    {
        var cells = new ResultCell[columnCount];
        for (var c = 0; c < columnCount; c++) cells[c] = ReadCell(stmt, c);
        return new ResultRow(cells);
    }

    private static ResultCell ReadCell(sqlite3_stmt stmt, int c)
    {
        switch (raw.sqlite3_column_type(stmt, c))
        {
            case raw.SQLITE_INTEGER:
                return new(StorageClass.Integer, raw.sqlite3_column_int64(stmt, c).ToString(CultureInfo.InvariantCulture));
            case raw.SQLITE_FLOAT:
                // SQLite's own REAL-to-text conversion, so values look exactly like SQLite prints them.
                return new(StorageClass.Real, raw.sqlite3_column_text(stmt, c).utf8_to_string() ?? "");
            case raw.SQLITE_TEXT:
            {
                var text = raw.sqlite3_column_text(stmt, c).utf8_to_string() ?? "";
                return text.Length > RunLimits.MaxDisplayChars
                    ? new(StorageClass.Text, text[..RunLimits.MaxDisplayChars] + "…", true)
                    : new(StorageClass.Text, text);
            }
            case raw.SQLITE_BLOB:
                return FormatBlob(raw.sqlite3_column_blob(stmt, c));
            default:
                return ResultCell.Null;
        }
    }

    public static ResultCell FormatBlob(ReadOnlySpan<byte> blob)
    {
        var shown = Math.Min(blob.Length, RunLimits.MaxBlobPreviewBytes);
        var hex = Convert.ToHexString(blob[..shown]);
        var more = blob.Length > shown ? "…" : "";
        var unit = blob.Length == 1 ? "byte" : "bytes";
        return new(StorageClass.Blob, $"x'{hex}{more}' ({blob.Length} {unit})", blob.Length > shown);
    }

    /// <summary>Interrupts the connection when the run exceeds its time budget. sqlite3_interrupt is thread-safe.</summary>
    private sealed class Watchdog : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly Timer _timer;
        private bool _active = true;
        private volatile bool _fired;

        public Watchdog(sqlite3 db, TimeSpan timeout)
        {
            _timer = new Timer(_ =>
            {
                lock (_gate)
                {
                    if (!_active) return;
                    _fired = true;
                    raw.sqlite3_interrupt(db);
                }
            }, null, timeout, Timeout.InfiniteTimeSpan);
        }

        public bool Fired => _fired;

        public void Dispose()
        {
            lock (_gate) _active = false;
            _timer.Dispose();
        }
    }
}
