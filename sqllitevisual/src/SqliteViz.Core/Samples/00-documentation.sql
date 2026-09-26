-- Start here: a runnable tour of SqliteViz
-- SqliteViz is a playground for SQLite. You have a private in-memory database,
-- already filled with a small store + company schema (see the Schema panel on the right).
-- Nothing is written to disk, and nobody else can see your data.
--
-- Press Ctrl+Enter to run this whole script, or put the cursor inside one
-- statement and press Ctrl+Shift+Enter to run just that one (or run a selection).

-- 1. A script can hold many statements. Each one gets its own result card below.
--    Click a card to highlight its statement here in the editor.
SELECT 'Hello from SQLite ' || sqlite_version() AS greeting;

-- 2. Cells are colored by the storage class of each VALUE, not by the declared column type.
--    SQLite is dynamically typed: one column can hold values of different classes.
SELECT 42 AS integer_value, 3.14 AS real_value, 'text' AS text_value,
       x'CAFE' AS blob_value, NULL AS null_value, typeof(1.0) AS type_name;

-- 3. NULL gets its own muted badge, so you can tell it apart from an empty string.
SELECT name, city, city IS NULL AS city_missing, '' AS empty_string
FROM customers
WHERE city IS NULL OR name LIKE 'grace%';

-- 4. INSERT, UPDATE and DELETE report how many rows they changed.
UPDATE products SET stock = stock + 10 WHERE category_id = 4;

-- 5. EXPLAIN QUERY PLAN is drawn as a tree: SCAN is a warning, SEARCH with an index is good,
--    a covering index is best.
EXPLAIN QUERY PLAN
SELECT order_id, order_date FROM orders WHERE customer_id = 1;

-- 6. Big results are cut to 1,000 rows for display (the total is still counted).
WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n LIMIT 1500)
SELECT i, i * i AS square FROM n;

-- 7. You can create, alter and drop anything. Reset DB (top bar) brings the seed back.
CREATE TABLE scratch (id INTEGER PRIMARY KEY, note TEXT);
INSERT INTO scratch (note) VALUES ('try anything'), ('Reset DB undoes it');
SELECT * FROM scratch;
DROP TABLE scratch;

-- 8. What is blocked, and why. Each of these would stop the run with a friendly message,
--    so they are left as comments. Copy one into the editor to see it.
--      ATTACH 'other.db' AS other;       -- one in-memory database only
--      VACUUM INTO '/tmp/copy.db';       -- would write a file
--      SELECT load_extension('x');       -- no extensions or file access
--      PRAGMA journal_mode = OFF;        -- only a safe list of pragmas is allowed
--      DELETE FROM sqlite_schema;        -- the schema table is read-only
--    Allowed pragmas include foreign_keys, table_info, index_list, foreign_key_list, integrity_check.
PRAGMA foreign_keys;

-- 9. A run stops at the first error. Earlier results stay, and the error is marked on its line.
--    Each run has a 5 second time limit, which stops runaway recursive CTEs and huge cross joins.
--    If you BEGIN a transaction and do not finish it, a "Transaction open" badge appears.

-- 10. Load more examples from the Samples menu: joins, CTEs, window functions, JSON, dates, upserts...
SELECT count(*) AS tables FROM pragma_table_list WHERE schema = 'main' AND name NOT LIKE 'sqlite%';
