namespace SqliteViz.Core.Completions;

/// <param name="Meta">Short label shown next to the completion ("keyword", "function", a signature...).</param>
public sealed record Completion(string Value, string Caption, string Meta, string? Doc = null);

/// <summary>Static SQLite keywords and built-in functions for the editor completer (read-only metadata).</summary>
public static class SqlCompletions
{
    private static readonly string[] Keywords =
    [
        "ABORT", "ACTION", "ADD", "AFTER", "ALL", "ALTER", "ALWAYS", "ANALYZE", "AND", "AS", "ASC", "AUTOINCREMENT",
        "BEFORE", "BEGIN", "BETWEEN", "BY", "CASCADE", "CASE", "CAST", "CHECK", "COLLATE", "COLUMN", "COMMIT",
        "CONFLICT", "CONSTRAINT", "CREATE", "CROSS", "CURRENT", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP",
        "DEFAULT", "DEFERRABLE", "DEFERRED", "DELETE", "DESC", "DISTINCT", "DO", "DROP", "EACH", "ELSE", "END",
        "ESCAPE", "EXCEPT", "EXCLUDE", "EXCLUSIVE", "EXISTS", "EXPLAIN", "FAIL", "FILTER", "FIRST", "FOLLOWING", "FOR",
        "FOREIGN", "FROM", "FULL", "GENERATED", "GLOB", "GROUP", "GROUPS", "HAVING", "IF", "IGNORE", "IMMEDIATE", "IN",
        "INDEX", "INDEXED", "INITIALLY", "INNER", "INSERT", "INSTEAD", "INTERSECT", "INTO", "IS", "ISNULL", "JOIN",
        "KEY", "LAST", "LEFT", "LIKE", "LIMIT", "MATCH", "MATERIALIZED", "NATURAL", "NO", "NOT", "NOTHING", "NOTNULL",
        "NULL", "NULLS", "OF", "OFFSET", "ON", "OR", "ORDER", "OTHERS", "OUTER", "OVER", "PARTITION", "PLAN", "PRAGMA",
        "PRECEDING", "PRIMARY", "QUERY", "RAISE", "RANGE", "RECURSIVE", "REFERENCES", "REGEXP", "REINDEX", "RELEASE",
        "RENAME", "REPLACE", "RESTRICT", "RETURNING", "RIGHT", "ROLLBACK", "ROW", "ROWID", "ROWS", "SAVEPOINT",
        "SELECT", "SET", "STORED", "STRICT", "TABLE", "TEMP", "TEMPORARY", "THEN", "TIES", "TO", "TRANSACTION",
        "TRIGGER", "UNBOUNDED", "UNION", "UNIQUE", "UPDATE", "USING", "VALUES", "VIEW", "VIRTUAL", "WHEN", "WHERE",
        "WINDOW", "WITH", "WITHOUT",
        "INTEGER", "REAL", "TEXT", "BLOB", "NUMERIC", "ANY",
    ];

