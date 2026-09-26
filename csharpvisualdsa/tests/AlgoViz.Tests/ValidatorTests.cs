using AlgoViz.Core.Runner;

namespace AlgoViz.Tests;

/// <summary>The validator rejects each banned construct with a friendly message, and accepts normal code.</summary>
public static class ValidatorTests
{
    private static readonly (string Name, string Code, string Message)[] Banned =
    [
        ("Console", "Console.WriteLine(1);", "Use Viz.Log"),
        ("System.Console", "System.Console.WriteLine(1);", "not available"),
        ("Environment", "var x = Environment.MachineName;", "Environment is not available"),
        ("Environment.Exit", "Environment.Exit(0);", "Environment is not available"),
        ("AppDomain", "var d = AppDomain.CurrentDomain;", "AppDomain"),
        ("Activator", "var o = Activator.CreateInstance<object>();", "Reflection"),
        ("GC", "GC.Collect();", "GC is not available"),
        ("typeof", "var t = typeof(int);", "Reflection"),
        ("GetType", "var n = 1.GetType();", "Reflection"),
        ("reflection namespace", "var m = typeof(int).GetMethods();", "Reflection"),
        ("Delegate.DynamicInvoke", "Func<int> f = () => 1; f.DynamicInvoke();", "Reflection"),
        ("Thread", "var t = new System.Threading.Thread(() => { });", "single-threaded"),
        ("Task", "var t = Task.Run(() => 1);", "single-threaded"),
        ("async/await", "async System.Threading.Tasks.Task F() { await System.Threading.Tasks.Task.Yield(); }", "single-threaded"),
        ("lock", "var o = new object(); lock (o) { }", "single-threaded"),
        ("unsafe", "unsafe { int x = 1; int* p = &x; }", "unsafe"),
        ("stackalloc", "Span<int> s = stackalloc int[4];", "stackalloc"),
        ("goto", "start: goto start;", "goto"),
        ("dynamic", "dynamic d = 1;", "dynamic"),
        ("attribute", "[Obsolete] void F() { }", "Attributes"),
        ("extern", "static extern void F();", "extern"),
        ("file I/O", "System.IO.File.ReadAllText(\"x\");", "I/O"),
        ("diagnostics", "System.Diagnostics.Process.Start(\"ls\");", "Diagnostics"),
        ("record", "record P(int X);", "Records"),
        ("finalizer", "class C { ~C() { } }", "Finalizers"),
        ("non-generic collections", "var h = new System.Collections.Hashtable();", "not available"),
        ("using directive", "using System.IO;\nViz.Log(1);", "I/O"),
        ("__makeref", "int i = 0; var r = __makeref(i);", "__makeref"),
    ];

    public static IEnumerable<(string, Func<Task>)> All() =>
    [
        .. Banned.Select(b => ($"validator: rejects {b.Name}", (Func<Task>)(() => Rejects(b.Code, b.Message)))),
        ("validator: accepts normal code", AcceptsNormalCode),
    ];

    private static Task Rejects(string code, string message)
    {
        var output = ScriptRunner.Compile(code);
        Check.True(output.Assembly is null, $"'{code}' should not compile");
        var errors = output.Diagnostics.Where(d => d.Severity == ScriptSeverity.Error).ToList();
        Check.True(errors.Any(d => d.Message.Contains(message, StringComparison.OrdinalIgnoreCase)),
            $"expected an error containing '{message}', got: {string.Join(" | ", errors.Select(e => e.Message))}");
        Check.True(errors.All(d => d.Line >= 1));
        return Task.CompletedTask;
    }

    private static Task AcceptsNormalCode()
    {
        var output = ScriptRunner.Compile("""
            var list = new List<int> { 3, 1, 2 };
            var sorted = list.OrderBy(x => x).Select(x => x * 2).ToList();
            var sb = new StringBuilder();
            foreach (var (k, v) in new Dictionary<string, int> { ["a"] = 1 }) sb.Append(k).Append(v);
            var pq = new PriorityQueue<string, int>();
            var set = new HashSet<(int, int)>();
            int Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);
            Viz.Log($"{Math.Max(1, 2)} {string.Join(",", sorted)} {sb} {Fib(10)} {DateTime.Now.Year > 0}");
            var p = new Point(1, 2);
            Viz.Log(p);
            class Point(int x, int y)
            {
                public int X => x;
                public int Y { get; } = y;
                public override string ToString() => $"({X}, {Y})";
            }
            """);
        Check.True(output.Assembly is not null, string.Join(" | ", output.Diagnostics));
        return Task.CompletedTask;
    }
}
