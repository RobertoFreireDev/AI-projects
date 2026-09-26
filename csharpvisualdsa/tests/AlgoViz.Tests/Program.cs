using System.Diagnostics;
using AlgoViz.Tests;

// Plain console test runner: runs every test, prints failures, exits non-zero if any failed.
var tests = new List<(string Name, Func<Task> Body)>();
tests.AddRange(ApiOpTests.All());
tests.AddRange(EngineTests.All());
tests.AddRange(ValidatorTests.All());
tests.AddRange(RewriterTests.All());
tests.AddRange(RunnerTests.All());
tests.AddRange(SampleTests.All());

var filter = args.FirstOrDefault();
var failed = 0;
var run = 0;
var total = Stopwatch.StartNew();
foreach (var (name, body) in tests)
{
    if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
    run++;
    var watch = Stopwatch.StartNew();
    try
    {
        await body();
        Console.WriteLine($"  ok    {name} ({watch.ElapsedMilliseconds} ms)");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"  FAIL  {name}");
        Console.WriteLine($"        {(ex is CheckException ? ex.Message : ex.ToString())}");
    }
}

Console.WriteLine();
Console.WriteLine($"{run - failed}/{run} passed in {total.Elapsed.TotalSeconds:0.0} s");
return failed == 0 ? 0 : 1;
