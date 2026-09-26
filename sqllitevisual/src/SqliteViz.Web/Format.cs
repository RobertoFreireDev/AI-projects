using System.Globalization;

namespace SqliteViz.Web;

internal static class Format
{
    public static string Duration(TimeSpan elapsed) => elapsed.TotalMilliseconds switch
    {
        < 1 => $"{elapsed.TotalMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)} ms",
        < 1000 => $"{elapsed.TotalMilliseconds.ToString("0.#", CultureInfo.InvariantCulture)} ms",
        _ => $"{elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture)} s",
    };

    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static string Plural(long n, string word) => $"{Count(n)} {word}{(n == 1 ? "" : "s")}";
}
