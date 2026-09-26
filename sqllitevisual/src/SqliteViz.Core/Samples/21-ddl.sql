-- DDL: creating and changing tables, views, triggers, indexes
-- CREATE TABLE ... AS SELECT copies a query result into a new table (no constraints are copied).
CREATE TABLE top_products AS
SELECT p.product_id, p.name, sum(oi.quantity) AS units
FROM products p JOIN order_items oi ON oi.product_id = p.product_id
GROUP BY p.product_id
ORDER BY units DESC
LIMIT 5;
SELECT * FROM top_products;

-- ALTER TABLE: add, rename and drop columns, and rename the table.
ALTER TABLE top_products ADD COLUMN note TEXT DEFAULT 'bestseller';
ALTER TABLE top_products RENAME COLUMN units TO units_sold;
ALTER TABLE top_products DROP COLUMN note;
ALTER TABLE top_products RENAME TO best_sellers;
PRAGMA table_info(best_sellers);

-- CREATE VIEW saves a query under a name.
CREATE VIEW v_low_stock AS
SELECT product_id, name, stock FROM products WHERE stock < 10 AND discontinued = 0;
SELECT * FROM v_low_stock;

-- CREATE TRIGGER runs statements automatically. This one logs stock that drops below 5.
CREATE TRIGGER trg_low_stock_alert
AFTER UPDATE OF stock ON products
WHEN NEW.stock < 5 AND OLD.stock >= 5
BEGIN
    INSERT INTO audit_log (table_name, row_id, action, old_value, new_value)
    VALUES ('products', NEW.product_id, 'low stock', OLD.stock, NEW.stock);
END;
UPDATE products SET stock = 3 WHERE product_id = 9;
SELECT action, row_id, old_value, new_value FROM audit_log;

-- CREATE INDEX, including UNIQUE and multi-column.
CREATE UNIQUE INDEX ux_best_sellers_name ON best_sellers (name);
CREATE INDEX idx_products_cat_price ON products (category_id, price DESC);
PRAGMA index_list(products);

-- STRICT tables enforce column types. A normal table would store 'abc' in an INTEGER column.
CREATE TABLE loose (n INTEGER);
INSERT INTO loose VALUES ('abc'), ('42');
SELECT n, typeof(n) FROM loose;
CREATE TABLE tight (n INTEGER) STRICT;
INSERT INTO tight VALUES ('42');
SELECT n, typeof(n) FROM tight;
-- This line would fail with "cannot store TEXT value in INTEGER column tight.n":
--   INSERT INTO tight VALUES ('abc');

-- DROP ... IF EXISTS does not fail when the object is missing.
DROP TRIGGER IF EXISTS trg_low_stock_alert;
DROP VIEW IF EXISTS v_low_stock;
DROP INDEX IF EXISTS idx_products_cat_price;
DROP TABLE IF EXISTS best_sellers;
DROP TABLE IF EXISTS loose;
DROP TABLE IF EXISTS tight;
DROP TABLE IF EXISTS table_that_never_existed;
