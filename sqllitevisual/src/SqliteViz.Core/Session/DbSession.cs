using Microsoft.Data.Sqlite;
using SQLitePCL;
using SqliteViz.Core.Guard;
using SqliteViz.Core.Runner;
using SqliteViz.Core.Samples;
using SqliteViz.Core.Schema;

namespace SqliteViz.Core.Session;

/// <summary>
/// One private in-memory database, owned by one Blazor circuit (scoped service). A connection is not thread-safe
/// and a user can click Run twice, so every use goes through <see cref="Lock"/>.
/// </summary>
public sealed class DbSession : IDisposable
{
    /// <summary>In memory only. Pooling is off so closing the connection really frees the database.</summary>
    public const string ConnectionString = "Data Source=:memory:;Pooling=False";

    private SqliteConnection _connection;
    private strdelegate_authorizer? _authorizer; // kept alive for the native callback
    private bool _disposed;

    internal SemaphoreSlim Lock { get; } = new(1, 1);

    /// <summary>When true the authorizer allows everything. Only set by host code (schema reader), never while user SQL runs.</summary>
    internal bool HostMode { get; set; }

    /// <summary>The first denial message of the statement being prepared, so the runner can replace "not authorized".</summary>
    internal string? LastDenial { get; set; }

    /// <summary>The statement being prepared is INSERT/UPDATE/DELETE (see <see cref="SqlGuard.Check"/>).</summary>
    internal bool DmlStatement { get; set; }

    /// <summary>Schema after the last run (or after seeding).</summary>
    public SchemaSnapshot Schema { get; internal set; } = SchemaSnapshot.Empty;

    public DbSession()
    {
        _connection = Open();
    }

    internal SqliteConnection Connection => _connection;

    internal sqlite3 Handle => _connection.Handle ?? throw new ObjectDisposedException(nameof(DbSession));

    public bool InTransaction => raw.sqlite3_get_autocommit(Handle) == 0;

    /// <summary>Throws the database away and re-seeds a fresh one.</summary>
    public async Task ResetAsync()
    {
        await Lock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await Task.Run(() =>
            {
                var old = _connection;
                old.Dispose();
                _connection = Open();
            });
        }
        finally
        {
            Lock.Release();
        }
    }

    private SqliteConnection Open()
    {
        SqliteEnvironment.EnsureInitialized();
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        try
        {
            _connection = connection;
            var db = connection.Handle!;

            Execute(connection, "PRAGMA foreign_keys = ON;");
            Execute(connection, "PRAGMA temp_store = MEMORY;");
            var pageSize = Convert.ToInt64(Scalar(connection, "PRAGMA page_size;"));
            Execute(connection, $"PRAGMA max_page_count = {RunLimits.MaxDatabaseBytes / pageSize};");

            Check(db, raw.sqlite3_db_config(db, raw.SQLITE_DBCONFIG_DEFENSIVE, 1, out _), "enable defensive mode");
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_LENGTH, RunLimits.MaxValueLength);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_SQL_LENGTH, RunLimits.MaxSqlLength);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_ATTACHED, 0);

            if (!SqliteEnvironment.Info.NativeMathFunctions) MathFunctions.Register(connection);

            HostMode = false;
            _authorizer = Authorize;
            Check(db, raw.sqlite3_set_authorizer(db, _authorizer, null), "install the authorizer");

            var seed = SqlRunner.RunLocked(this, SampleCatalog.Seed.Sql);
            if (seed.Error is { } error)
                throw new InvalidOperationException($"The seed script failed at line {error.Line}: {error.Message}");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private int Authorize(object userData, int action, string param0, string param1, string dbName, string trigger)
    {
        if (HostMode) return raw.SQLITE_OK;
        var denial = SqlGuard.Check(action, param0, param1, DmlStatement);
        if (denial is null) return raw.SQLITE_OK;
        LastDenial ??= denial;
        return raw.SQLITE_DENY;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static void Check(sqlite3 db, int rc, string what)
    {
        if (rc != raw.SQLITE_OK)
            throw new InvalidOperationException($"Could not {what}: {raw.sqlite3_errmsg(db).utf8_to_string()}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Stop a statement that may still be running for this circuit, then wait briefly for the run to end.
        if (_connection.Handle is { } db) raw.sqlite3_interrupt(db);
        var acquired = Lock.Wait(TimeSpan.FromSeconds(10));
        try
        {
            _connection.Dispose();
        }
        finally
        {
            if (acquired) Lock.Release();
        }
    }
}
