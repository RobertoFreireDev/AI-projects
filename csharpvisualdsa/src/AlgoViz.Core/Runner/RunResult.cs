using AlgoViz.Api;

namespace AlgoViz.Core.Runner;

public enum ScriptSeverity
{
    Error,
    Warning,
}

/// <summary>A compile or validation message at a 1-based line and column of the user's script.</summary>
public sealed record ScriptDiagnostic(int Line, int Column, string Message, ScriptSeverity Severity);

/// <summary>A failure while the script ran; <see cref="Line"/> is 1-based, 0 when unknown.</summary>
public sealed record RuntimeError(string Message, int Line);

public sealed record RunResult(
    IReadOnlyList<VizOp> Ops,
    IReadOnlyList<ScriptDiagnostic> CompileErrors,
    RuntimeError? Error,
    TimeSpan Elapsed)
{
    public bool HasCompileErrors => CompileErrors.Any(d => d.Severity == ScriptSeverity.Error);
}

public sealed record RunnerOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long to wait for the script thread to notice cancellation after the timeout.</summary>
    public TimeSpan CancelGrace { get; init; } = TimeSpan.FromSeconds(1);

    public int MaxOps { get; init; } = 5_000;

    public long MaxIterations { get; init; } = 10_000_000;

    public int StackSize { get; init; } = 8 * 1024 * 1024;
}
