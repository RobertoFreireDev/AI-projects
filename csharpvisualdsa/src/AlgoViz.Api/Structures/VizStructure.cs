namespace AlgoViz.Api;

/// <summary>Base of every visualized structure: a display label and an internal id.</summary>
public abstract class VizStructure
{
    private protected VizStructure(string label, StructureKind kind, int? size, string? initial, int line)
    {
        ArgumentNullException.ThrowIfNull(label);
        var recorder = Recorder.Current;
        Label = label;
        Id = recorder.NewStructureId();
        recorder.Append(OpKind.Create, Id, null, (int)kind, size, initial, label, line);
    }

    public string Label { get; }

    internal int Id { get; }

    internal void Record(OpKind kind, int line, int? node = null, int? a = null, int? b = null, string? value = null, string? text = null) =>
        Recorder.Current.Append(kind, Id, node, a, b, value, text, line);

    internal static int NewNodeId() => Recorder.Current.NewNodeId();

    internal static void CheckSize(int count)
    {
        if (count > Recorder.MaxElements)
            throw new VizLimitException($"Too many elements ({count:N0}); a structure can hold at most {Recorder.MaxElements:N0}.");
    }

    internal static string Pack<T>(IEnumerable<T> values) => VizOp.Pack(values.Select(v => Viz.Format(v)));

    public override string ToString() => Label;
}
