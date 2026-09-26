-- Subqueries: scalar, correlated, EXISTS, IN, derived tables, ANY/ALL equivalents
-- A scalar subquery returns one value and can appear anywhere an expression can.
SELECT name, price, (SELECT round(avg(price), 2) FROM products) AS average_price
FROM products
WHERE price > (SELECT avg(price) FROM products);

-- A correlated subquery refers to the outer row: each customer's order count.
SELECT c.name,
       (SELECT count(*) FROM orders o WHERE o.customer_id = c.customer_id) AS orders
FROM customers c
ORDER BY orders DESC, c.name;

-- EXISTS: customers who have at least one open order.
SELECT c.name FROM customers c
WHERE EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = c.customer_id AND o.status = 'open');

-- NOT EXISTS: customers with no orders at all.
SELECT c.name FROM customers c
WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = c.customer_id);

-- IN with a subquery: products that appear in any shipped order.
SELECT name FROM products
WHERE product_id IN (
    SELECT oi.product_id FROM order_items oi JOIN orders o USING (order_id) WHERE o.status = 'shipped'
);

-- The NOT IN pitfall: if the subquery returns a NULL, NOT IN matches nothing.
-- products.category_id has a NULL (the Gift Card), so this returns zero rows:
SELECT name FROM categories WHERE category_id NOT IN (SELECT category_id FROM products);
-- Filter out the NULLs, or use NOT EXISTS, to get the categories with no direct products:
SELECT name FROM categories WHERE category_id NOT IN (SELECT category_id FROM products WHERE category_id IS NOT NULL);
SELECT name FROM categories c WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.category_id = c.category_id);

-- A derived table (subquery in FROM) behaves like a temporary result you can join.
SELECT t.customer_id, c.name, t.total_spent
FROM (
    SELECT customer_id, sum(total) AS total_spent
    FROM v_order_totals WHERE status <> 'cancelled'
    GROUP BY customer_id
) t
JOIN customers c USING (customer_id)
WHERE t.total_spent > 500
ORDER BY t.total_spent DESC;

-- ANY / ALL: SQLite has NO "> ANY (subquery)" or "> ALL (subquery)" operators. Use these equivalents.
-- "price > ALL (prices in Books)" is the same as "price > the maximum price in Books":
SELECT name, price FROM products
WHERE price > (SELECT max(p.price) FROM products p JOIN categories c USING (category_id) WHERE c.name = 'Programming');

-- "price > ANY (prices in Books)" is the same as "price > the minimum price in Books":
SELECT name, price FROM products
WHERE price > (SELECT min(p.price) FROM products p JOIN categories c USING (category_id) WHERE c.name = 'Programming');

-- The same "> ANY" with EXISTS, and "> ALL" with NOT EXISTS (these also handle empty sets and NULLs explicitly):
SELECT name, price FROM products x
WHERE EXISTS (SELECT 1 FROM products p WHERE p.category_id = 10 AND x.price > p.price);

SELECT name, price FROM products x
WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.category_id = 10 AND x.price <= p.price);

-- "= ANY" is simply IN:
SELECT name FROM products WHERE price IN (SELECT unit_price FROM order_items WHERE discount > 0);
