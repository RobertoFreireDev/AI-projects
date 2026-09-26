-- PRAGMA: inspecting the schema
-- table_info: one row per column (cid, name, type, notnull, default, pk).
PRAGMA table_info(orders);

-- table_xinfo also shows hidden and generated columns (hidden: 2 = VIRTUAL, 3 = STORED).
PRAGMA table_xinfo(order_items);

-- table_list: every table and view, with strict/without-rowid flags.
PRAGMA table_list;

-- index_list and index_info: indexes on a table, and the columns in one index.
PRAGMA index_list(orders);
PRAGMA index_info(idx_orders_customer_date);
PRAGMA index_xinfo(ux_customers_email);

-- foreign_key_list: the table's foreign keys, including ON DELETE / ON UPDATE actions.
PRAGMA foreign_key_list(shipment_items);

-- foreign_keys: enforcement is a per-connection setting (on here).
PRAGMA foreign_keys;

-- With enforcement off, bad rows can get in. foreign_key_check finds them.
PRAGMA foreign_keys = OFF;
INSERT INTO orders (order_id, customer_id, order_date) VALUES (900, 999, '2025-01-01');
PRAGMA foreign_key_check;
DELETE FROM orders WHERE order_id = 900;
PRAGMA foreign_keys = ON;
PRAGMA foreign_key_check;

-- integrity_check verifies the whole database file structure.
PRAGMA integrity_check;

-- Every pragma that returns rows also exists as a table-valued function named pragma_<name>,
-- which you can filter and join like a table.
SELECT name, type, "notnull", pk FROM pragma_table_info('customers') WHERE pk > 0 OR "notnull";

-- function_list: which functions exist in this build.
SELECT name, narg, type FROM pragma_function_list WHERE name LIKE 'json%' ORDER BY name;

-- Every column of every table, by joining sqlite_schema with pragma_table_info.
SELECT m.name AS table_name, p.cid, p.name AS column_name, p.type, p.pk
FROM sqlite_schema m
JOIN pragma_table_info(m.name) p
WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite%'
ORDER BY m.name, p.cid;

-- Every foreign key in the database, in one query.
SELECT m.name AS child, f."from" AS child_column, f."table" AS parent, f."to" AS parent_column, f.on_delete
FROM sqlite_schema m
JOIN pragma_foreign_key_list(m.name) f
WHERE m.type = 'table'
ORDER BY child;

-- Pragmas that change how the database works, or reach outside it, are blocked here.
-- For example, "PRAGMA journal_mode = OFF;" would stop the run with a message.
PRAGMA user_version = 3;
PRAGMA user_version;
