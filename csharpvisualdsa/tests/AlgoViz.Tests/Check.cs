using System.Runtime.CompilerServices;

namespace AlgoViz.Tests;

public sealed class CheckException(string message) : Exception(message);

/// <summary>A tiny assertion helper: every failure throws <see cref="CheckException"/>.</summary>
public static class Check
{
    public static void True(bool condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw new CheckException(message ?? $"Expected true: {expression}");
    }

    public static void Equal<T>(T expected, T actual, string? message = null, [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new CheckException($"{message ?? expression}: expected <{expected}> but got <{actual}>");
    }

    public static void Contains(string expected, string? actual, [CallerArgumentExpression(nameof(actual))] string? expression = null)
    {
        if (actual is null || !actual.Contains(expected, StringComparison.Ordinal))
            throw new CheckException($"{expression}: expected to contain <{expected}> but got <{actual}>");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new CheckException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new CheckException($"Expected {typeof(TException).Name} but nothing was thrown");
    }
}
