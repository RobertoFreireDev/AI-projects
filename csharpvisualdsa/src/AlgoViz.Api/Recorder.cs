using System.Diagnostics;

namespace AlgoViz.Api;

/// <summary>
/// Collects the ops of one run. One instance per run, installed on the run's thread; scripts are
/// single-threaded, so the <see cref="ThreadStaticAttribute"/> slot isolates concurrent runs.
/// </summary>
internal sealed class Recorder(int maxOps = Recorder.DefaultMaxOps, long maxIterations = Recorder.DefaultMaxIterations)
{
    public const int DefaultMaxOps = 5_000;
    public const long DefaultMaxIterations = 10_000_000;
    public const int MaxElements = 2_500;

    [ThreadStatic]
    private static Recorder? active;

    private readonly List<VizOp> ops = [];
    private readonly Lock gate = new();
    private int nextStructureId;
    private int nextNodeId;
    private long iterations;
    private volatile bool cancelled;

    public int MaxOps { get; } = maxOps;
    public long MaxIterations { get; } = maxIterations;

    /// <summary>Maps a stack trace to the innermost user-script line; installed by the runner.</summary>
    public Func<StackTrace, int?>? LineResolver { get; init; }

    public static Recorder? Active => active;

    public static Recorder Current =>
        active ?? throw new InvalidOperationException("The Viz API is only available while a script runs.");

    public int OpCount
    {
        get { lock (gate) return ops.Count; }
    }

    /// <summary>Installs <paramref name="recorder"/> on the current thread until the scope is disposed.</summary>
    public static Scope Begin(Recorder recorder)
    {
        active = recorder;
        return new Scope();
    }

    public int NewStructureId() => ++nextStructureId;

    public int NewNodeId() => ++nextNodeId;

    public void Append(OpKind kind, int structureId, int? nodeId, int? a, int? b, string? value, string? text, int line)
    {
        ThrowIfCancelled();
        lock (gate)
        {
            if (ops.Count >= MaxOps)
                throw new VizLimitException($"Operation limit reached ({MaxOps:N0} ops). Use a smaller input or fewer Viz calls.");
            ops.Add(new VizOp(ops.Count, kind, structureId, nodeId, a, b, value, text, line));
        }
    }

    public IReadOnlyList<VizOp> Snapshot()
    {
        lock (gate) return ops.ToArray();
    }

    public void Cancel() => cancelled = true;

    public void Enter()
    {
        ThrowIfCancelled();
        System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
    }

    public void Tick()
    {
        ThrowIfCancelled();
        if (++iterations > MaxIterations)
            throw new VizLimitException($"Loop limit reached ({MaxIterations:N0} iterations). Is there an infinite loop?");
    }

    /// <summary>Line of the user code calling into the API; for members that can't take [CallerLineNumber].</summary>
    public static int CallerLine() =>
        active?.LineResolver?.Invoke(new StackTrace(1, false)) ?? 0;

    private void ThrowIfCancelled()
    {
        if (cancelled) throw new VizTimeoutException();
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose() => active = null;
    }
}
