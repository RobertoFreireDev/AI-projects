-- Window functions
-- Ranking within each department. ROW_NUMBER is always unique; RANK leaves gaps after ties;
-- DENSE_RANK does not.
SELECT d.name AS department, e.first_name, e.salary,
       ROW_NUMBER() OVER w AS row_number,
       RANK()       OVER w AS rank,
       DENSE_RANK() OVER w AS dense_rank
FROM employees e
JOIN departments d ON d.dept_id = e.dept_id
WINDOW w AS (PARTITION BY e.dept_id ORDER BY e.salary DESC);

-- Ties: order_items with equal quantities share a rank.
SELECT order_id, product_id, quantity,
       RANK() OVER (ORDER BY quantity DESC) AS rank,
       DENSE_RANK() OVER (ORDER BY quantity DESC) AS dense_rank
FROM order_items;

-- NTILE splits rows into N buckets; PERCENT_RANK and CUME_DIST give relative positions.
SELECT name, price,
       NTILE(4) OVER (ORDER BY price) AS quartile,
       round(PERCENT_RANK() OVER (ORDER BY price), 3) AS percent_rank,
       round(CUME_DIST() OVER (ORDER BY price), 3) AS cume_dist
FROM products;

-- LAG and LEAD look at the previous and next row: days since the customer's previous order.
SELECT customer_id, order_id, order_date,
       LAG(order_date) OVER (PARTITION BY customer_id ORDER BY order_date) AS previous_order,
       julianday(order_date) - julianday(LAG(order_date) OVER (PARTITION BY customer_id ORDER BY order_date)) AS days_between,
       LEAD(order_date, 1, 'none yet') OVER (PARTITION BY customer_id ORDER BY order_date) AS next_order
FROM orders
ORDER BY customer_id, order_date;

-- FIRST_VALUE, LAST_VALUE and NTH_VALUE. LAST_VALUE needs a frame that reaches the end of the
-- partition; the default frame stops at the current row.
SELECT d.name AS department, e.first_name, e.salary,
       FIRST_VALUE(e.first_name) OVER w AS top_earner,
       LAST_VALUE(e.first_name) OVER w AS lowest_earner,
       NTH_VALUE(e.first_name, 2) OVER w AS second
FROM employees e JOIN departments d ON d.dept_id = e.dept_id
WINDOW w AS (PARTITION BY e.dept_id ORDER BY e.salary DESC
             ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING);

-- Running total of revenue by order date.
SELECT order_id, order_date, total,
       sum(total) OVER (ORDER BY order_date, order_id) AS running_total
FROM v_order_totals
WHERE status <> 'cancelled';

-- Moving averages with three frame types:
-- ROWS counts physical rows, RANGE uses the ORDER BY value, GROUPS counts peer groups (ties).
SELECT order_id, quantity,
       round(avg(quantity) OVER (ORDER BY quantity ROWS BETWEEN 1 PRECEDING AND 1 FOLLOWING), 2) AS rows_avg,
       round(avg(quantity) OVER (ORDER BY quantity RANGE BETWEEN 1 PRECEDING AND 1 FOLLOWING), 2) AS range_avg,
       round(avg(quantity) OVER (ORDER BY quantity GROUPS BETWEEN 1 PRECEDING AND CURRENT ROW), 2) AS groups_avg
FROM order_items;

-- EXCLUDE removes rows from the frame: compare each salary with the rest of the department.
SELECT e.first_name, e.dept_id, e.salary,
       avg(e.salary) OVER (PARTITION BY e.dept_id
                           ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
                           EXCLUDE CURRENT ROW) AS avg_of_others
FROM employees e
WHERE e.dept_id IS NOT NULL;

-- Top-N per group: the two most expensive products in each category.
SELECT category, name, price
FROM (
    SELECT c.name AS category, p.name, p.price,
           ROW_NUMBER() OVER (PARTITION BY p.category_id ORDER BY p.price DESC) AS rn
    FROM products p JOIN categories c ON c.category_id = p.category_id
)
WHERE rn <= 2
ORDER BY category, price DESC;

-- Share of the total: each category's revenue as a percentage.
SELECT category, revenue, round(100.0 * revenue / sum(revenue) OVER (), 1) AS pct
FROM (
    SELECT c.name AS category, sum(oi.line_total) AS revenue
    FROM order_items oi
    JOIN products p ON p.product_id = oi.product_id
    JOIN categories c ON c.category_id = p.category_id
    GROUP BY c.name
)
ORDER BY revenue DESC;
