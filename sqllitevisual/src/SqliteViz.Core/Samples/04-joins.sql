-- Joins: inner, outer, cross, natural, self, many-to-many, anti-join
-- INNER JOIN keeps only rows that match on both sides.
SELECT o.order_id, c.name, o.order_date
FROM orders o
INNER JOIN customers c ON c.customer_id = o.customer_id
ORDER BY o.order_id;

-- LEFT OUTER JOIN keeps every customer; customers without orders get NULLs.
SELECT c.name, o.order_id
FROM customers c
LEFT OUTER JOIN orders o ON o.customer_id = c.customer_id
ORDER BY c.customer_id, o.order_id;

-- RIGHT JOIN (SQLite 3.39+) keeps every row of the right table: here every department.
SELECT e.first_name, d.name AS department
FROM employees e
RIGHT JOIN departments d ON d.dept_id = e.dept_id
ORDER BY d.dept_id;

-- FULL OUTER JOIN keeps unmatched rows from both sides:
-- the CEO has no department, and Research has no employees.
SELECT e.first_name, d.name AS department
FROM employees e
FULL OUTER JOIN departments d ON d.dept_id = e.dept_id
WHERE e.emp_id IS NULL OR d.dept_id IS NULL;

-- CROSS JOIN: every combination (Cartesian product).
SELECT t.name AS tag, s.status
FROM tags t
CROSS JOIN (SELECT DISTINCT status FROM orders) s
WHERE t.tag_id <= 2;

-- USING (column) joins on columns with the same name and shows them once.
SELECT order_id, customer_id, name, order_date
FROM orders JOIN customers USING (customer_id)
WHERE order_id <= 3;

-- NATURAL JOIN joins on ALL same-named columns. Handy, but fragile when columns are added later.
SELECT customer_id, name, loyalty_tier FROM customers NATURAL JOIN customer_profiles;

-- Self join: each employee next to their manager.
SELECT e.first_name || ' ' || e.last_name AS employee,
       m.first_name || ' ' || m.last_name AS manager
FROM employees e
LEFT JOIN employees m ON m.emp_id = e.manager_id
ORDER BY e.emp_id;

-- Many-to-many through a junction table: products and their tags.
SELECT p.name AS product, group_concat(t.name, ', ') AS tags
FROM products p
JOIN product_tags pt ON pt.product_id = p.product_id
JOIN tags t ON t.tag_id = pt.tag_id
GROUP BY p.product_id;

-- Anti-join: products that were never ordered (LEFT JOIN ... WHERE right side IS NULL).
SELECT p.product_id, p.name
FROM products p
LEFT JOIN order_items oi ON oi.product_id = p.product_id
WHERE oi.product_id IS NULL;

-- Joining with a composite key: shipped quantities against ordered quantities.
SELECT oi.order_id, oi.product_id, oi.quantity AS ordered, coalesce(sum(si.quantity), 0) AS shipped
FROM order_items oi
LEFT JOIN shipment_items si ON si.order_id = oi.order_id AND si.product_id = oi.product_id
JOIN orders o ON o.order_id = oi.order_id AND o.status = 'shipped'
GROUP BY oi.order_id, oi.product_id;
