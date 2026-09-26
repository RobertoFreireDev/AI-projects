using AlgoViz.Api;

namespace AlgoViz.Core.Visual;

/// <summary>A one-line human description of an op, shown under the player controls.</summary>
public static class OpText
{
    public static string Describe(VizOp? op, Frame frame)
    {
        if (op is null) return "Start";
        var s = frame.Find(op.StructureId);
        var name = s?.Label ?? "?";
        string Node(int? id) => id is { } n ? NodeValue(s, n) : "∅";

        return (op.Kind, s) switch
        {
            (OpKind.Create, _) => $"Create {(StructureKind)(op.A ?? 0)} '{op.Text}'",
            (OpKind.Log, _) => $"Log: {op.Text}",
            (OpKind.Var, _) => $"{op.Text} = {op.Value}",
            (OpKind.Step, _) => $"Step: {op.Text}",
            (OpKind.Read, GridState) => $"Read {name}[{op.A},{op.B}] → {op.Value}",
            (OpKind.Write, GridState) => $"Set {name}[{op.A},{op.B}] = {op.Value}",
            (OpKind.Mark, GridState) => $"Mark {name}[{op.A},{op.B}] {op.Text}",
            (OpKind.Unmark, GridState) => $"Unmark {name}[{op.A},{op.B}]",
            (OpKind.Read, MapState { Kind: StructureKind.Map }) =>
                op.Value is null ? $"{name} has key {op.Text}? {(op.NodeId is null ? "no" : "yes")}" : $"Get {name}[{op.Text}] → {op.Value}",
            (OpKind.Write, MapState) => $"Set {name}[{op.Text}] = {op.Value}",
            (OpKind.Remove, MapState { Kind: StructureKind.Map }) => $"Remove key {op.Text} from {name}{Missing(op)}",
            (OpKind.Read, MapState) => $"{name} contains {op.Value}? {(op.NodeId is null ? "no" : "yes")}",
            (OpKind.Insert, MapState) => $"Add {op.Value} to {name}",
            (OpKind.Remove, MapState) => $"Remove {op.Value} from {name}{Missing(op)}",
            (OpKind.Read, GraphState) => $"Neighbors of {op.NodeId} in {name}",
            (OpKind.Insert, GraphState) => $"Add node {op.NodeId} to {name}",
            (OpKind.Remove, GraphState) => $"Remove node {op.NodeId} from {name}",
            (OpKind.Link, GraphState) => $"Edge {op.A}–{op.B}{(op.Value is null ? "" : $" (w {op.Value})")} in {name}",
            (OpKind.Unlink, GraphState) => $"Remove edge {op.A}–{op.B} from {name}",
            (OpKind.Visit, GraphState) => $"Visit {op.NodeId} in {name}",
            (OpKind.Mark, GraphState) => op.NodeId is null ? $"Mark edge {op.A}–{op.B} {op.Text}" : $"Mark node {op.NodeId} {op.Text}",
            (OpKind.Unmark, GraphState) => op.NodeId is null ? $"Unmark edge {op.A}–{op.B}" : $"Unmark node {op.NodeId}",
            (OpKind.Visit, _) => $"Visit {Node(op.NodeId)} in {name}",
            (OpKind.Insert, TreeState) => op.A is null ? $"Set root of {name} to {op.Value}" : $"Add {op.Value} under {Node(op.A)}",
            (OpKind.Insert, LinkedListState) => op.A is null ? $"Add {op.Value} at the head of {name}" : $"Add {op.Value} after {Node(op.A)}",
            (OpKind.Write, TreeState or LinkedListState) => $"Set node value to {op.Value}",
            (OpKind.Remove, TreeState) => $"Remove a subtree from {name}",
            (OpKind.Remove, LinkedListState) => $"Remove a node from {name}",
            (OpKind.Move, _) => op.A is null ? $"Make {Node(op.NodeId)} the root of {name}" : $"Move {Node(op.NodeId)} under {Node(op.A)}",
            (OpKind.Unlink, TreeState) => $"Detach the {(op.B == 0 ? "left" : "right")} child of {Node(op.NodeId)}",
            (OpKind.Link, LinkedListState) =>
                op.NodeId is null ? $"Head of {name} → {Node(op.A)}" : $"{Node(op.NodeId)}.Next → {Node(op.A)}",
            (OpKind.Mark, TreeState) => $"Mark {Node(op.NodeId)} {op.Text}",
            (OpKind.Unmark, TreeState) => $"Unmark {Node(op.NodeId)}",
            (OpKind.Read, _) => $"Read {name}[{op.A}] → {op.Value}",
            (OpKind.Write, _) => $"Set {name}[{op.A}] = {op.Value}",
            (OpKind.Compare, _) => $"Compare {name}[{op.A}] {op.Value} {name}[{op.B}]",
            (OpKind.Swap, _) => $"Swap {name}[{op.A}] ↔ {name}[{op.B}]",
            (OpKind.Insert, _) => $"Insert {op.Value} at {name}[{op.A}]",
            (OpKind.Remove, _) when op.Text == "*" => $"Clear {name}",
            (OpKind.Remove, _) => op.A is null ? $"Remove {op.Value} from {name}: not found" : $"Remove {name}[{op.A}]",
            (OpKind.Push, _) => $"Push {op.Value} onto {name}",
            (OpKind.Pop, _) => $"Pop {op.Value} from {name}",
            (OpKind.Enqueue, _) => $"Enqueue {op.Value} into {name}",
            (OpKind.Dequeue, _) => $"Dequeue {op.Value} from {name}",
            (OpKind.Mark, _) when op.Text == "view:tree" => $"Show {name} as a tree",
            (OpKind.Mark, _) => $"Mark {name}[{op.A}] {op.Text}",
            (OpKind.Unmark, _) => $"Unmark {name}[{op.A}]",
            (OpKind.Pointer, _) => op.A is null ? $"Remove pointer {op.Text}" : $"{op.Text} → {op.A}",
            _ => $"{op.Kind} {name}",
        };
    }

    private static string Missing(VizOp op) => op.NodeId is null ? ": not found" : "";

    private static string NodeValue(StructureState? s, int id) => s switch
    {
        TreeState t when t.Nodes.TryGetValue(id, out var n) => n.Value,
        LinkedListState l when l.Nodes.TryGetValue(id, out var n) => n.Value,
        _ => "a node",
    };
}
