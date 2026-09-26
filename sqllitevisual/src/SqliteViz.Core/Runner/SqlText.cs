namespace SqliteViz.Core.Runner;

/// <summary>Small lexical helpers. Never used for splitting or for safety decisions.</summary>
public static class SqlText
{
    /// <summary>Index of the first character that is not whitespace or part of a comment, or <c>text.Length</c>.</summary>
    public static int SkipTrivia(string text, int start = 0, int end = -1)
    {
        if (end < 0) end = text.Length;
        var i = start;
        while (i < end)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '-' && i + 1 < end && text[i + 1] == '-')
            {
                var nl = text.IndexOf('\n', i, end - i);
                i = nl < 0 ? end : nl + 1;
                continue;
            }
            if (c == '/' && i + 1 < end && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, end - i - 2, StringComparison.Ordinal);
                i = close < 0 ? end : close + 2;
                continue;
            }
            break;
        }
        return i;
    }

    /// <summary>The leading keywords of a statement (after comments), upper-cased. Used for kind detection and friendly messages.</summary>
    public static IReadOnlyList<string> LeadingKeywords(string sql, int count)
    {
        var words = new List<string>(count);
        var i = 0;
        while (words.Count < count)
        {
            i = SkipTrivia(sql, i);
            var start = i;
            while (i < sql.Length && (char.IsAsciiLetter(sql[i]) || sql[i] == '_')) i++;
            if (i == start) break;
            words.Add(sql[start..i].ToUpperInvariant());
        }
        return words;
    }

    public static string FirstKeyword(string sql) => LeadingKeywords(sql, 1) is [var w] ? w : "";

    /// <summary>Number of lines the text spans (at least 1).</summary>
    public static int CountLines(string text)
    {
        var n = 1;
        foreach (var c in text) if (c == '\n') n++;
        return n;
    }

    /// <summary>Quotes an identifier for host-issued SQL: wraps in double quotes and doubles embedded quotes.</summary>
    public static string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
