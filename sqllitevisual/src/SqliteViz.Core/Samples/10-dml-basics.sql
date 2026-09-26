-- DML basics: INSERT, UPDATE, DELETE, and what foreign keys and triggers do
-- A multi-row INSERT. customer_id is an INTEGER PRIMARY KEY, so SQLite assigns it.
INSERT INTO customers (name, email, city, country, created_at) VALUES
    ('Olga Ivanova', 'olga@example.com', 'Riga', 'LV', '2025-04-01 09:00:00'),
    ('Pablo Ruiz',   'pablo@example.es', 'Seville', 'ES', '2025-04-02 10:30:00');
SELECT customer_id, name, country FROM customers ORDER BY customer_id DESC LIMIT 2;

-- A column left out takes its DEFAULT (country defaults to 'US').
INSERT INTO customers (name, email, created_at) VALUES ('Quinn Taylor', 'quinn@example.com', '2025-04-03 12:00:00');
SELECT name, country FROM customers WHERE name = 'Quinn Taylor';

-- INSERT ... SELECT copies rows from a query: tag every Books product as 'new'.
INSERT INTO product_tags (product_id, tag_id)
SELECT product_id, 2 FROM products
WHERE category_id = 10 AND product_id NOT IN (SELECT product_id FROM product_tags WHERE tag_id = 2);

-- UPDATE with an expression.
UPDATE products SET stock = stock + 5 WHERE stock < 10;

-- UPDATE ... FROM (3.33+) joins another table into the update: mark paid orders with shipments as shipped.
UPDATE orders SET status = 'shipped', shipped_at = s.first_ship
FROM (SELECT order_id, min(shipped_on) AS first_ship FROM shipment_items GROUP BY order_id) AS s
WHERE orders.order_id = s.order_id AND orders.status <> 'shipped';

-- A trigger at work: changing a price writes a row to audit_log (trg_products_price_audit)
-- and stamps updated_at (trg_products_touch).
UPDATE products SET price = price * 0.9 WHERE product_id IN (3, 5);
SELECT table_name, row_id, action, old_value, new_value FROM audit_log;
SELECT product_id, name, price, updated_at IS NOT NULL AS touched FROM products WHERE product_id IN (3, 5);

-- DELETE with a subquery: remove tags that no product uses.
DELETE FROM tags WHERE tag_id NOT IN (SELECT tag_id FROM product_tags);

-- ON DELETE CASCADE: deleting a customer also deletes their one-to-one profile.
SELECT count(*) AS profiles_before FROM customer_profiles;
DELETE FROM customers WHERE name = 'Nina Petrova';
SELECT count(*) AS profiles_after FROM customer_profiles;

-- ON DELETE SET NULL: deleting a department keeps its employees but clears their dept_id.
DELETE FROM departments WHERE name = 'Finance';
SELECT first_name, dept_id FROM employees WHERE emp_id IN (15, 16);

-- ON DELETE CASCADE through a chain: deleting an open order removes its order_items.
SELECT count(*) AS items_before FROM order_items WHERE order_id = 16;
DELETE FROM orders WHERE order_id = 16;
SELECT count(*) AS items_after FROM order_items WHERE order_id = 16;

-- Deleting a customer who still has orders fails (ON DELETE RESTRICT), and a BEFORE DELETE trigger
-- refuses to delete shipped orders. Try them yourself; each stops the run with an error:
--   DELETE FROM customers WHERE customer_id = 1;
--   DELETE FROM orders WHERE order_id = 1;
SELECT 'done' AS result;
