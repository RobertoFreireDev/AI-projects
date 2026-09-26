namespace AlgoViz.Api;

/// <summary>Colors for persistent marks. Rendered through CSS variables, never raw hex.</summary>
public enum VizColor
{
    Active,
    Done,
    Warn,
    Path,
    Muted,
}

internal static class VizColorNames
{
    public static string Name(this VizColor color) => color switch
    {
        VizColor.Active => "active",
        VizColor.Done => "done",
        VizColor.Warn => "warn",
        VizColor.Path => "path",
        VizColor.Muted => "muted",
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}
