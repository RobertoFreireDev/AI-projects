using System.Globalization;
using AlgoViz.Core.Visual;

namespace AlgoViz.Web.Components.Views;

/// <summary>CSS class names for marks and transient highlights, and invariant number formatting for SVG.</summary>
public static class VizCss
{
    public static string Mark(string? color) => color is null ? "" : "mark-" + color;

    public static string Highlight(HighlightKind? kind) => kind switch
    {
        HighlightKind.Read => "hl-read",
        HighlightKind.Compare => "hl-compare",
        HighlightKind.Visit => "hl-visit",
        HighlightKind.Change => "hl-change",
        _ => "",
    };

    /// <summary>Formats a coordinate with '.' decimals regardless of the server culture.</summary>
    public static string N(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Translate(double x, double y) => $"transform: translate({N(x)}px, {N(y)}px)";

    /// <summary>Width of a box that fits the longest text (13px monospace ≈ 7.8px per char), at least <paramref name="min"/>.</summary>
    public static int BoxWidth(IEnumerable<string?> texts, int min) =>
        Math.Max(min, (int)Math.Ceiling(texts.Select(t => t?.Length ?? 0).DefaultIfEmpty(0).Max() * 7.8 + 14));

    /// <summary>The highlight on an index-based element (A or B), if any.</summary>
    public static HighlightKind? AtIndex(IReadOnlyList<Highlight> highlights, int index) =>
        highlights.FirstOrDefault(h => h.NodeId is null && (h.A == index || h.B == index))?.Kind;

    public static HighlightKind? AtNode(IReadOnlyList<Highlight> highlights, int id) =>
        highlights.FirstOrDefault(h => h.NodeId == id)?.Kind;
}
