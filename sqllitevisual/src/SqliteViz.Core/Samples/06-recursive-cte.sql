-- Recursive CTEs: series, trees, graphs
-- A number series: the anchor row (1), then the recursive step until the WHERE stops it.
WITH RECURSIVE n(i) AS (
    SELECT 1
    UNION ALL
    SELECT i + 1 FROM n WHERE i < 10
)
SELECT i, i * i AS square FROM n;

-- The category tree with depth and a readable path.
WITH RECURSIVE tree(category_id, name, depth, path) AS (
    SELECT category_id, name, 0, name FROM categories WHERE parent_id IS NULL
    UNION ALL
    SELECT c.category_id, c.name, t.depth + 1, t.path || ' / ' || c.name
    FROM categories c JOIN tree t ON c.parent_id = t.category_id
)
SELECT substr('            ', 1, depth * 3) || name AS indented, depth, path
FROM tree
ORDER BY path;

-- Org chart: everyone under the CTO (Grace Hopper, emp_id 2), with their level below her.
WITH RECURSIVE reports(emp_id, name, level) AS (
    SELECT emp_id, first_name || ' ' || last_name, 0 FROM employees WHERE emp_id = 2
    UNION ALL
    SELECT e.emp_id, e.first_name || ' ' || e.last_name, r.level + 1
    FROM employees e JOIN reports r ON e.manager_id = r.emp_id
)
SELECT * FROM reports;

-- The same idea is saved as a view: v_employee_tree.
SELECT depth, path, title FROM v_employee_tree ORDER BY path;

-- Walking up instead of down: the chain of managers above Pete Campbell (emp_id 11).
WITH RECURSIVE chain(emp_id, name, manager_id, step) AS (
    SELECT emp_id, first_name || ' ' || last_name, manager_id, 0 FROM employees WHERE emp_id = 11
    UNION ALL
    SELECT e.emp_id, e.first_name || ' ' || e.last_name, e.manager_id, c.step + 1
    FROM employees e JOIN chain c ON e.emp_id = c.manager_id
)
SELECT step, name FROM chain;

-- Route search over a graph WITH cycles: every path from Lisbon to Munich.
-- The visited list (',1,3,') stops a path from entering a city twice, so cycles cannot loop forever.
WITH RECURSIVE paths(city_id, route, visited, km, hops) AS (
    SELECT city_id, name, ',' || city_id || ',', 0, 0 FROM cities WHERE name = 'Lisbon'
    UNION ALL
    SELECT r.to_city, p.route || ' -> ' || c.name, p.visited || r.to_city || ',', p.km + r.km, p.hops + 1
    FROM paths p
    JOIN routes r ON r.from_city = p.city_id
    JOIN cities c ON c.city_id = r.to_city
    WHERE instr(p.visited, ',' || r.to_city || ',') = 0
)
SELECT route, km, hops
FROM paths
WHERE city_id = (SELECT city_id FROM cities WHERE name = 'Munich')
ORDER BY km;

-- A date series: every day in February 2024 (a leap year), with its weekday.
WITH RECURSIVE days(day) AS (
    SELECT date('2024-02-01')
    UNION ALL
    SELECT date(day, '+1 day') FROM days WHERE day < '2024-02-29'
)
SELECT day, strftime('%w', day) AS weekday_number,
       CASE strftime('%w', day) WHEN '0' THEN 'Sun' WHEN '6' THEN 'Sat' ELSE 'weekday' END AS kind
FROM days;

-- Filling gaps: orders per month, including months with no orders.
WITH RECURSIVE months(m) AS (
    SELECT '2024-01'
    UNION ALL
    SELECT strftime('%Y-%m', m || '-01', '+1 month') FROM months WHERE m < '2025-03'
)
SELECT months.m AS month, count(o.order_id) AS orders
FROM months
LEFT JOIN orders o ON strftime('%Y-%m', o.order_date) = months.m
GROUP BY months.m;
