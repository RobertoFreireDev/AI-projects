# CLAUDE.md — SqliteViz

Guidance for Claude (and any AI agent) working in this repository. Read this fully before writing code.

## 1. What this is

SqliteViz is a **Blazor Server** playground for writing and testing **SQLite** queries against a private **in-memory database**.

1. The user writes SQL in an **Ace editor** (top of the page). A script can hold many statements.
2. The server splits the script into statements and runs them, in order, against the user's own in-memory SQLite database.
3. Each statement produces a **result card** (below the editor): a grid of rows, a row count for DML, a query-plan tree for `EXPLAIN QUERY PLAN`, or an error on a specific line.
4. A **schema explorer** (side panel) shows the current tables, columns, keys, indexes, views, and triggers, and refreshes after every run.

Every new session starts with a database seeded by the **seed sample**, so every other sample works right away. The user can do anything SQLite allows (create, alter, and drop tables, run DML, change session pragmas) **except** things that act on the database as a whole or reach outside it: `ATTACH`/`DETACH`, `VACUUM` (including `VACUUM INTO`), loading extensions, and unsafe pragmas. A **Reset DB** button throws the database away and re-seeds it.

## 2. Non-negotiables

- **.NET 10**, Blazor Web App with **Interactive Server** render mode. No WebAssembly.
- **No external NuGet packages**, with ONE approved exception: `Microsoft.Data.Sqlite` (Microsoft's own ADO.NET provider), referenced by `SqliteViz.Core` only. It brings `SQLitePCLRaw.bundle_e_sqlite3` transitively; use its `SQLitePCL.raw` API directly for the authorizer, interrupt, limits, and statement splitting. Don't add any other `SQLitePCLRaw` bundle, and nothing else (no Dapper, no EF Core, no UI kits, no test frameworks, no JSON libs). Pin to the latest stable version compatible with .NET 10 (currently 10.0.12, which bundles SQLite 3.53.3). The bundled native library must cover **linux-arm64** (the dev machine is a Raspberry Pi) and x64.
- SQLitePCLRaw 2.1.x does not wrap `sqlite3_error_offset`, so `Session/NativeMethods.cs` P/Invokes it from the same bundled `e_sqlite3` library (no extra package) and falls back to "no column" if the entry point is missing.
- The bundled build has `DQS=0` (double-quoted strings are identifiers only, so samples use single quotes for text) and `DEFAULT_RECURSIVE_TRIGGERS` (a trigger that updates its own table must not fire itself again).
- **Minimum SQLite version 3.45** (needed for `RIGHT`/`FULL JOIN` 3.39, `->`/`->>` and `unixepoch` 3.38, `timediff` 3.43, `concat` 3.44, `jsonb` 3.45). At startup, log `sqlite_version()` and fail fast with a clear message if it is lower. A test asserts the version.
- **The database is in memory only.** Open it with `Data Source=:memory:`. Nothing is ever written to disk: `temp_store = MEMORY`, no `ATTACH`, no `VACUUM INTO`, no extension loading.
- **One database per circuit.** Each Blazor circuit owns one open `SqliteConnection` for its lifetime. Users never see each other's data.
- **The authorizer does the enforcement**, not string matching (section 7). A first-keyword check only exists to give friendlier messages.
- **Minimal JavaScript**: only Ace interop. Grids, plan trees, the schema explorer, and the ER diagram are Razor + HTML/SVG + CSS.

## 3. Solution layout

```
SqliteViz.sln
Directory.Build.props
src/
  SqliteViz.Core/        # Session DB, splitter, guard (authorizer), runner, schema reader, plan parser, samples. Refs Microsoft.Data.Sqlite.
    Samples/             # built-in .sql files (embedded resources)
  SqliteViz.Web/         # Blazor Server UI. Refs Core.
    wwwroot/lib/ace/     # vendored Ace build
    wwwroot/js/editor.js
tests/
  SqliteViz.Tests/       # Plain console app with a tiny assert helper (no xUnit — no packages).
```

Samples live in **Core** (not Web) because the session needs the seed script to initialize every new database, and the tests read the same catalog.

## 4. Request flow

```
Editor (Ace) --Run--> SqlRunner.RunAsync(session, script)          [Core, server]
                        1. Split into statements with SQLite's own parser (prepare + tail)
                        2. Pre-check first keyword (friendly errors for VACUUM/ATTACH/DETACH)
                        3. For each statement, in order:
                             prepare -> authorizer checks -> step rows -> collect result
                           Stop at the first error.
                        4. Refresh the schema snapshot
                      <-- RunResult { Statements, Error, Schema, Elapsed }
UI: one ResultCard per statement, error annotation in Ace, schema panel re-rendered
```

There is no HTTP API: in Blazor Server the component calls `SqlRunner` directly. Keep it that way.

## 5. Session database (SqliteViz.Core/Session)

- `DbSession` is a **scoped** service (one per circuit). It owns one open `SqliteConnection` and a `SemaphoreSlim(1)`, because a connection is not thread-safe and a user can click Run twice.
- On open, in this order, **before** installing the authorizer:
  1. `PRAGMA foreign_keys = ON;`
  2. `PRAGMA temp_store = MEMORY;`
  3. `PRAGMA max_page_count = <N>;` so the database stays under **64 MB**.
  4. Set `SQLITE_DBCONFIG_DEFENSIVE` (blocks writes to shadow tables and `sqlite_schema`).
  5. Set limits with `sqlite3_limit`: `SQLITE_LIMIT_LENGTH` 1 MB, `SQLITE_LIMIT_SQL_LENGTH` 100 KB, `SQLITE_LIMIT_ATTACHED` 0.
  6. Install the authorizer (section 7).
  7. Run the seed sample.
- `ResetAsync()` closes the connection (which frees the memory database), opens a new one, and repeats the steps.
- `Dispose` closes the connection. A circuit that goes away frees its database.

## 6. Statement splitting and execution (SqliteViz.Core/Runner)

1. **Split** with SQLite's own tokenizer: `SqlSplitter` ends a piece at the first `;` where `raw.sqlite3_complete` says the text so far is a complete statement, then the runner prepares each piece on its own with `raw.sqlite3_prepare_v3`. (A split that prepares the whole script up front cannot work: `CREATE TABLE t ...; INSERT INTO t ...` fails to prepare the INSERT before the CREATE has run.) Never split on `;` by hand: it breaks string literals, comments, and `CREATE TRIGGER ... BEGIN ...; END;` bodies. Each piece records its text, start offset, and **start line and column** (1-based) of its first token in the user's script. Skip empty or comment-only pieces. The same splitter finds the statement under the cursor for Ctrl+Shift+Enter.
2. **Run each statement** in order, in autocommit mode unless the user wrote `BEGIN`. The runner never wraps the script in its own transaction.
3. **Result per statement**:

```csharp
public sealed record StatementResult(
    int Index,
    int Line,                         // start line in the user's script
    string Sql,                       // statement text as written
    StatementKind Kind,               // Query, Dml, Ddl, Pragma, QueryPlan, Transaction, Other
    IReadOnlyList<ResultColumn> Columns,   // name, declared type
    IReadOnlyList<ResultRow> Rows,         // values as display cells (see below)
    int TotalRows,                    // rows produced, may exceed Rows.Count
    bool Truncated,
    long? RowsAffected,               // sqlite3_changes() for DML
    PlanNode? Plan,                   // tree for EXPLAIN QUERY PLAN
    TimeSpan Elapsed);

public sealed record RunResult(
    IReadOnlyList<StatementResult> Statements,
    SqlError? Error,                  // message, SQLite error code, line, column
    SchemaSnapshot Schema,
    TimeSpan Elapsed);
```

4. **Cells** carry the **storage class of the value** (`NULL`, `INTEGER`, `REAL`, `TEXT`, `BLOB`) from `sqlite3_column_type`, not the declared column type, because SQLite is dynamically typed and showing that is part of learning it. Text is truncated to 200 characters for display, BLOBs show as `x'0A1B…'` plus their length.
5. **Stop at the first error.** Keep the results of the statements that already ran, then show the error on its card. Line and column come from the statement's start line plus `sqlite3_error_offset` (3.38+) when it is available.
6. **Limits**:
   - **Timeout 5 s** per run. A timer calls `raw.sqlite3_interrupt(handle)` (thread-safe), which makes the running statement fail with `SQLITE_INTERRUPT`. It is reported as "Query timed out after 5 s". This is what stops runaway recursive CTEs and cross joins.
   - At most **1,000 rows kept** per statement (`Truncated = true`, `TotalRows` keeps counting up to 100,000, then stops stepping).
   - At most **100 statements** per run. (Originally 50, but the seed alone has 58 statements: 17 drops, 15 tables, 5 indexes, 2 views, 3 triggers, 14 inserts, BEGIN/COMMIT. It has to run under the limit because users can load and run it like any other sample.)
7. **Open transactions**: if a run ends inside a transaction (`sqlite3_get_autocommit == 0`), the UI shows a "Transaction open" badge. Don't commit or roll back on the user's behalf. Reset DB clears it.

## 7. Guard (SqliteViz.Core/Guard)

The authorizer (`raw.sqlite3_set_authorizer`) is the security boundary for this app. It runs while each statement is prepared, so it also covers statements inside triggers and CTEs.

**Denied** (return `SQLITE_DENY`, with a friendly message the runner can show):

| Action code | Rule | Message |
|---|---|---|
| `SQLITE_ATTACH`, `SQLITE_DETACH` | always | "This playground has a single in-memory database. ATTACH/DETACH are disabled." |
| `SQLITE_PRAGMA` | pragma name not in the allowlist, or a write to a read-only pragma | "PRAGMA x is not allowed here." |
| `SQLITE_FUNCTION` | `load_extension`, `readfile`, `writefile`, `edit`, `fts3_tokenizer` | "Loading extensions and file access are disabled." |
| `SQLITE_CREATE_VTABLE` | module not in {`fts5`, `rtree`} | "Only fts5 and rtree virtual tables are allowed." |
| `SQLITE_INSERT`/`SQLITE_UPDATE`/`SQLITE_DELETE` | target is `sqlite_schema`/`sqlite_master`, and the top-level statement is INSERT/UPDATE/DELETE/REPLACE/WITH | "The schema table is read-only." |

Notes on the schema-table rule: `CREATE`, `DROP`, `ALTER`, `ANALYZE` and `PRAGMA optimize` write `sqlite_schema` as part of their own work, and SQLite asks the authorizer for those writes too, so the rule only applies when the user's statement is DML (the runner sets `DbSession.DmlStatement` before each prepare). `SQLITE_DBCONFIG_DEFENSIVE` rejects direct writes before the authorizer is asked, and its "table sqlite_master may not be modified" message is mapped to the friendly one (`SqlGuard.FriendlyMessage`). Trigger bodies are authorized when the trigger fires, as part of the statement that fires it.

**Pragma allowlist**:
- Read and write: `foreign_keys`, `defer_foreign_keys`, `recursive_triggers`, `case_sensitive_like`, `user_version`, `analysis_limit`.
- Read only (no `= value`): `table_info`, `table_xinfo`, `table_list`, `index_list`, `index_info`, `index_xinfo`, `foreign_key_list`, `foreign_key_check`, `integrity_check`, `quick_check`, `database_list`, `collation_list`, `function_list`, `pragma_list`, `module_list`, `compile_options`, `encoding`, `page_size`, `page_count`, `freelist_count`, `schema_version`, `application_id`, `max_page_count`, `temp_store`, `data_version` (FTS5 reads it internally, through the authorizer). The inspect pragmas (`table_info(x)`, `integrity_check(N)`...) may take an argument; the setting pragmas may not.
- Also allowed: `optimize`.
- Everything else is denied, in particular `writable_schema`, `journal_mode`, `locking_mode`, `temp_store` writes, `max_page_count` writes, `mmap_size`, `cache_size`, `trusted_schema`, and `*_directory`. The table-valued forms (`SELECT * FROM pragma_table_info('orders')`) go through the same check.

**Pre-check** (friendlier messages only, the authorizer is still the real guard): if a statement's first keyword, after comments, is `VACUUM`, `ATTACH`, or `DETACH`, reject it before preparing. `VACUUM` is blocked completely, because `VACUUM INTO 'file'` writes to disk and a plain `VACUUM` on an in-memory database is pointless.

**Allowed on purpose**: `CREATE`/`ALTER`/`DROP` of tables, indexes, views, and triggers, `DELETE` without `WHERE`, transactions and savepoints. Losing seeded data is fine: Reset DB brings it back.

This is a guardrail for a personal practice tool, not a hardened multi-tenant service. Do not claim otherwise in the UI or docs, and do not deploy it publicly without real isolation and per-user resource limits.

## 8. Schema explorer and visuals (SqliteViz.Core/Schema, SqliteViz.Web)

- `SchemaReader` builds a `SchemaSnapshot` from `pragma_table_list`, `pragma_table_xinfo`, `pragma_foreign_key_list`, `pragma_index_list`, `pragma_index_xinfo`, and `sqlite_schema` (for views, triggers, and `CREATE` SQL). It runs **as the host**, with the authorizer temporarily bypassed by a flag on the session and never while user SQL is executing.
- Snapshot per table: columns (name, declared type, PK position, NOT NULL, default, hidden/generated), foreign keys (columns → table.columns, ON DELETE/UPDATE), indexes (unique, partial, columns or expressions), `STRICT`, `WITHOUT ROWID`, row count.
- **Result grid**: sticky header, cells colored by storage class via CSS variables, `NULL` shown as a distinct muted badge (never an empty cell).
- **Query plan tree**: `EXPLAIN QUERY PLAN` rows (`id`, `parent`, `detail`) become a `PlanNode` tree in Core. Nodes are tagged: `SCAN` → warn, `SEARCH ... USING INDEX` → good, `USING COVERING INDEX` → best, `USE TEMP B-TREE` → warn, `AUTOMATIC INDEX` → warn. The Web renders the tree as nested HTML with those colors.
- **ER diagram** (phase 5): SVG, one box per table (PK/FK markers on columns), one line per foreign key with cardinality markers. Layout is a pure function in Core (layered by FK depth, columns in each layer ordered to reduce crossings). No JS.
- Dark/light via CSS variables. Layout must work on a laptop screen: editor on top (resizable height), results below, schema panel on the right (collapsible).

## 9. Editor (Ace)

- `wwwroot/js/editor.js` as an ES module loaded via `IJSObjectReference`. Functions: `init`, `getValue`, `setValue`, `getSelectionOrStatementAtCursor`, `setAnnotations(errors)`, `highlightRange(startLine, endLine)`, `clearHighlight`, `setCompletions(schemaWords)`, `dispose`.
- Mode `ace/mode/sql`. Errors → Ace annotations and a marker on the failing statement.
- Clicking a result card highlights its statement in the editor.
- Completer: SQLite keywords, built-in function names with short signatures (a static list in Core), plus the **live** table and column names from the last schema snapshot.
- Shortcuts: **Ctrl+Enter** runs the whole script, **Ctrl+Shift+Enter** runs the selection or the statement under the cursor.
- Samples dropdown loads a sample into the editor (it does not run it). Remember the last script per browser with `localStorage` (only JS touches it).

## 10. Seed schema (Samples/01-seed.sql)

The seed is a small store + company domain designed so every relationship type, constraint, and index kind appears at least once. It is **idempotent**: it starts with `DROP ... IF EXISTS` in reverse dependency order and then creates everything in one transaction. Seed data is **deterministic** (no `random()`, no `'now'`), with 5–30 rows per table.

| Feature | Where |
|---|---|
| One-to-one | `customers` ↔ `customer_profiles` (profile PK is also the FK, `ON DELETE CASCADE`) |
| One-to-many | `customers` → `orders`, `departments` → `employees` |
| Many-to-many | `orders` ↔ `products` via `order_items` (composite PK), `products` ↔ `tags` via `product_tags` |
| Self-reference (hierarchy) | `employees.manager_id` → `employees`, `categories.parent_id` → `categories` |
| Composite foreign key | `shipment_items (order_id, product_id)` → `order_items` |
| Graph data | `cities` + `routes (from_city, to_city, km)` with a cycle, for recursive path search |
| FK actions | `CASCADE`, `SET NULL`, `RESTRICT` (`orders.customer_id`, `order_items.product_id`), `NO ACTION` (`shipment_items`) each used at least once. A self-referencing FK must not be `RESTRICT`: `DROP TABLE` deletes row by row, so re-running the seed would fail (`categories.parent_id` is `CASCADE`). |
| Constraints | `NOT NULL`, `UNIQUE`, `CHECK` (incl. `CHECK (json_valid(attributes))`), `DEFAULT`, `COLLATE NOCASE` |
| Table kinds | `INTEGER PRIMARY KEY` (rowid alias), `AUTOINCREMENT`, `WITHOUT ROWID`, `STRICT` |
| Generated columns | `VIRTUAL` (`order_items.line_total`) and `STORED` |
| JSON | `products.attributes` (JSON text), `events.payload` |
| Dates | ISO-8601 `TEXT` for dates, one Unix-epoch `INTEGER` column for contrast |
| Indexes | single-column, composite, `UNIQUE`, partial (`WHERE status = 'open'`), expression (`lower(email)`), covering |
| Views | `v_order_totals`, `v_employee_tree` |
| Triggers | `AFTER UPDATE` audit trigger into `audit_log`, `updated_at` touch trigger (`AFTER UPDATE OF <every column but updated_at>`, so it does not re-fire itself with recursive triggers on), `BEFORE DELETE` guard with `RAISE(ABORT, ...)` |
| Edge-case data | a customer with no orders, a product never ordered, a department with no employees, `NULL`s in optional columns, duplicate-looking names differing by case |

## 11. Built-in samples

Samples live in `src/SqliteViz.Core/Samples/NN-name.sql` as embedded resources. The first line (`-- Title`) is the dropdown title. Each sample has short `--` comments before each statement explaining what it shows. All samples except `01-seed` assume a freshly seeded database.

| File | Covers |
|---|---|
| `00-documentation.sql` | Runnable tour of the app: multiple statements, result cards, storage classes, NULL display, what is blocked and why, Reset DB. Keep it in sync with the guard and UI. |
| `01-seed.sql` | Full schema + seed data (section 10). Runs automatically for every new session. |
| `02-select-basics.sql` | `WHERE`, `DISTINCT`, `ORDER BY` (incl. `NULLS FIRST/LAST`), `LIMIT`/`OFFSET`, `LIKE`/`GLOB`, `BETWEEN`, `IN`, `IS NULL`, `CASE`, `COALESCE`, `IIF` |
| `03-aggregation.sql` | `GROUP BY`, `HAVING`, `count`/`sum`/`total`/`avg`/`min`/`max`, `group_concat`/`string_agg`, aggregate `FILTER (WHERE ...)` |
| `04-joins.sql` | `INNER`, `LEFT OUTER`, `RIGHT`, `FULL OUTER`, `CROSS`, `NATURAL`, `USING`, self join (employee → manager), many-to-many through a junction table, anti-join |
| `05-cte.sql` | Non-recursive `WITH`, multiple and chained CTEs, `MATERIALIZED` / `NOT MATERIALIZED` |
| `06-recursive-cte.sql` | Number series, category tree with depth and path, org chart, route search over `routes` with cycle prevention, date series |
| `07-window-functions.sql` | `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `NTILE`, `LAG`, `LEAD`, `FIRST_VALUE`, `LAST_VALUE`, `NTH_VALUE`, `PERCENT_RANK`, `CUME_DIST`, `PARTITION BY`, running totals, moving averages with `ROWS`/`RANGE`/`GROUPS` frames, `EXCLUDE`, named `WINDOW`, top-N per group |
| `08-subqueries.sql` | Scalar subquery in `SELECT`, correlated subqueries, `EXISTS`/`NOT EXISTS`, `IN`/`NOT IN` (with the `NULL` pitfall), derived tables in `FROM`, **ANY/ALL equivalents** (see note below) |
| `09-set-operations.sql` | `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT` |
| `10-dml-basics.sql` | Multi-row `INSERT`, `INSERT ... SELECT`, `UPDATE ... FROM`, `DELETE` with subquery, FK cascade effects, trigger-written `audit_log` |
| `11-upsert.sql` | `INSERT ... ON CONFLICT DO NOTHING`, `DO UPDATE SET ... = excluded.x`, `DO UPDATE ... WHERE`, multiple `ON CONFLICT` clauses, upsert on a composite key |
| `12-returning.sql` | `INSERT`/`UPDATE`/`DELETE ... RETURNING`, including generated ids and computed expressions |
| `13-conflict-resolution.sql` | `INSERT OR IGNORE`, `OR REPLACE` (and how it deletes + cascades), `OR ABORT`, `OR FAIL`, `OR ROLLBACK`, `UPDATE OR ...`, `REPLACE INTO` |
| `14-transactions.sql` | `BEGIN`/`COMMIT`/`ROLLBACK`, `SAVEPOINT`/`RELEASE`/`ROLLBACK TO`, deferred FK checks |
| `15-core-functions.sql` | String (`substr`, `instr`, `replace`, `trim`, `upper`, `lower`, `length`, `octet_length`, `printf`/`format`, `concat`, `concat_ws`, `hex`, `unicode`, `char`), `typeof`, `cast`, `nullif`, `round`, `abs`, `min`/`max` scalar, `likely`, `quote` |
| `16-math-functions.sql` | `sqrt`, `pow`, `exp`, `ln`, `log`, `log2`, `log10`, `sin`, `cos`, `tan`, `atan2`, `pi`, `degrees`, `radians`, `ceil`, `floor`, `trunc`, `mod`, `sign` |
| `17-json.sql` | `json`, `json_valid`, `json_extract`, `->`, `->>`, `json_object`, `json_array`, `json_group_array`, `json_group_object`, `json_each`, `json_tree`, `json_set`/`insert`/`replace`/`remove`, `json_patch`, `json_type`, `jsonb`, indexing a JSON path with an expression index |
| `18-date-time.sql` | `date`, `time`, `datetime`, `julianday`, `unixepoch`, `strftime`, `timediff`, modifiers (`+N days`, `start of month`, `weekday N`, `-1 day`, `unixepoch`, `auto`), age and interval math, grouping by month |
| `19-pragma.sql` | `table_info`, `table_xinfo`, `table_list`, `index_list`, `index_info`, `foreign_key_list`, `foreign_key_check`, `foreign_keys` on/off, `integrity_check`, `function_list`, table-valued pragma functions joined with `sqlite_schema` |
| `20-explain-query-plan.sql` | Full `SCAN` vs index `SEARCH`, covering index, partial index, expression index, `USE TEMP B-TREE FOR ORDER BY`, automatic index, the same query before and after `CREATE INDEX`, `ANALYZE` |
| `21-ddl.sql` | `CREATE TABLE ... AS SELECT`, `ALTER TABLE ADD/RENAME/DROP COLUMN`, `RENAME TO`, `CREATE VIEW`, `CREATE TRIGGER`, `CREATE INDEX`, `DROP ... IF EXISTS`, `STRICT` type errors |
| `22-fts5.sql` | Optional (only if `fts5` is in `compile_options`): virtual table, `MATCH`, `bm25`, `highlight` |

Note on **ANY/ALL**: SQLite has no `> ANY (...)` / `> ALL (...)` operators. `08-subqueries.sql` must say so in a comment and show the equivalents (`> (SELECT min(...))`, `> (SELECT max(...))`, `EXISTS`/`NOT EXISTS`). Don't write samples with syntax SQLite rejects.

Note on **math functions**: they exist only if SQLite was built with `SQLITE_ENABLE_MATH_FUNCTIONS`. At startup, check `SELECT sqrt(4)`. If it fails, register the same names with `SqliteConnection.CreateFunction` (deterministic) so the sample works either way. A test covers this.

Note on **non-deterministic samples**: `'now'`, `random()`, and `localtime` are fine in samples, but tests only assert that those statements run, never their values.

Every sample must run without errors under the limits and is exercised by a test.

## 12. Build phases

1. Solution skeleton, `DbSession` with an in-memory connection and startup checks, splitter, runner end-to-end, Ace editor, results shown as plain text.
2. Guard: authorizer, pragma allowlist, pre-check, limits, timeout via interrupt. Tests for every blocked action.
3. Result cards and grid (storage-class colors, NULL badge, truncation, rows affected, per-statement timing), error annotations, statement highlighting.
4. Seed script, Reset DB, schema explorer, schema-aware autocomplete.
5. `EXPLAIN QUERY PLAN` tree, ER diagram.
6. All samples, samples dropdown, polish, keyboard shortcuts, dark/light.

Finish and test each phase before starting the next.

## 13. Conventions

- Nullable enabled, warnings as errors, file-scoped namespaces, `sealed` by default, records for data.
- Always use parameters (`$name`) for host-issued SQL that includes values. Identifiers (table names for the schema reader) are quoted with `"` + doubled quotes, never concatenated raw.
- No static mutable state except cached read-only metadata (function list, samples).
- Dispose every `SqliteCommand`/`SqliteDataReader`/native statement; finalize anything prepared with `raw.sqlite3_prepare_v3`.
- Build and test: `dotnet build`, then `dotnet run --project tests/SqliteViz.Tests` (optional name filter as the first argument). Run the app with `dotnet run --project src/SqliteViz.Web` (http://localhost:5110). To check a sample's comments against real output: `dotnet run --project tests/SqliteViz.Tests -- --dump <sample-id>`.
- Tests (plain console runner, exits non-zero on failure) cover: the SQLite version and math functions; splitter (strings with `;`, comments, trigger bodies, line numbers); each blocked action is rejected with its friendly message (`ATTACH`, `DETACH`, `VACUUM`, `VACUUM INTO`, `load_extension`, `PRAGMA writable_schema=1`, `journal_mode`, writes to `sqlite_schema`, disallowed pragmas through `pragma_*` functions); allowed pragmas work; timeout on an infinite recursive CTE returns an error instead of hanging; row truncation; stop-on-first-error keeps earlier results; error line mapping; two sessions are isolated; Reset DB restores the seed; every sample runs on a fresh seeded database.
- Keep commits and changes small; update this file when a rule, the guard, or the seed schema changes.

## 14. Don'ts

- Don't add NuGet packages beyond `Microsoft.Data.Sqlite`.
- Don't open a file-backed or shared-cache database. Never write to disk.
- Don't split SQL with regex or `;`; use SQLite's own parser.
- Don't rely on keyword or string filtering for safety; the authorizer is the boundary.
- Don't run user SQL without the authorizer and interrupt timer in place, or on a connection shared between sessions.
- Don't wrap the user's script in a hidden transaction or commit/roll back for them.
- Don't use JS for rendering grids, plan trees, or diagrams.
- Don't add samples that use syntax SQLite doesn't support (`ANY`/`ALL`, `TOP`, `ILIKE`, `::` casts, etc.).
