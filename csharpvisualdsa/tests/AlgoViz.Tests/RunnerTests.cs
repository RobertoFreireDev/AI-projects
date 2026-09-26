using System.Diagnostics;
using AlgoViz.Api;
using AlgoViz.Core.Runner;

namespace AlgoViz.Tests;

/// <summary>Runs end with a RuntimeError instead of hanging or crashing, and errors point at the right line.</summary>
public static class RunnerTests
{
    public static IEnumerable<(string, Func<Task>)> All() =>
    [
        ("runner: runs a script and records ops", RunsScript),
        ("runner: empty script", EmptyScript),
        ("runner: timeout ends an infinite loop", Timeout),
        ("runner: timeout ends a loop that swallows exceptions", TimeoutWithCatchAll),
        ("runner: iteration limit", IterationLimit),
        ("runner: max ops keeps the recorded ops", MaxOps),
        ("runner: deep recursion is a runtime error", DeepRecursion),
        ("runner: deep recursion through a property", DeepRecursionProperty),
        ("runner: runtime error line and partial ops", RuntimeErrorLine),
        ("runner: compile errors have lines", CompileErrors),
        ("runner: concurrent runs are isolated", Concurrent),
    ];

    private static async Task RunsScript()
    {
        var result = await new ScriptRunner().RunAsync("""
            // Bubble sort
            var arr = Viz.Array("arr", 5, 1, 4, 2, 8);
            for (int i = 0; i < arr.Length - 1; i++)
                for (int j = 0; j < arr.Length - 1 - i; j++)
                    if (arr.Compare(j, j + 1) > 0)
                        arr.Swap(j, j + 1);
            """);
        Check.True(result.Error is null && !result.HasCompileErrors);
        Check.Equal(OpKind.Create, result.Ops[0].Kind);
        Check.Equal(2, result.Ops[0].Line, "params factory line comes from the stack trace");
        Check.True(result.Ops.Where(o => o.Kind == OpKind.Compare).All(o => o.Line == 5));
        Check.True(result.Ops.Where(o => o.Kind == OpKind.Swap).All(o => o.Line == 6));
        Check.Equal(10, result.Ops.Count(o => o.Kind == OpKind.Compare));
    }

    private static async Task EmptyScript()
    {
        var result = await new ScriptRunner().RunAsync("   \n");
        Check.True(result.Ops.Count == 0 && result.Error is null && result.CompileErrors.Count == 0);
    }

    private static async Task Timeout()
    {
        var runner = new ScriptRunner(new RunnerOptions { Timeout = TimeSpan.FromSeconds(1), MaxIterations = long.MaxValue });
        var watch = Stopwatch.StartNew();
        var result = await runner.RunAsync("Viz.Log(\"start\");\nwhile (true) { }");
        Check.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
        Check.Contains("Time limit exceeded", result.Error?.Message);
        Check.Equal(2, result.Error!.Line);
        Check.Equal(1, result.Ops.Count, "ops recorded before the timeout are kept");
    }

    private static async Task TimeoutWithCatchAll()
    {
        var runner = new ScriptRunner(new RunnerOptions { Timeout = TimeSpan.FromSeconds(1), MaxIterations = long.MaxValue });
        var result = await runner.RunAsync("""
            while (true)
            {
                try { while (true) { } }
                catch { }
            }
            """);
        Check.Contains("Time limit exceeded", result.Error?.Message);
        Check.True(!result.Error!.Message.Contains("could not be stopped"), "the guard outside the try stopped it");
    }

    private static async Task IterationLimit()
    {
        var result = await new ScriptRunner().RunAsync("long n = 0;\nfor (;;) { n++; }");
        Check.Contains("Loop limit reached", result.Error?.Message);
        Check.Equal(2, result.Error!.Line);
    }

    private static async Task MaxOps()
    {
        var result = await new ScriptRunner().RunAsync("var a = Viz.Array(\"a\", 1);\nwhile (true) a.Get(0);");
        Check.Contains("Operation limit", result.Error?.Message);
        Check.Equal(Recorder.DefaultMaxOps, result.Ops.Count);
        Check.Equal(2, result.Error!.Line);
    }

    private static async Task DeepRecursion()
    {
        var result = await new ScriptRunner().RunAsync("Viz.Log(1);\nint F(int n) => F(n + 1) + 1;\nViz.Log(F(0));");
        Check.Contains("Recursion too deep", result.Error?.Message);
        Check.Equal(2, result.Error!.Line);
        Check.Equal(1, result.Ops.Count);
    }

    private static async Task DeepRecursionProperty()
    {
        var result = await new ScriptRunner().RunAsync("""
            Viz.Log(new Loop().Value);
            class Loop
            {
                public int Value => Value + 1;
            }
            """);
        Check.Contains("Recursion too deep", result.Error?.Message);
    }

    private static async Task RuntimeErrorLine()
    {
        var result = await new ScriptRunner().RunAsync("""
            var a = Viz.Array("a", 1, 2);
            a.Get(1);
            void Boom(int i)
            {
                a.Get(i);
            }
            Boom(7);
            """);
        Check.Contains("IndexOutOfRangeException", result.Error?.Message);
        Check.Equal(5, result.Error!.Line);
        Check.Equal(2, result.Ops.Count);

        var thrown = await new ScriptRunner().RunAsync("Viz.Log(1);\n\nthrow new InvalidOperationException(\"boom\");");
        Check.Equal("InvalidOperationException: boom", thrown.Error?.Message);
        Check.Equal(3, thrown.Error!.Line);
    }

    private static async Task CompileErrors()
    {
        var result = await new ScriptRunner().RunAsync("var x = 1;\nvar y = x +;\n");
        Check.True(result.HasCompileErrors);
        Check.True(result.CompileErrors.Any(d => d.Line == 2 && d.Severity == ScriptSeverity.Error));
        Check.Equal(0, result.Ops.Count);
    }

    private static async Task Concurrent()
    {
        var runner = new ScriptRunner();
        var tasks = Enumerable.Range(1, 6).Select(n => runner.RunAsync($$"""
            var a = Viz.Array<int>("a{{n}}", {{n}});
            for (int i = 0; i < {{n}}; i++) a.Set(i, {{n}});
            """)).ToList();
        var results = await Task.WhenAll(tasks);
        for (var n = 1; n <= 6; n++)
        {
            var r = results[n - 1];
            Check.True(r.Error is null, r.Error?.Message);
            Check.Equal(n + 1, r.Ops.Count, $"run {n}");
            Check.Equal($"a{n}", r.Ops[0].Text);
            Check.True(r.Ops.Skip(1).All(o => o.Value == n.ToString()));
        }
    }
}
