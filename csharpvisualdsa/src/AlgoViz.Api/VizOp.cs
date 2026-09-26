namespace AlgoViz.Api;

/// <summary>One recorded operation. See <see cref="OpKind"/> for the meaning of each field per kind.</summary>
public sealed record VizOp(
    int Index,
    OpKind Kind,
    int StructureId,
    int? NodeId,
    int? A, int? B,
    string? Value,
    string? Text,
    int Line)
{
    private const char Separator = '\u001F';

    /// <summary>Packs formatted values into a single <see cref="Value"/> string.</summary>
    public static string Pack(IEnumerable<string> values) => string.Join(Separator, values);

    /// <summary>Reverses <see cref="Pack"/>. Null or empty means no values.</summary>
    public static string[] Unpack(string? packed) =>
        string.IsNullOrEmpty(packed) ? [] : packed.Split(Separator);
}
