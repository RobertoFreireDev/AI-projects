using System.Collections.Immutable;
using AlgoViz.Api;

namespace AlgoViz.Core.Visual;

/// <summary>
/// An immutable snapshot after <see cref="Index"/> ops: every structure, the transient highlights of the op that
/// produced it, watch variables and log lines. Frame 0 is the empty state.
/// </summary>
public sealed record Frame(
    int Index,
    ImmutableList<StructureState> Structures,
    ImmutableList<Highlight> Highlights,
    ImmutableList<WatchVar> Vars,
    ImmutableList<LogLine> Log,
    VizOp? Op,
    int NextElementId)
{
    public static Frame Empty { get; } = new(0, [], [], [], [], null, 1);

    /// <summary>Source line of the op that produced this frame; 0 for frame 0.</summary>
    public int Line => Op?.Line ?? 0;

    public StructureState? Find(int structureId) => Structures.FirstOrDefault(s => s.Id == structureId);

    public IEnumerable<Highlight> HighlightsFor(int structureId) => Highlights.Where(h => h.StructureId == structureId);
}

public enum HighlightKind
{
    Read,
    Compare,
    Visit,
    Change,
}

/// <summary>A highlight that only exists in the frame of one op. Fields mirror the op's NodeId/A/B.</summary>
public sealed record Highlight(int StructureId, HighlightKind Kind, int? NodeId, int? A, int? B);

public sealed record WatchVar(string Name, string Value, int ChangedAt);

public sealed record LogLine(string Text, int Line);

public abstract record StructureState(int Id, string Label, StructureKind Kind);

/// <summary>An element with an engine-assigned id, so views can animate it as it moves.</summary>
public sealed record Element(int Id, string Value);

/// <summary>Array, List, Stack, Queue and Deque. Marks and pointers are keyed by index.</summary>
public sealed record SequenceState(
    int Id, string Label, StructureKind Kind,
    ImmutableList<Element> Items,
    ImmutableDictionary<int, string> Marks,
    ImmutableSortedDictionary<string, int> Pointers,
    bool ShowAsTree) : StructureState(Id, Label, Kind);

public sealed record LinkedNodeState(int Id, string Value, int? Next);

/// <summary><see cref="Order"/> is the display order: the chain from the head, then detached nodes.</summary>
public sealed record LinkedListState(
    int Id, string Label,
    ImmutableDictionary<int, LinkedNodeState> Nodes,
    ImmutableList<int> Order,
    int? Head) : StructureState(Id, Label, StructureKind.LinkedList);

/// <summary>Binary nodes always have two child slots (null = empty); n-ary nodes only have real children.</summary>
public sealed record TreeNodeState(int Id, string Value, int? Parent, ImmutableList<int?> Children);

public sealed record TreeState(
    int Id, string Label, StructureKind Kind,
    ImmutableDictionary<int, TreeNodeState> Nodes,
    int? Root,
    ImmutableList<int> Detached,
    ImmutableDictionary<int, string> Marks) : StructureState(Id, Label, Kind)
{
    public bool Binary => Kind == StructureKind.BinaryTree;
}

public sealed record GraphNodeState(int Id, int? X, int? Y);

public sealed record GraphEdgeState(int From, int To, string? Weight);

public sealed record GraphState(
    int Id, string Label, bool Directed,
    ImmutableList<GraphNodeState> Nodes,
    ImmutableList<GraphEdgeState> Edges,
    ImmutableDictionary<int, string> NodeMarks,
    ImmutableDictionary<(int, int), string> EdgeMarks) : StructureState(Id, Label, StructureKind.Graph)
{
    /// <summary>Canonical key of an edge: (a, b) when directed, otherwise ordered.</summary>
    public (int, int) EdgeKey(int a, int b) => Directed || a <= b ? (a, b) : (b, a);
}

/// <summary>Cells and marks are row-major, index = row * Cols + col.</summary>
public sealed record GridState(
    int Id, string Label, int Rows, int Cols,
    ImmutableArray<string> Cells,
    ImmutableDictionary<int, string> Marks) : StructureState(Id, Label, StructureKind.Grid);

/// <summary>A map entry (<see cref="Key"/> set) or a set member (<see cref="Key"/> null).</summary>
public sealed record EntryState(int Id, string? Key, string Value);

public sealed record MapState(int Id, string Label, StructureKind Kind, ImmutableList<EntryState> Entries)
    : StructureState(Id, Label, Kind);
