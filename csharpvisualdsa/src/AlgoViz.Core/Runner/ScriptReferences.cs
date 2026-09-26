using AlgoViz.Api;
using Microsoft.CodeAnalysis;

namespace AlgoViz.Core.Runner;

/// <summary>The metadata references scripts compile against, built once from the running framework.</summary>
internal static class ScriptReferences
{
    // System.Console is deliberately absent.
    private static readonly string[] FrameworkAssemblies =
        ["System.Private.CoreLib", "System.Runtime", "System.Collections", "System.Linq"];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> all = new(Build);

    public static IReadOnlyList<MetadataReference> All => all.Value;

    private static IReadOnlyList<MetadataReference> Build()
    {
        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator);
        var paths = FrameworkAssemblies
            .Select(name => tpa.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name)
                ?? throw new InvalidOperationException($"Framework assembly {name} not found."))
            .Append(typeof(Viz).Assembly.Location);
        return [.. paths.Select(p => MetadataReference.CreateFromFile(p))];
    }
}
