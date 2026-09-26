using SqliteViz.Core;
using SqliteViz.Core.Samples;
using SqliteViz.Core.Session;

namespace SqliteViz.Tests;

public static class SampleTests
{
    [Test]
    public static void CatalogHasAllSamples()
    {
        var ids = SampleCatalog.All.Select(s => s.Id).ToList();
        Assert.Equal("00-documentation", ids[0]);
        Assert.Equal(SampleCatalog.SeedId, ids[1]);
        Assert.Equal(23, ids.Count);
        foreach (var s in SampleCatalog.All)
            Assert.True(s.Sql.StartsWith("-- ", StringComparison.Ordinal) && s.Title.Length > 0, $"{s.Id} needs a '-- Title' first line");
    }

    [Test]
    public static void EverySampleRunsOnFreshDatabase()
    {
        var failures = new List<string>();
        foreach (var sample in SampleCatalog.All)
        {
            if (sample.Id == "22-fts5" && !SqliteEnvironment.Info.HasFts5) continue;
            using var s = new DbSession();
            var r = s.Run(sample.Sql);
            if (r.Error is { } e) failures.Add($"{sample.Id} line {e.Line}: {e.Message}");
            else if (r.InTransaction) failures.Add($"{sample.Id} leaves a transaction open");
        }
        Assert.True(failures.Count == 0, string.Join("\n        ", failures.Prepend("")));
    }
}
