using System.Diagnostics;
using System.Reflection;

namespace SqliteViz.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public sealed class AssertionException(string message) : Exception(message);

/// <summary>Tiny assert helper: no test framework, no packages.</summary>
public static class Assert
{
    public static void True(bool condition, string message = "expected true")
    {
        if (!condition) throw new AssertionException(message);
    }

    public static void False(bool condition, string message = "expected false") => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string? context = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{(context is null ? "" : context + ": ")}expected <{expected}> but got <{actual}>");
    }

    public static T NotNull<T>(T? value, string message = "expected a value") where T : class =>
        value ?? throw new AssertionException(message);

    public static void Null(object? value, string message = "expected null")
    {
        if (value is not null) throw new AssertionException($"{message}, got <{value}>");
    }

    public static void Contains(string expectedPart, string? actual)
    {
        if (actual is null || !actual.Contains(expectedPart, StringComparison.Ordinal))
            throw new AssertionException($"expected <{actual}> to contain <{expectedPart}>");
    }
}

/// <summary>Finds [Test] methods, runs them (optionally filtered by name), prints results, returns an exit code.</summary>
public static class TestRunner
{
    public static int Run(string? filter)
    {
        var tests = typeof(TestRunner).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
            .Select(m => (Name: $"{m.DeclaringType!.Name}.{m.Name}", Method: m))
            .Where(t => filter is null || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name)
            .ToList();

        var failed = 0;
        var total = Stopwatch.StartNew();
        foreach (var (name, method) in tests)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = method.Invoke(null, null);
                if (result is Task task) task.GetAwaiter().GetResult();
                Console.WriteLine($"  PASS  {name} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception e)
            {
                failed++;
                var inner = e is TargetInvocationException { InnerException: { } ie } ? ie : e;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  FAIL  {name}: {inner.Message}");
                Console.ResetColor();
                if (inner is not AssertionException) Console.WriteLine(inner.StackTrace);
            }
        }
        Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed in {total.ElapsedMilliseconds} ms");
        return failed == 0 && tests.Count > 0 ? 0 : 1;
    }
}