    private static readonly (string Name, string Signature, string Doc)[] Functions =
    [
        // Core scalar
        ("abs", "abs(X)", "Absolute value"), ("changes", "changes()", "Rows changed by the last statement"),
        ("char", "char(X1, X2, ...)", "String from Unicode code points"), ("coalesce", "coalesce(X, Y, ...)", "First non-NULL argument"),
        ("concat", "concat(X, ...)", "Concatenate, NULLs ignored"), ("concat_ws", "concat_ws(SEP, X, ...)", "Concatenate with separator"),
        ("format", "format(FORMAT, ...)", "printf-style formatting"), ("glob", "glob(X, Y)", "GLOB match"),
        ("hex", "hex(X)", "Hex encoding"), ("ifnull", "ifnull(X, Y)", "X if not NULL, else Y"), ("iif", "iif(COND, X, Y)", "Inline if"),
        ("instr", "instr(X, Y)", "1-based position of Y in X"), ("last_insert_rowid", "last_insert_rowid()", "Rowid of the last insert"),
        ("length", "length(X)", "Characters in text, bytes in blob"), ("like", "like(X, Y)", "LIKE match"),
        ("likelihood", "likelihood(X, P)", "Planner hint"), ("likely", "likely(X)", "Planner hint: usually true"),
        ("lower", "lower(X)", "Lower case"), ("ltrim", "ltrim(X, Y)", "Trim left"), ("max", "max(X, Y, ...)", "Largest argument / aggregate max"),
        ("min", "min(X, Y, ...)", "Smallest argument / aggregate min"), ("nullif", "nullif(X, Y)", "NULL if X = Y"),
        ("octet_length", "octet_length(X)", "Bytes in the text encoding"), ("printf", "printf(FORMAT, ...)", "Same as format"),
        ("quote", "quote(X)", "SQL literal of X"), ("random", "random()", "Random 64-bit integer"), ("randomblob", "randomblob(N)", "N random bytes"),
        ("replace", "replace(X, Y, Z)", "Replace every Y in X with Z"), ("round", "round(X, N)", "Round to N digits"),
        ("rtrim", "rtrim(X, Y)", "Trim right"), ("sign", "sign(X)", "-1, 0 or +1"), ("soundex", "soundex(X)", "Soundex code"),
        ("sqlite_version", "sqlite_version()", "Library version"), ("substr", "substr(X, START, LEN)", "Substring (1-based)"),
        ("substring", "substring(X, START, LEN)", "Alias of substr"), ("total_changes", "total_changes()", "Rows changed since open"),
        ("trim", "trim(X, Y)", "Trim both sides"), ("typeof", "typeof(X)", "Storage class of X"), ("unhex", "unhex(X, Y)", "Blob from hex"),
        ("unicode", "unicode(X)", "Code point of first char"), ("unlikely", "unlikely(X)", "Planner hint: usually false"),
        ("upper", "upper(X)", "Upper case"), ("zeroblob", "zeroblob(N)", "N zero bytes"),
        // Aggregates
        ("avg", "avg(X)", "Average"), ("count", "count(X)", "Count non-NULL / count(*)"), ("group_concat", "group_concat(X, SEP)", "Join values"),
        ("string_agg", "string_agg(X, SEP)", "Join values"), ("sum", "sum(X)", "Sum (NULL if no rows)"), ("total", "total(X)", "Sum as REAL (0.0 if no rows)"),
        // Window
        ("row_number", "row_number()", "Row number in partition"), ("rank", "rank()", "Rank with gaps"), ("dense_rank", "dense_rank()", "Rank without gaps"),
        ("percent_rank", "percent_rank()", "(rank - 1) / (rows - 1)"), ("cume_dist", "cume_dist()", "Cumulative distribution"),
        ("ntile", "ntile(N)", "Bucket number 1..N"), ("lag", "lag(X, OFFSET, DEFAULT)", "Value from a previous row"),
        ("lead", "lead(X, OFFSET, DEFAULT)", "Value from a following row"), ("first_value", "first_value(X)", "First value in frame"),
        ("last_value", "last_value(X)", "Last value in frame"), ("nth_value", "nth_value(X, N)", "N-th value in frame"),
        // Date and time
        ("date", "date(TIME, MOD...)", "YYYY-MM-DD"), ("time", "time(TIME, MOD...)", "HH:MM:SS"), ("datetime", "datetime(TIME, MOD...)", "YYYY-MM-DD HH:MM:SS"),
        ("julianday", "julianday(TIME, MOD...)", "Julian day number"), ("unixepoch", "unixepoch(TIME, MOD...)", "Seconds since 1970"),
        ("strftime", "strftime(FORMAT, TIME, MOD...)", "Custom date format"), ("timediff", "timediff(A, B)", "Difference as +YYYY-MM-DD HH:MM:SS"),
        // Math
        ("acos", "acos(X)", "Arc cosine"), ("asin", "asin(X)", "Arc sine"), ("atan", "atan(X)", "Arc tangent"), ("atan2", "atan2(Y, X)", "Arc tangent of Y/X"),
        ("ceil", "ceil(X)", "Round up"), ("ceiling", "ceiling(X)", "Round up"), ("cos", "cos(X)", "Cosine"), ("degrees", "degrees(X)", "Radians to degrees"),
        ("exp", "exp(X)", "e^X"), ("floor", "floor(X)", "Round down"), ("ln", "ln(X)", "Natural log"), ("log", "log(B, X)", "Log base B (log10 with one arg)"),
        ("log10", "log10(X)", "Log base 10"), ("log2", "log2(X)", "Log base 2"), ("mod", "mod(X, Y)", "Remainder"), ("pi", "pi()", "π"),
        ("pow", "pow(X, Y)", "X^Y"), ("power", "power(X, Y)", "X^Y"), ("radians", "radians(X)", "Degrees to radians"), ("sin", "sin(X)", "Sine"),
        ("sqrt", "sqrt(X)", "Square root"), ("tan", "tan(X)", "Tangent"), ("trunc", "trunc(X)", "Round toward zero"),
        // JSON
        ("json", "json(X)", "Minified JSON"), ("jsonb", "jsonb(X)", "Binary JSON"), ("json_array", "json_array(X, ...)", "Build array"),
        ("json_array_length", "json_array_length(X, PATH)", "Array length"), ("json_each", "json_each(X, PATH)", "Table of top-level elements"),
        ("json_extract", "json_extract(X, PATH, ...)", "Extract value"), ("json_group_array", "json_group_array(X)", "Aggregate into array"),
        ("json_group_object", "json_group_object(K, V)", "Aggregate into object"), ("json_insert", "json_insert(X, PATH, V, ...)", "Insert if missing"),
        ("json_object", "json_object(K, V, ...)", "Build object"), ("json_patch", "json_patch(T, P)", "RFC 7396 merge patch"),
        ("json_quote", "json_quote(X)", "SQL value to JSON"), ("json_remove", "json_remove(X, PATH, ...)", "Remove paths"),
        ("json_replace", "json_replace(X, PATH, V, ...)", "Replace if present"), ("json_set", "json_set(X, PATH, V, ...)", "Insert or replace"),
        ("json_tree", "json_tree(X, PATH)", "Table of every element, recursively"), ("json_type", "json_type(X, PATH)", "JSON type name"),
        ("json_valid", "json_valid(X)", "1 if well-formed JSON"),
        // FTS5
        ("bm25", "bm25(FTS)", "FTS5 relevance (lower is better)"), ("highlight", "highlight(FTS, COL, OPEN, CLOSE)", "Mark matches"),
        ("snippet", "snippet(FTS, COL, OPEN, CLOSE, ELLIPSIS, N)", "Matching excerpt"),
    ];

    public static IReadOnlyList<Completion> All { get; } =
        Keywords.Select(k => new Completion(k, k, "keyword"))
            .Concat(Functions.Select(f => new Completion(f.Name, f.Signature, "function", f.Doc)))
            .ToList();
}
