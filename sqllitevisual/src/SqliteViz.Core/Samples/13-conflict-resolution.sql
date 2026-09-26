-- Conflict resolution: OR IGNORE, OR REPLACE, OR ABORT, OR FAIL, OR ROLLBACK
CREATE TABLE inventory (
    sku   TEXT PRIMARY KEY,
    qty   INTEGER NOT NULL CHECK (qty >= 0),
    label TEXT
);
INSERT INTO inventory VALUES ('A', 1, 'first'), ('B', 2, 'second');

-- OR IGNORE skips rows that violate a constraint and keeps going.
INSERT OR IGNORE INTO inventory VALUES ('A', 100, 'dup'), ('C', 3, 'third');
SELECT * FROM inventory;

-- OR REPLACE deletes the conflicting row and inserts the new one. Columns you do not give
-- become NULL/default: label is lost here. That is the main difference from an upsert.
INSERT OR REPLACE INTO inventory (sku, qty) VALUES ('A', 10);
SELECT * FROM inventory WHERE sku = 'A';

-- REPLACE INTO is shorthand for INSERT OR REPLACE.
REPLACE INTO inventory VALUES ('B', 20, 'replaced');
SELECT * FROM inventory;

-- OR REPLACE really DELETEs, so ON DELETE CASCADE fires (when recursive_triggers is on, delete
-- triggers fire too). Replacing a customer profile's parent row cascades to the profile:
SELECT count(*) AS profiles_before FROM customer_profiles WHERE customer_id = 12;
INSERT OR REPLACE INTO customers (customer_id, name, email, city, country, created_at)
VALUES (12, 'Nina Petrova', 'nina@example.com', NULL, 'US', '2024-02-29 13:37:00');
SELECT count(*) AS profiles_after FROM customer_profiles WHERE customer_id = 12;

-- UPDATE OR IGNORE: rows that would violate a constraint are skipped, the others are updated.
-- 'C' has qty 3, so qty - 5 would break the CHECK: that row is left alone.
UPDATE OR IGNORE inventory SET qty = qty - 5;
SELECT * FROM inventory ORDER BY sku;

-- OR ABORT (the default) undoes the whole failing statement; OR FAIL keeps the rows changed before
-- the error; OR ROLLBACK also rolls back the enclosing transaction. Each of these stops a run with an
-- error, so they are shown as comments. Try them one at a time:
--   INSERT OR ABORT INTO inventory VALUES ('D', 4, NULL), ('A', -1, NULL);   -- nothing inserted
--   INSERT OR FAIL  INTO inventory VALUES ('D', 4, NULL), ('A', -1, NULL);   -- 'D' stays
--   BEGIN; INSERT INTO inventory VALUES ('E', 5, NULL);
--   INSERT OR ROLLBACK INTO inventory VALUES ('A', -1, NULL);               -- 'E' is gone too
-- CHECK constraints are not "uniqueness" conflicts: OR IGNORE still skips the row, OR REPLACE aborts.
INSERT OR IGNORE INTO inventory VALUES ('F', -5, 'negative');
SELECT count(*) AS negative_rows FROM inventory WHERE qty < 0;

DROP TABLE inventory;
