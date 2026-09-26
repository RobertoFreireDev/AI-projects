namespace AlgoViz.Web.Services;

public sealed record Sample(string Id, string Title, string Code);

/// <summary>Built-in example scripts, embedded from Samples/*.csx. The first line comment is the title.</summary>
public sealed class SampleCatalog
{
    public SampleCatalog()
    {
        var assembly = typeof(SampleCatalog).Assembly;
        const string prefix = "AlgoViz.Web.Samples.";
        Samples = [.. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".csx", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                var code = reader.ReadToEnd().ReplaceLineEndings("\n");
                var id = name[prefix.Length..^".csx".Length];
                return new Sample(id, TitleOf(code, id), code);
            })];
    }

    public IReadOnlyList<Sample> Samples { get; }

    public Sample? Find(string id) => Samples.FirstOrDefault(s => s.Id == id);

    private static string TitleOf(string code, string fallback)
    {
        var first = code.Split('\n', 2)[0].Trim();
        return first.StartsWith("//", StringComparison.Ordinal) ? first[2..].Trim() : fallback;
    }
}
