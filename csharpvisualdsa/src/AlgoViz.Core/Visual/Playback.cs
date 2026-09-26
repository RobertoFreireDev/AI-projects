using AlgoViz.Api;

namespace AlgoViz.Core.Visual;

/// <summary>
/// Random access to the frames of a run. Keeps an immutable checkpoint every <see cref="CheckpointInterval"/>
/// frames; seeking restores the nearest checkpoint at or before the target and replays forward.
/// </summary>
public sealed class Playback
{
    public const int CheckpointInterval = 50;

    private readonly IReadOnlyList<VizOp> ops;
    private readonly List<Frame> checkpoints = [];

    public Playback(IReadOnlyList<VizOp> ops)
    {
        this.ops = ops;
        var frame = Frame.Empty;
        checkpoints.Add(frame);
        foreach (var op in ops)
        {
            frame = StateEngine.Apply(frame, op);
            if (frame.Index % CheckpointInterval == 0) checkpoints.Add(frame);
        }
        Current = Frame.Empty;
        Steps = [.. ops.Where(o => o.Kind == OpKind.Step).Select(o => new StepMarker(o.Index + 1, o.Text ?? ""))];
    }

    /// <summary>Ops + 1: frame 0 is the empty state, frame N is after op N-1.</summary>
    public int FrameCount => ops.Count + 1;

    public int LastFrame => ops.Count;

    public Frame Current { get; private set; }

    public bool AtEnd => Current.Index == LastFrame;

    public IReadOnlyList<StepMarker> Steps { get; }

    public Frame Seek(int index)
    {
        index = Math.Clamp(index, 0, LastFrame);
        if (index == Current.Index) return Current;
        if (index == Current.Index + 1)
            return Current = StateEngine.Apply(Current, ops[Current.Index]);

        var frame = checkpoints[index / CheckpointInterval];
        // Replaying forward from the current frame is cheaper when it's between the checkpoint and the target.
        if (Current.Index < index && Current.Index > frame.Index) frame = Current;
        for (var i = frame.Index; i < index; i++) frame = StateEngine.Apply(frame, ops[i]);
        return Current = frame;
    }

    public Frame StepForward() => Seek(Current.Index + 1);

    public Frame StepBack() => Seek(Current.Index - 1);
}

public sealed record StepMarker(int Frame, string Label);
