using Microsoft.Data.Sqlite;
using SqliteViz.Core;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class EnvironmentTests
{
    [Test]
    public static void SqliteVersionIsAtLeastMinimum()
    {
        var info = SqliteEnvironment.CheckOrThrow();
        Assert.True(SqliteEnvironment.ParseVersion(info.Version) >= new Version(3, 45, 0), $"SQLite {info.Version} is too old");
    }

    [Test]
    public static void MathFunctionsWorkInSession()
    {
        using var s = new DbSession();
        Assert.Equal("2.0", s.Scalar("SELECT sqrt(4);"));
        Assert.Equal("2.0", s.Scalar("SELECT log10(100);"));
    }

    [Test]
    public static void MathFallbackRegistersSameNames()
    {
        using var connection = new SqliteConnection(DbSession.ConnectionString);
        connection.Open();
        MathFunctions.Register(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT sqrt(16), pow(2, 10), log(2, 8), log(1000), floor(-1.5), mod(7, 3), round(degrees(pi()), 6), sqrt(-1) IS NULL";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal(4.0, r.GetDouble(0), "sqrt");
        Assert.Equal(1024.0, r.GetDouble(1), "pow");
        Assert.Equal(3.0, Math.Round(r.GetDouble(2), 9), "log(b, x)");
        Assert.Equal(3.0, Math.Round(r.GetDouble(3), 9), "log(x)");
        Assert.Equal(-2.0, r.GetDouble(4), "floor");
        Assert.Equal(1.0, r.GetDouble(5), "mod");
        Assert.Equal(180.0, r.GetDouble(6), "degrees");
        Assert.Equal(1L, r.GetInt64(7), "NaN -> NULL");
    }
}
