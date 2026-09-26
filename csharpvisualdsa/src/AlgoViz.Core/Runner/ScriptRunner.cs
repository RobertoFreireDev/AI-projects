using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using AlgoViz.Api;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace AlgoViz.Core.Runner;

/// <summary>
/// Compiles and runs a user script: parse → validate → rewrite (guards) → emit → run on a dedicated thread with
/// a recorder → unload. A guardrail for a personal practice tool, not a security sandbox.
/// </summary>
public sealed class ScriptRunner(RunnerOptions? options = null)
{
    public const string ScriptPath = "script.cs";

    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Text;
        global using AlgoViz.Api;
        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly SyntaxTree GlobalUsingsTree =
        CSharpSyntaxTree.ParseText(GlobalUsings, ParseOptions, "globals.cs", Encoding.UTF8);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.ConsoleApplication,
        // Debug keeps JIT IL-offset mapping exact, so runtime errors point at the right line.
        optimizationLevel: OptimizationLevel.Debug,
        nullableContextOptions: NullableContextOptions.Disable,
        allowUnsafe: false,
        concurrentBuild: false,
        deterministic: true);

    public RunnerOptions Options { get; } = options ?? new RunnerOptions();

    public async Task<RunResult> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(code)) return new RunResult([], [], null, stopwatch.Elapsed);

        var compiled = Compile(code);
        if (compiled.Assembly is null)
            return new RunResult([], compiled.Diagnostics, null, stopwatch.Elapsed);

        var (ops, error) = await ExecuteAsync(compiled.Assembly, compiled.Pdb, cancellationToken);
        return new RunResult(ops, compiled.Diagnostics, error, stopwatch.Elapsed);
    }

    /// <summary>Compiles and validates without running; for tests and warm-up.</summary>
    internal static CompileOutput Compile(string code)
    {
        var userTree = CSharpSyntaxTree.ParseText(code, ParseOptions, ScriptPath, Encoding.UTF8);
        var compilation = CreateCompilation(userTree);

        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Location.SourceTree == userTree && d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(d => ToScriptDiagnostic(d) with { Message = ScriptValidator.FriendlyMessage(d) ?? d.GetMessage() })
            .ToList();

        var model = compilation.GetSemanticModel(userTree);
        diagnostics.AddRange(new ScriptValidator(model).Validate(userTree.GetRoot()));
        diagnostics = [.. diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column)];
        if (diagnostics.Any(d => d.Severity == ScriptSeverity.Error)) return new CompileOutput(diagnostics, null, default);

        var rewritten = Rewrite(userTree, model);
        var guarded = compilation.ReplaceSyntaxTree(userTree, rewritten);

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = guarded.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        if (!emit.Success)
        {
            diagnostics.AddRange(emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => ToScriptDiagnostic(d) with { Message = "Internal error after adding guards: " + d.GetMessage() }));
            return new CompileOutput(diagnostics, null, default);
        }
        return new CompileOutput(diagnostics, pe.ToArray(), [.. pdb.ToArray()]);
    }

    /// <summary>The user's tree and its guarded rewrite, compiled exactly like a real run; for tests.</summary>
    internal static (SyntaxTree Original, SyntaxTree Rewritten) ParseAndRewrite(string code)
    {
        var userTree = CSharpSyntaxTree.ParseText(code, ParseOptions, ScriptPath, Encoding.UTF8);
        return (userTree, Rewrite(userTree, CreateCompilation(userTree).GetSemanticModel(userTree)));
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree userTree) =>
        CSharpCompilation.Create(
            "AlgoVizScript_" + Guid.NewGuid().ToString("N"),
            [GlobalUsingsTree, userTree],
            ScriptReferences.All,
            CompilationOptions);

    /// <summary>Injects the loop and call guards. The result keeps every user token on its original line.</summary>
    internal static SyntaxTree Rewrite(SyntaxTree tree, SemanticModel model)
    {
        var root = new GuardRewriter(model).Visit(tree.GetRoot());
        return CSharpSyntaxTree.ParseText(root.ToFullString(), ParseOptions, ScriptPath, Encoding.UTF8);
    }

    private async Task<(IReadOnlyList<VizOp>, RuntimeError?)> ExecuteAsync(
        byte[] image, ImmutableArray<byte> pdb, CancellationToken cancellationToken)
    {
        var context = new ScriptLoadContext();
        Assembly assembly;
        using (var peStream = new MemoryStream(image))
        using (var pdbStream = new MemoryStream(pdb.ToArray()))
            assembly = context.LoadFromStream(peStream, pdbStream);

        var locator = new ScriptLineLocator(assembly, pdb);
        var recorder = new Recorder(Options.MaxOps, Options.MaxIterations) { LineResolver = locator.FindLine };
        var done = new TaskCompletionSource<RuntimeError?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entryPoint = assembly.EntryPoint ?? throw new InvalidOperationException("The script has no entry point.");

        var thread = new Thread(() => done.SetResult(RunEntryPoint(entryPoint, recorder, locator)), Options.StackSize)
        {
            IsBackground = true,
            Name = "AlgoViz script",
        };
        thread.Start();

        // Unload once the script thread is really done, even if that happens after we stop waiting.
        _ = done.Task.ContinueWith(_ =>
        {
            locator.Dispose();
            context.Unload();
        }, TaskScheduler.Default);

        var finished = await WaitAsync(done.Task, Options.Timeout, cancellationToken);
        if (!finished)
        {
            recorder.Cancel();
            finished = await WaitAsync(done.Task, Options.CancelGrace, CancellationToken.None);
        }

        var ops = recorder.Snapshot();
        if (!finished)
            return (ops, new RuntimeError(TimeoutMessage + " The script could not be stopped.", ops.Count > 0 ? ops[^1].Line : 0));

        var error = await done.Task;
        if (error is not null && error.Line == 0 && ops.Count > 0) error = error with { Line = ops[^1].Line };
        return (ops, error);
    }

    private string TimeoutMessage => $"Time limit exceeded ({Options.Timeout.TotalSeconds:0.#} s).";

    private RuntimeError? RunEntryPoint(MethodInfo entryPoint, Recorder recorder, ScriptLineLocator locator)
    {
        using var scope = Recorder.Begin(recorder);
        try
        {
            object?[]? args = entryPoint.GetParameters().Length == 0 ? null : [Array.Empty<string>()];
            entryPoint.Invoke(null, BindingFlags.DoNotWrapExceptions, null, args, null);
            return null;
        }
        catch (Exception ex)
        {
            var line = locator.FindLine(new StackTrace(ex, false)) ?? 0;
            return new RuntimeError(Describe(ex), line);
        }
    }

    private string Describe(Exception ex) => ex switch
    {
        VizTimeoutException => TimeoutMessage,
        VizLimitException => ex.Message,
        InsufficientExecutionStackException => "Recursion too deep: the call stack is exhausted.",
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };

    private static async Task<bool> WaitAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static ScriptDiagnostic ToScriptDiagnostic(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        return new ScriptDiagnostic(
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            d.GetMessage(),
            d.Severity == DiagnosticSeverity.Error ? ScriptSeverity.Error : ScriptSeverity.Warning);
    }

    internal sealed record CompileOutput(IReadOnlyList<ScriptDiagnostic> Diagnostics, byte[]? Assembly, ImmutableArray<byte> Pdb);
}
