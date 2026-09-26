-- EXPLAIN QUERY PLAN: how SQLite runs a query
-- A full SCAN reads every row. Fine for tiny tables; slow for big ones. (warn)
EXPLAIN QUERY PLAN SELECT * FROM customers WHERE city = 'Lisbon';

-- A primary-key lookup is a SEARCH using the rowid. (good)
EXPLAIN QUERY PLAN SELECT * FROM customers WHERE customer_id = 3;

-- SEARCH using an index: orders(customer_id, order_date) serves customer_id = ?.
EXPLAIN QUERY PLAN SELECT * FROM orders WHERE customer_id = 1;

-- A covering index has every column the query needs, so the table is never read. (best)
EXPLAIN QUERY PLAN SELECT customer_id, order_date FROM orders WHERE customer_id = 1;
EXPLAIN QUERY PLAN SELECT product_id, sum(quantity * unit_price) FROM order_items GROUP BY product_id;

-- A partial index is used only when the query's WHERE implies the index's WHERE (status = 'open').
EXPLAIN QUERY PLAN SELECT order_id FROM orders WHERE status = 'open' AND order_date > '2024-12-01';
EXPLAIN QUERY PLAN SELECT order_id FROM orders WHERE status = 'paid' AND order_date > '2024-12-01';

-- An expression index matches the same expression: lower(email).
EXPLAIN QUERY PLAN SELECT name FROM customers WHERE lower(email) = 'bob@example.com';
EXPLAIN QUERY PLAN SELECT name FROM customers WHERE email = 'bob@example.com';

-- ORDER BY on an unindexed column needs a temporary b-tree to sort. (warn)
EXPLAIN QUERY PLAN SELECT name, price FROM products ORDER BY price;

-- Joins, subqueries and CTEs show up as nested nodes.
EXPLAIN QUERY PLAN
SELECT c.name, count(o.order_id)
FROM customers c LEFT JOIN orders o ON o.customer_id = c.customer_id
GROUP BY c.customer_id;

-- When no index helps a join, SQLite may build an AUTOMATIC index for the query. (warn)
EXPLAIN QUERY PLAN
SELECT e.kind, c.name FROM events e JOIN customers c ON c.name = e.kind;

-- Before and after CREATE INDEX: the same query goes from SCAN to SEARCH.
EXPLAIN QUERY PLAN SELECT * FROM products WHERE name = 'Chef Knife';
CREATE INDEX idx_products_name ON products (name);
EXPLAIN QUERY PLAN SELECT * FROM products WHERE name = 'Chef Knife';

-- ANALYZE gathers statistics (in sqlite_stat1) that help the planner choose between indexes.
ANALYZE;
SELECT tbl, idx, stat FROM sqlite_stat1 ORDER BY tbl, idx LIMIT 10;
EXPLAIN QUERY PLAN SELECT * FROM orders WHERE customer_id = 1 AND status = 'open';
