# CLAUDE.md — AlgoViz

Guidance for Claude (and any AI agent) working in this repository. Read this fully before writing code.

## 1. What this is

AlgoViz is a **Blazor Server** app for practicing algorithms and data structures in C# and **seeing them run**.

1. The user writes a C# script in an **Ace editor** (top of the page).
2. The server compiles and runs the script.
3. The script manipulates data structures only through a small **Viz API** of atomic operations. Every call appends one entry to an **operation list**.
4. The server returns that list to the UI (below the editor), which **replays it as an animation**: play (0.5 s per frame), pause, step forward/back, restart, jump to end, scrub.

The goal is breadth: arrays, lists, stacks, queues, linked lists, n-ary trees, binary trees, graphs, grids, maps, and sets, so the user can practice building, searching, traversing, filtering, sorting, and so on.

## 2. Non-negotiables

- **.NET 10**, Blazor Web App with **Interactive Server** render mode. No WebAssembly.
- **No external NuGet packages**, with ONE approved exception: `Microsoft.CodeAnalysis.CSharp` (Roslyn, Microsoft's own compiler), referenced by `AlgoViz.Core` only. This is a deliberate, approved decision: in-process compilation keeps each run fast, instead of shelling out to `dotnet build` (seconds per run). Pin it to the latest stable version compatible with .NET 10. Do not add anything else (no UI kits, no test frameworks, no JSON libs, no `Microsoft.CodeAnalysis.CSharp.Scripting`).
- **Ace editor** is plain JavaScript, vendored into `wwwroot/lib/ace/` (not a NuGet package, not a CDN at runtime).
- **Scripts are single-threaded.** Threads, tasks, async/await, `Parallel`, `lock`, and `System.Threading` are rejected at validation time. The recorder relies on this.
- **Recording and rendering are fully separated.** Scripts produce a flat, serializable list of operations. They never touch UI. The UI never runs user code.
- **The state engine is pure and deterministic**: applying ops `0..N` to an empty state always yields the same frame `N`.
- Minimal JavaScript: only Ace interop. All rendering is Razor + SVG + CSS.

## 3. Solution layout

```
AlgoViz.sln
src/
  AlgoViz.Api/        # The Viz API user scripts call. Zero dependencies. Recorder lives here.
  AlgoViz.Core/       # Script runner (Roslyn), validator, rewriter, state engine, layout. Refs Api.
  AlgoViz.Web/        # Blazor Server UI. Refs Core.
    wwwroot/lib/ace/  # vendored Ace build
    wwwroot/js/editor.js
    Samples/          # built-in example scripts (.csx text files, embedded resources)
tests/
  AlgoViz.Tests/      # Plain console app with a tiny assert helper (no xUnit — no packages).
```

`AlgoViz.Api` must stay tiny and dependency-free: it is the only project user code can reference.

## 4. Request flow

```
Editor (Ace) --Run--> ScriptRunner.RunAsync(code)            [Core, server]
                        1. Parse (top-level statements)
                        2. Validate (semantic allowlist)
                        3. Rewrite (inject guards)
                        4. Emit to memory, load in collectible AssemblyLoadContext
                        5. Execute entry point on a dedicated thread with Recorder active
                        6. Unload context
                      <-- RunResult { Ops, CompileErrors, RuntimeError, Elapsed }
Player component: StateEngine replays Ops -> Frame -> SVG components render it
```

There is no HTTP API: in Blazor Server the component calls `ScriptRunner` directly (it is a scoped/singleton service). Keep it that way.

## 5. The script contract

Users write **top-level statements** (C# 9+). Local functions and class declarations after the statements are allowed. Everything from `AlgoViz.Api` is available through a global `using`.

```csharp
// Bubble sort
var arr = Viz.Array("arr", 5, 1, 4, 2, 8);
for (int i = 0; i < arr.Length - 1; i++)
    for (int j = 0; j < arr.Length - 1 - i; j++)
        if (arr.Compare(j, j + 1) > 0)
            arr.Swap(j, j + 1);
```

```csharp
// BST insert
var bst = Viz.BinaryTree<int>("bst");
foreach (var v in new[] { 8, 3, 10, 1, 6, 14 })
{
    if (bst.Root is null) { bst.SetRoot(v); continue; }
    var n = bst.Root;
    while (true)
    {
        n.Visit();
        if (v < n.Value) { if (n.Left is null) { n.SetLeft(v); break; } n = n.Left; }
        else             { if (n.Right is null) { n.SetRight(v); break; } n = n.Right; }
    }
}
```

## 6. Viz API (AlgoViz.Api)

Rules for every API member:

- Each public mutating or "observable" method records **exactly one op** and takes `[CallerLineNumber] int line = 0` as its last parameter so the editor can highlight the source line during playback. Exception: factories whose last parameter is `params` (or where a trailing `int line` would be ambiguous, e.g. `Viz.Array<T>(label, size)`) get their line from `Recorder.CallerLine()`, which maps the caller's stack frame through the script's PDB.
- **Silent** (no op recorded): `Length`, `Count`, `IsEmpty`, `Root`, `Value`, `Left`, `Right`, `Children`, `Parent`, `Next`, `Head`, `Tail`, `IsLeaf`, `Rows`, `Cols`, `InBounds`, `Keys`, `Items`, `Nodes`, `HasNode`, `HasEdge`, `Weight`, `ToArray`, `IndexOf`, and other navigation/size members. Otherwise simple loops flood the timeline. To show a visit, the user calls `Visit()`.
- **Recorded reads**: `Get`, `Compare`, `Contains`, `Peek`, `ContainsKey`, `Neighbors`. They render as a transient highlight.
- Every structure is created with a display label and gets an internal integer id. Every node gets a stable integer id (never reused in one run).
- Values are recorded as display strings via `Viz.Format(value)` (`ToString()`, truncated to 12 chars, `null` → `∅`). The engine never needs real values.
- Invalid operations (index out of range, pop on empty) throw a normal .NET exception, which becomes a runtime error on that line.

| Structure | Factory | Operations |
|---|---|---|
| Array | `Viz.Array("a", params T[])`, `Viz.Array<T>("a", size)` | `Get(i)`, `Set(i,v)`, `Swap(i,j)`, `Compare(i,j)` → int, `Mark(i, color)`, `Unmark(i)`, `Pointer(name, i)`, `RemovePointer(name)` |
| List | `Viz.List<T>("l", params T[])` | Array ops + `Add(v)`, `Insert(i,v)`, `RemoveAt(i)`, `Remove(v)`, `Clear()` |
| Stack | `Viz.Stack<T>("s")` | `Push(v)`, `Pop()`, `Peek()` |
| Queue | `Viz.Queue<T>("q")` | `Enqueue(v)`, `Dequeue()`, `Peek()` |
| Deque | `Viz.Deque<T>("d")` | `PushFront`, `PushBack`, `PopFront`, `PopBack`, `PeekFront`, `PeekBack` |
| LinkedList | `Viz.LinkedList<T>("ll")` | `AddFirst(v)`, `AddLast(v)`, `InsertAfter(node,v)`, `Remove(node)`, `SetNext(node, other)` (for reversal), `SetHead(node)`; node `.Value`, `.Next`, `Visit()`, `SetValue(v)` |
| Tree (n-ary) | `Viz.Tree<T>("t")` | `SetRoot(v)`; node: `AddChild(v)`, `InsertChild(i,v)`, `SetValue(v)` (rename), `Remove()` (subtree), `MoveTo(newParent, index?)`, `Visit()`, `Mark(color)`, `Unmark()` |
| BinaryTree | `Viz.BinaryTree<T>("b")` | `SetRoot(v)`, `SetRoot(node)`; node: `SetLeft(v)`, `SetRight(v)`, `SetLeft(node)`/`SetRight(node)` (reattach existing subtree, for rotations; `null` detaches), `RemoveLeft()`, `RemoveRight()`, `Remove()`, `SetValue(v)`, `Visit()`, `Mark(color)`, `Unmark()` |
| Graph | `Viz.Graph("g", directed: false)` | `AddNode(id, x?, y?)`, `RemoveNode(id)`, `AddEdge(a,b, weight?)`, `RemoveEdge(a,b)`, `Neighbors(id)`, `Visit(id)`, `MarkNode(id,color)`, `UnmarkNode(id)`, `MarkEdge(a,b,color)`, `UnmarkEdge(a,b)` |
| Grid | `Viz.Grid<T>("m", rows, cols, fill?)`, `Viz.Grid<T>("m", T[][] rows)`, `Viz.CharGrid("m", params string[] rows)` | `Get(r,c)`, `Set(r,c,v)`, `Mark(r,c,color)`, `Unmark(r,c)` |
| Map | `Viz.Map<K,V>("h")` | `Set(k,v)`, `Get(k)`, `ContainsKey(k)`, `Remove(k)` |
| Set | `Viz.Set<T>("set")` | `Add(v)`, `Contains(v)`, `Remove(v)` |
| Global | `Viz` | `Log(message)`, `Var(name, value)` (watch panel), `Step(label)` (named checkpoint shown on the timeline) |

Colors are a small enum (`VizColor.Active, Done, Warn, Path, Muted`) mapped to CSS variables, never raw hex.

BinaryTree detaching: replacing a child (`SetLeft(v)` on an occupied slot, `SetLeft(node)`, `SetLeft(null)`, `SetRoot(node)`) never deletes the displaced subtree; it becomes **detached** (drawn faded) until it is reattached or `Remove()`d, because the script may still hold a reference (e.g. mid-rotation). Attaching a node under itself or its descendant throws.

Size limit: a structure holds at most 2,500 elements/nodes/cells (`VizLimitException`).

Array/List view option (phase 4): `arr.ShowAsTree()` renders the array as a complete binary tree too (heaps).

### Recorder

- `Recorder` is `[ThreadStatic]`. The runner creates one per run on the execution thread. Scripts are single-threaded, so this is safe and isolates concurrent users.
- `Recorder.Append(op)` enforces `MaxOps` (default 5,000) and throws `VizLimitException` beyond it.
- Also exposes the internal guard hooks `Viz.__Enter()` and `Viz.__Tick()` used by the rewriter (section 8). Mark them `[EditorBrowsable(Never)]` and exclude them from autocomplete.
- `Recorder` is `internal` (visible to Core and Tests via `InternalsVisibleTo`), so scripts can't touch it.

## 7. Operation model

```csharp
public enum OpKind { Create, Read, Write, Compare, Swap, Insert, Remove, Move,
                     Push, Pop, Enqueue, Dequeue, Visit, Mark, Unmark,
                     Pointer, Link, Unlink, Log, Var, Step }

public sealed record VizOp(
    int Index,          // position in the list
    OpKind Kind,
    int StructureId,
    int? NodeId,        // node/cell/element id when relevant
    int? A, int? B,     // indices, child position, target node id, edge endpoint, etc.
    string? Value,      // formatted value
    string? Text,       // label, color name, pointer name, log text
    int Line);          // source line (1-based) in the user's script
```

Keep ops **small and flat**. Do not add structure-specific subclasses. Document the meaning of `A`/`B`/`Text` per `OpKind` + structure in `OpKind.cs` XML comments.
`Create` carries `(int)StructureKind` in `A`; initial values are packed into `Value` with `VizOp.Pack` (unit-separator joined).

## 8. Script execution (AlgoViz.Core/Runner)

1. **Parse** with `CSharpParseOptions(LanguageVersion.Latest)`, `OutputKind.ConsoleApplication`, and a prepended global using for `AlgoViz.Api` added as a separate syntax tree (so user line numbers are untouched).
2. **References**: built once and cached. Take the needed assemblies from `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")` (`System.Runtime`, `System.Private.CoreLib`, `System.Collections`, `System.Linq`, `System.Console`-excluded) plus `AlgoViz.Api.dll`. No reference-assembly packages.
3. **Validate** with the `SemanticModel`, not string matching. Walk every symbol and reject anything outside an **allowlist of namespaces**: `System` (minus banned types), `System.Collections.Generic`, `System.Linq`, `System.Text`, `AlgoViz.Api`. Banned types in `System`: `Environment`, `AppDomain`, `Activator`, `GC`, `Console`, `Type`, and anything reflection-related. Also reject `unsafe`, `extern`, `goto`, `async`/`await`, `lock`, `dynamic`, and attributes. Error messages must be friendly: e.g. `Console` → "Use Viz.Log(...) instead."
   Also rejected: `stackalloc`/`fixed`/pointers (stack overflow), finalizers (run on another thread), `record` types (compiler-generated `ToString`/`Equals` recurse without guards), and `__makeref`-style keywords. Names that fail to resolve because their assembly isn't referenced (`Console`, `Thread`, `File`, …) get the same friendly messages.
4. **Rewrite** with a `CSharpSyntaxRewriter`:
   - Insert `Viz.__Tick();` as the first statement of every loop body (`for`, `foreach`, `while`, `do`).
   - Insert `Viz.__Enter();` as the first statement of every method, local function, and block-bodied lambda. `__Enter` calls `RuntimeHelpers.EnsureSufficientExecutionStack()` so deep recursion throws a catchable exception instead of a fatal stack overflow that would kill the server.
   - `__Tick` counts iterations (cap 10M) and checks a cancellation flag set by the timeout.
   - Expression bodies (`=> expr` on methods, local functions, accessors, properties, lambdas) are converted to blocks so they get `__Enter` too; otherwise expression-bodied recursion would bypass the guard.
   - **Inserted statements must carry no newline trivia** so `[CallerLineNumber]` and diagnostics still match the user's lines. A test asserts this.
5. **Emit** to a `MemoryStream` with `OptimizationLevel.Debug` plus an in-memory portable PDB, and load into a new **collectible `AssemblyLoadContext`** whose `Load` override returns `null` (so `AlgoViz.Api` and the framework resolve to the default context and the recorder types are shared). Debug is deliberate: with Release the JIT drops IL-offset mapping and every runtime error pointed at the first statement. `ScriptLineLocator` maps stack frames to lines through the PDB (System.Reflection.Metadata, in-box).
6. **Execute** the entry point on a dedicated `Thread` (explicit stack size, e.g. 8 MB). Wait up to **5 s**; on timeout set the cancel flag so the next `__Tick`/`__Enter` throws. If the thread still hasn't stopped after a 1 s grace (stuck inside a BCL call), return a timeout error anyway; the context unloads when the thread finally exits.
7. **Unload** the context after the run. Never keep references to user types.

```csharp
public sealed record RunResult(
    IReadOnlyList<VizOp> Ops,
    IReadOnlyList<ScriptDiagnostic> CompileErrors,   // line, column, message, severity
    RuntimeError? Error,                              // message, line (from the innermost user frame or last op)
    TimeSpan Elapsed);
```

On a runtime error, **keep the ops recorded so far**: the player shows them and ends on an error frame.

This is a guardrail for a personal practice tool, not a security sandbox. Do not claim otherwise in the UI or docs, and do not deploy it publicly without real isolation (separate process/container).

## 9. State engine and playback (AlgoViz.Core/Visual)

- `Frame` is an immutable snapshot of every structure, plus transient highlights, watch vars, log lines, and the current source line.
- `StateEngine.Apply(Frame, VizOp) → Frame` is pure.
- **Transient** highlights (Read, Compare, Visit) exist only in the frame of that op. **Persistent** marks (Mark/Unmark, Pointer) stay until changed.
- Frame 0 is the empty state; frame N is after op N-1. Total frames = ops + 1.
- Stepping back: keep a checkpoint every 50 frames; restore the nearest checkpoint and replay forward. Never mutate a checkpoint.

**Player controls**: Restart ⏮, Step back ◀, Play/Pause ▶/⏸, Step forward ▶|, End ⏭, a scrubber slider, frame counter `12 / 87`, speed selector (0.25×, 0.5×, 1× = 500 ms, 2×, 4×).
Keyboard: Space play/pause, ←/→ step, Home/End, Ctrl+Enter run.
Use `PeriodicTimer` in the component, update via `InvokeAsync(StateHasChanged)`, and dispose the timer when the component is disposed or a new run starts.

## 10. Rendering (AlgoViz.Web)

- One Razor component per structure kind (`ArrayView`, `StackView`, `TreeView`, `GraphView`, ...), all rendering **SVG**.
- Use `@key` with stable node/element ids so Blazor updates existing elements instead of recreating them; animate position/color with **CSS transitions** (`transform`, `fill`) at ~300 ms, shorter than the frame delay.
- Layout lives in Core as pure functions, not in components:
  - Trees: simple tidy layout (leaves get consecutive x slots, parents centered over children, y by depth).
  - Graphs: use user-given `x,y` when provided, otherwise circular layout.
  - Stack vertical, queue horizontal, linked list horizontal with arrows.
- Each structure panel shows its label. Pointers (`i`, `j`, `lo`, `hi`) render as small labeled arrows under the cells.
- Side panel: watch variables (`Viz.Var`) and log (`Viz.Log`). Timeline shows `Viz.Step` labels as markers.
- Dark/light via CSS variables. Layout must work on a laptop screen: editor on top (resizable height), visualization below.

## 11. Editor (Ace)

- `wwwroot/js/editor.js` as an ES module loaded via `IJSObjectReference`. Functions: `init`, `getValue`, `setValue`, `setAnnotations(diagnostics)`, `highlightLine(line)`, `clearHighlight`, `dispose`.
- Mode `ace/mode/csharp`. Compile errors → Ace annotations + a list under the editor.
- During playback, highlight the op's `Line`.
- Custom completer with the Viz API names and short signatures (static list generated from `AlgoViz.Api` at build/startup via reflection in the host, not in user code).
- Samples dropdown loads built-in scripts. Remember the last script per browser with Ace/JS `localStorage` (only JS touches it).

## 12. Built-in samples

Samples live in `src/AlgoViz.Web/Samples/NN-name.csx`; the first line (`// Title`) is the dropdown title. The test project embeds the same files. `00-documentation.csx` is not in the dropdown: the page renders it as a Documentation aside beside the editor (sections split at `// ===== Title =====` lines, trailing comments shown under each code line), with an "Open in editor" button to run it.

Documentation (`00-documentation.csx`, a runnable tour of every Viz API member; keep it in sync when the API changes) · Bubble, selection, insertion, merge, quick, heap sort · binary search · two pointers · balanced parentheses (stack) · BFS with a queue · reverse linked list · BST insert/search/delete · pre/in/post/level-order traversal · n-ary tree build/rename/remove · graph DFS/BFS · Dijkstra · flood fill on a grid · two-sum with a map.
Every sample must run under the limits and is exercised by a test.

## 13. Build phases

1. Solution skeleton, `AlgoViz.Api` with Array + recorder, runner end-to-end, Ace editor, raw op list shown as text.
2. State engine + player controls + `ArrayView` animation + line highlighting.
3. List, Stack, Queue, Deque, LinkedList, Map, Set views.
4. Tree and BinaryTree + tree layout; `ShowAsTree()`.
5. Graph and Grid.
6. Samples, autocomplete, watch/log panels, polish, keyboard shortcuts.

Finish and test each phase before starting the next.

## 14. Conventions

- Nullable enabled, warnings as errors, file-scoped namespaces, `sealed` by default, records for data.
- No static mutable state except the `[ThreadStatic]` recorder and cached metadata references.
- Build and test: `dotnet build` then `dotnet run --project tests/AlgoViz.Tests` (optional name filter as the first argument). Run the app with `dotnet run --project src/AlgoViz.Web`.
- Tests (plain console runner, exits non-zero on failure) cover: every API op → expected `VizOp`; engine apply/replay determinism; step back == replay from 0; validator rejects each banned construct; rewriter keeps line numbers; timeout, max ops, and deep recursion end with a `RuntimeError` instead of hanging or crashing; every sample runs.
- Keep commits and changes small; update this file when an API or rule changes.

## 15. Don'ts

- Don't add NuGet packages beyond `Microsoft.CodeAnalysis.CSharp`.
- Don't run user code outside the runner pipeline or without the guards.
- Don't make the UI depend on user types or call back into user code.
- Don't use JS for rendering or animation logic.
- Don't record silent navigation properties as ops.
- Don't use Roslyn Scripting (`CSharpScript`); use a normal compilation with top-level statements.
