-- Aggregation: GROUP BY, HAVING, aggregate functions
-- Aggregates over the whole table.
SELECT count(*) AS products, min(price) AS cheapest, max(price) AS priciest, avg(price) AS average
FROM products;

-- count(*) counts rows; count(column) skips NULLs.
SELECT count(*) AS customers, count(city) AS with_city FROM customers;

-- GROUP BY makes one row per group.
SELECT status, count(*) AS orders FROM orders GROUP BY status ORDER BY orders DESC;

-- HAVING filters groups (WHERE filters rows before grouping).
SELECT customer_id, count(*) AS orders
FROM orders
WHERE status <> 'cancelled'
GROUP BY customer_id
HAVING count(*) >= 2;

-- sum() returns NULL for no rows; total() returns 0.0 and always a REAL.
SELECT sum(quantity) AS sum_none, total(quantity) AS total_none FROM order_items WHERE order_id = -1;
SELECT sum(quantity) AS sum_all, total(quantity) AS total_all FROM order_items;

-- group_concat / string_agg join values from a group into one string.
SELECT c.name AS category, group_concat(p.name, ', ') AS products
FROM categories c JOIN products p ON p.category_id = c.category_id
GROUP BY c.category_id;

SELECT o.order_id, string_agg(p.sku, ' + ') AS skus
FROM orders o
JOIN order_items oi ON oi.order_id = o.order_id
JOIN products p ON p.product_id = oi.product_id
GROUP BY o.order_id
ORDER BY o.order_id;

-- An aggregate with FILTER (WHERE ...) only sees matching rows: a pivot in one pass.
SELECT strftime('%Y', order_date) AS year,
       count(*) AS all_orders,
       count(*) FILTER (WHERE status = 'shipped') AS shipped,
       count(*) FILTER (WHERE status = 'open') AS open,
       count(*) FILTER (WHERE status = 'cancelled') AS cancelled
FROM orders
GROUP BY year;

-- Revenue per category, using the generated line_total column.
SELECT c.name AS category, round(sum(oi.line_total), 2) AS revenue, sum(oi.quantity) AS units
FROM order_items oi
JOIN products p ON p.product_id = oi.product_id
JOIN categories c ON c.category_id = p.category_id
GROUP BY c.name
ORDER BY revenue DESC;
