namespace AlgoViz.Web.Services;

public sealed record Sample(string Id, string Title, string Code);

/// <summary>Built-in example scripts, embedded from Samples/*.csx. The first line comment is the title.</summary>
/// <remarks>The documentation tour is kept out of <see cref="Samples"/>; the page shows it in its own panel.</remarks>
public sealed class SampleCatalog
{
    private const string DocumentationId = "00-documentation";

    public SampleCatalog()
    {
        var assembly = typeof(SampleCatalog).Assembly;
        const string prefix = "AlgoViz.Web.Samples.";
        Sample[] all = [.. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".csx", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                var code = reader.ReadToEnd().ReplaceLineEndings("\n");
                var id = name[prefix.Length..^".csx".Length];
                return new Sample(id, TitleOf(code, id), code);
            })];
        Documentation = all.FirstOrDefault(s => s.Id == DocumentationId);
        Samples = [.. all.Where(s => s.Id != DocumentationId)];
    }

    public IReadOnlyList<Sample> Samples { get; }

    public Sample? Documentation { get; }

    public Sample? Find(string id) => Samples.FirstOrDefault(s => s.Id == id);

    private static string TitleOf(string code, string fallback)
    {
        var first = code.Split('\n', 2)[0].Trim();
        return first.StartsWith("//", StringComparison.Ordinal) ? first[2..].Trim() : fallback;
    }
}
