using AlgoViz.Core.Runner;
using AlgoViz.Core.Visual;

namespace AlgoViz.Tests;

/// <summary>Every built-in sample (embedded from src/AlgoViz.Web/Samples) runs cleanly under the limits.</summary>
public static class SampleTests
{
    public static IEnumerable<(string, Func<Task>)> All()
    {
        var assembly = typeof(SampleTests).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.StartsWith("Samples.", StringComparison.Ordinal)).Order().ToList();
        yield return ("samples: there are built-in samples", () =>
        {
            Check.True(names.Count >= 18, $"found {names.Count}");
            return Task.CompletedTask;
        });
        foreach (var name in names)
        {
            yield return ($"sample: {name["Samples.".Length..]}", async () =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                var code = await reader.ReadToEndAsync();
                Check.True(code.StartsWith("// ", StringComparison.Ordinal), "the first line is a title comment");
                var result = await new ScriptRunner().RunAsync(code);
                Check.True(result.CompileErrors.Count == 0, string.Join(" | ", result.CompileErrors));
                Check.True(result.Error is null, result.Error?.Message);
                Check.True(result.Ops.Count is > 0 and < 1000, $"{result.Ops.Count} ops");
                Check.True(result.Ops.All(o => o.Line > 0), "every op has a source line");
                var playback = new Playback(result.Ops);
                Check.Equal(result.Ops.Count, playback.Seek(playback.LastFrame).Index);
            });
        }
    }
}
