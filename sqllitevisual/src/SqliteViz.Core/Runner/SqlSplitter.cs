using SQLitePCL;

namespace SqliteViz.Core.Runner;

/// <summary>One statement of a script, positioned in the user's editor.</summary>
/// <param name="Sql">Statement text from its first token to its terminating ';' (or end of script).</param>
/// <param name="Offset">Character offset of the first token in the script.</param>
/// <param name="Line">1-based line of the first token.</param>
/// <param name="Column">1-based column of the first token.</param>
public sealed record SqlPiece(string Sql, int Offset, int Line, int Column)
{
    public int EndLine => Line + SqlText.CountLines(Sql) - 1;
}

/// <summary>
/// Splits a script into statements with SQLite's own tokenizer (<c>sqlite3_complete</c>): a piece ends at the
/// first ';' where the text so far is a complete statement. That handles string literals, quoted identifiers,
/// comments, and <c>CREATE TRIGGER ... BEGIN ...; END;</c> bodies, and unlike a prepare-based split it does not
/// need earlier statements to have run (a later statement may reference a table an earlier one creates).
/// Each piece is then prepared on its own with <c>sqlite3_prepare_v3</c> by the runner.
/// </summary>
public static class SqlSplitter
{
    /// <param name="baseLine">Line in the editor where <paramref name="script"/> starts (for running a selection).</param>
    /// <param name="baseColumn">Column in the editor where the first line of <paramref name="script"/> starts.</param>
    public static IReadOnlyList<SqlPiece> Split(string script, int baseLine = 1, int baseColumn = 1)
    {
        SqliteEnvironment.EnsureInitialized();
        var pieces = new List<SqlPiece>();
        var pos = 0;
        var positions = new LinePositions(script, baseLine, baseColumn);
        while (true)
        {
            var start = SqlText.SkipTrivia(script, pos);
            // Stray semicolons between statements are empty statements; skip them.
            while (start < script.Length && script[start] == ';') start = SqlText.SkipTrivia(script, start + 1);
            if (start >= script.Length) break;

            var end = FindEnd(script, start);
            var sql = script[start..end].TrimEnd();
            var (line, column) = positions.At(start);
            pieces.Add(new SqlPiece(sql, start, line, column));
            pos = end;
        }
        return pieces;
    }

    /// <summary>The statement under the cursor (1-based line/column), or the nearest one before it.</summary>
    public static SqlPiece? StatementAt(string script, int line, int column)
    {
        var pieces = Split(script);
        if (pieces.Count == 0) return null;
        var cursor = new LinePositions(script, 1, 1).OffsetOf(line, column);
        SqlPiece? best = null;
        foreach (var p in pieces)
        {
            if (p.Offset <= cursor) best = p;
            else break;
        }
        return best ?? pieces[0];
    }

    private static int FindEnd(string script, int start)
    {
        var semi = start;
        while (true)
        {
            semi = NextSemicolon(script, semi);
            if (semi < 0) return script.Length;
            if (raw.sqlite3_complete(script[start..(semi + 1)]) != 0) return semi + 1;
            semi++;
        }
    }

    private static int NextSemicolon(string script, int from) =>
        from >= script.Length ? -1 : script.IndexOf(';', from);

    /// <summary>Maps character offsets to 1-based editor lines and columns.</summary>
    internal sealed class LinePositions
    {
        private readonly List<int> _lineStarts = [0];
        private readonly int _baseLine;
        private readonly int _baseColumn;

        public LinePositions(string text, int baseLine, int baseColumn)
        {
            for (var i = 0; i < text.Length; i++)
                if (text[i] == '\n') _lineStarts.Add(i + 1);
            _baseLine = baseLine;
            _baseColumn = baseColumn;
        }

        public (int Line, int Column) At(int offset)
        {
            var idx = _lineStarts.BinarySearch(offset);
            if (idx < 0) idx = ~idx - 1;
            var column = offset - _lineStarts[idx] + 1;
            if (idx == 0) column += _baseColumn - 1;
            return (_baseLine + idx, column);
        }

        public int OffsetOf(int line, int column)
        {
            var idx = Math.Clamp(line - _baseLine, 0, _lineStarts.Count - 1);
            return _lineStarts[idx] + Math.Max(0, column - 1);
        }
    }
}
