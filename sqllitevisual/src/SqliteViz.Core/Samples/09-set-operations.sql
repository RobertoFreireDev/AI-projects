-- Set operations: UNION, UNION ALL, INTERSECT, EXCEPT
-- UNION combines results and removes duplicates. Column count and order must match.
SELECT city FROM customers WHERE city IS NOT NULL
UNION
SELECT location FROM departments WHERE location IS NOT NULL
ORDER BY 1;

-- UNION ALL keeps duplicates (and is cheaper, because nothing is compared).
SELECT city FROM customers WHERE city IS NOT NULL
UNION ALL
SELECT location FROM departments WHERE location IS NOT NULL
ORDER BY 1;

-- INTERSECT: values present in both results. Customers who ordered in 2024 AND in 2025.
SELECT customer_id FROM orders WHERE order_date LIKE '2024%'
INTERSECT
SELECT customer_id FROM orders WHERE order_date LIKE '2025%';

-- EXCEPT: values in the first result but not the second. Products never ordered.
SELECT product_id FROM products
EXCEPT
SELECT product_id FROM order_items;

-- A labelled union: a single contact list from two tables.
SELECT 'customer' AS kind, name, email FROM customers
UNION ALL
SELECT 'employee', first_name || ' ' || last_name, email FROM employees
ORDER BY kind, name
LIMIT 10;

-- Set operations compare whole rows. NULLs count as equal here (unlike in "=").
SELECT NULL AS x, 1 AS y
UNION
SELECT NULL, 1;
