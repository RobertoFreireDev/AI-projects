using Microsoft.Data.Sqlite;
using SqliteViz.Core.Session;

namespace SqliteViz.Core;

/// <summary>Facts about the bundled SQLite library, checked once at startup and cached (read-only).</summary>
public sealed record SqliteInfo(string Version, bool NativeMathFunctions, bool HasFts5, bool HasRtree);

public static class SqliteEnvironment
{
    public static readonly Version MinimumVersion = new(3, 45, 0);

    private static readonly Lazy<SqliteInfo> _info = new(Probe);

    public static SqliteInfo Info => _info.Value;

    /// <summary>Makes sure the native library is loaded before raw SQLitePCL calls that do not need a connection.</summary>
    public static void EnsureInitialized() => _ = _info.Value;

    /// <summary>Throws when the bundled SQLite is older than <see cref="MinimumVersion"/>.</summary>
    public static SqliteInfo CheckOrThrow()
    {
        var info = Info;
        if (ParseVersion(info.Version) < MinimumVersion)
            throw new InvalidOperationException(
                $"SqliteViz needs SQLite {MinimumVersion.ToString(3)} or newer (RIGHT/FULL JOIN, ->/->>, unixepoch, timediff, concat, jsonb), but the loaded library is {info.Version}.");
        return info;
    }

    public static Version ParseVersion(string version)
    {
        var parts = version.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        return new Version(parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1), parts.ElementAtOrDefault(2));
    }

    private static SqliteInfo Probe()
    {
        using var connection = new SqliteConnection(DbSession.ConnectionString);
        connection.Open();
        var version = Scalar(connection, "SELECT sqlite_version()") as string ?? "0";
        bool math;
        try { math = Convert.ToDouble(Scalar(connection, "SELECT sqrt(4)")) == 2.0; }
        catch (SqliteException) { math = false; }
        var options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT compile_options FROM pragma_compile_options";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) options.Add(reader.GetString(0));
        }
        return new SqliteInfo(version, math, options.Contains("ENABLE_FTS5"), options.Contains("ENABLE_RTREE"));
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
