-- Common table expressions (WITH)
-- A CTE names a subquery so the main query reads top to bottom.
WITH big_orders AS (
    SELECT order_id, total FROM v_order_totals WHERE total > 400
)
SELECT * FROM big_orders ORDER BY total DESC;

-- Several CTEs, the later ones building on the earlier ones.
WITH
    customer_revenue AS (
        SELECT customer_id, sum(total) AS revenue
        FROM v_order_totals
        WHERE status <> 'cancelled'
        GROUP BY customer_id
    ),
    ranked AS (
        SELECT customer_id, revenue,
               (SELECT count(*) FROM customer_revenue r2 WHERE r2.revenue > r1.revenue) + 1 AS position
        FROM customer_revenue r1
    )
SELECT c.name, ranked.revenue, ranked.position
FROM ranked JOIN customers c USING (customer_id)
ORDER BY ranked.position;

-- A CTE can be referenced more than once.
WITH monthly AS (
    SELECT strftime('%Y-%m', order_date) AS month, count(*) AS orders
    FROM orders GROUP BY month
)
SELECT month, orders, (SELECT avg(orders) FROM monthly) AS average_per_month
FROM monthly
WHERE orders >= (SELECT avg(orders) FROM monthly);

-- MATERIALIZED computes the CTE once into a temporary table; NOT MATERIALIZED lets the planner
-- inline it like a view. Compare the two query plans.
EXPLAIN QUERY PLAN
WITH expensive AS MATERIALIZED (SELECT * FROM products WHERE price > 100)
SELECT name FROM expensive WHERE product_id = 2;

EXPLAIN QUERY PLAN
WITH expensive AS NOT MATERIALIZED (SELECT * FROM products WHERE price > 100)
SELECT name FROM expensive WHERE product_id = 2;

-- CTEs also work in front of INSERT, UPDATE and DELETE.
WITH discontinued AS (SELECT product_id FROM products WHERE discontinued = 1)
UPDATE products SET stock = 0 WHERE product_id IN (SELECT product_id FROM discontinued);
