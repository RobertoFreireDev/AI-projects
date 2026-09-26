using System.Reflection;

namespace SqliteViz.Core.Samples;

/// <param name="Id">File name without extension, e.g. "02-select-basics".</param>
/// <param name="Title">From the first line, "-- Title".</param>
public sealed record Sample(string Id, string Title, string Sql);

/// <summary>Built-in samples, embedded as resources and cached (read-only).</summary>
public static class SampleCatalog
{
    public const string SeedId = "01-seed";

    private static readonly Lazy<IReadOnlyList<Sample>> _all = new(Load);

    public static IReadOnlyList<Sample> All => _all.Value;

    public static Sample Seed => Get(SeedId);

    public static Sample Get(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException($"No sample named '{id}'.");

    private static IReadOnlyList<Sample> Load()
    {
        var assembly = typeof(SampleCatalog).Assembly;
        const string prefix = "Samples.";
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n => Read(assembly, n, n[prefix.Length..^".sql".Length]))
            .ToList();
    }

    private static Sample Read(Assembly assembly, string resource, string id)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd().Replace("\r\n", "\n");
        var firstLine = sql.Split('\n', 2)[0];
        var title = firstLine.StartsWith("--", StringComparison.Ordinal) ? firstLine[2..].Trim() : id;
        return new Sample(id, title, sql);
    }
}
