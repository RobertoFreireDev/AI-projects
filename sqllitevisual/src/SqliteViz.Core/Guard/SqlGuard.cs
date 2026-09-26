using SQLitePCL;
using SqliteViz.Core.Runner;

namespace SqliteViz.Core.Guard;

/// <summary>
/// The security boundary: an authorizer installed on every session connection. It runs while each statement is
/// prepared (including statements inside triggers, views, CTEs and table-valued pragma functions), so it sees what
/// SQLite will actually do rather than what the text looks like.
/// </summary>
public static class SqlGuard
{
    public const string AttachMessage = "This playground has a single in-memory database. ATTACH/DETACH are disabled.";
    public const string VacuumMessage = "VACUUM is disabled: VACUUM INTO writes a file, and a plain VACUUM does nothing useful on an in-memory database.";
    public const string ExtensionMessage = "Loading extensions and file access are disabled.";
    public const string VirtualTableMessage = "Only fts5 and rtree virtual tables are allowed.";
    public const string SchemaTableMessage = "The schema table is read-only.";

    public static string PragmaMessage(string name) => $"PRAGMA {name} is not allowed here.";

    /// <summary>Pragmas that may be read and written.</summary>
    public static readonly IReadOnlySet<string> ReadWritePragmas = Set(
        "foreign_keys", "defer_foreign_keys", "recursive_triggers", "case_sensitive_like", "user_version", "analysis_limit");

    /// <summary>Read-only pragmas whose argument selects what to inspect (a table, an index, a row limit).</summary>
    public static readonly IReadOnlySet<string> InspectPragmas = Set(
        "table_info", "table_xinfo", "table_list", "index_list", "index_info", "index_xinfo",
        "foreign_key_list", "foreign_key_check", "integrity_check", "quick_check", "optimize");

    /// <summary>Read-only pragmas that are settings: allowed only without "= value".</summary>
    public static readonly IReadOnlySet<string> ReadOnlyPragmas = Set(
        "database_list", "collation_list", "function_list", "pragma_list", "module_list", "compile_options",
        "encoding", "page_size", "page_count", "freelist_count", "schema_version", "application_id",
        "max_page_count", "temp_store",
        "data_version"); // FTS5 reads it internally

    private static readonly IReadOnlySet<string> DeniedFunctions = Set(
        "load_extension", "readfile", "writefile", "edit", "fts3_tokenizer");

    private static readonly IReadOnlySet<string> AllowedModules = Set("fts5", "rtree");

    private static readonly IReadOnlySet<string> SchemaTables = Set(
        "sqlite_schema", "sqlite_master", "sqlite_temp_schema", "sqlite_temp_master");

    /// <summary>
    /// Friendlier message for statements rejected before preparing. Not a safety measure: the authorizer and
    /// SQLITE_LIMIT_ATTACHED = 0 block the same things.
    /// </summary>
    public static string? PreCheck(string sql)
    {
        var words = SqlText.LeadingKeywords(sql, 4);
        var i = 0;
        // Look through EXPLAIN [QUERY PLAN] so "EXPLAIN VACUUM" gets the same message.
        if (words.Count > i && words[i] == "EXPLAIN") i++;
        if (words.Count > i + 1 && words[i] == "QUERY" && words[i + 1] == "PLAN") i += 2;
        return words.ElementAtOrDefault(i) switch
        {
            "VACUUM" => VacuumMessage,
            "ATTACH" or "DETACH" => AttachMessage,
            _ => null,
        };
    }

    /// <summary>True for INSERT/UPDATE/DELETE statements (the only ones where a schema-table write is the user's own).</summary>
    public static bool IsDmlStatement(string sql) => SqlText.FirstKeyword(sql) is "INSERT" or "UPDATE" or "DELETE" or "REPLACE" or "WITH";

    /// <summary>Replaces SQLite's defensive-mode message (which fires before the authorizer) with the friendly one.</summary>
    public static string FriendlyMessage(string message) =>
        message.StartsWith("table sqlite_master may not be modified", StringComparison.Ordinal)
        || message.StartsWith("table sqlite_temp_master may not be modified", StringComparison.Ordinal)
            ? SchemaTableMessage
            : message;

    /// <summary>Decides one authorizer callback. Returns the denial message, or null to allow.</summary>
    /// <param name="dmlStatement">
    /// The top-level statement is INSERT/UPDATE/DELETE. CREATE, DROP, ALTER, ANALYZE and PRAGMA optimize write
    /// sqlite_schema as part of their own work and SQLite asks the authorizer for that too, so those must pass.
    /// Direct writes to the schema table are also blocked by SQLITE_DBCONFIG_DEFENSIVE.
    /// </param>
    public static string? Check(int action, string? param0, string? param1, bool dmlStatement = true)
    {
        switch (action)
        {
            case raw.SQLITE_ATTACH:
            case raw.SQLITE_DETACH:
                return AttachMessage;

            case raw.SQLITE_PRAGMA:
            {
                var name = param0 ?? "";
                if (ReadWritePragmas.Contains(name) || InspectPragmas.Contains(name)) return null;
                if (ReadOnlyPragmas.Contains(name) && param1 is null) return null;
                return PragmaMessage(param1 is null ? name : $"{name} = {param1}");
            }

            case raw.SQLITE_FUNCTION:
                return DeniedFunctions.Contains(param1 ?? "") ? ExtensionMessage : null;

            case raw.SQLITE_CREATE_VTABLE:
                return AllowedModules.Contains(param1 ?? "") ? null : VirtualTableMessage;

            case raw.SQLITE_INSERT:
            case raw.SQLITE_UPDATE:
            case raw.SQLITE_DELETE:
                return dmlStatement && SchemaTables.Contains(param0 ?? "") ? SchemaTableMessage : null;

            default:
                return null;
        }
    }

    private static IReadOnlySet<string> Set(params string[] items) => new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
}
