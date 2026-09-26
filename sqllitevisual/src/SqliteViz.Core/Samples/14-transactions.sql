-- Transactions and savepoints
-- Outside BEGIN, every statement is its own transaction (autocommit).
-- BEGIN ... COMMIT groups statements so they succeed or fail together.
BEGIN;
UPDATE products SET stock = stock - 1 WHERE product_id = 1;
INSERT INTO orders (customer_id, order_date, status) VALUES (12, '2025-06-01', 'open');
INSERT INTO order_items (order_id, product_id, quantity, unit_price)
VALUES (last_insert_rowid(), 1, 1, 1299.00);
COMMIT;
SELECT order_id, customer, total FROM v_order_totals WHERE customer_id = 12;

-- ROLLBACK undoes everything since BEGIN.
BEGIN;
DELETE FROM product_tags;
SELECT count(*) AS links_inside_transaction FROM product_tags;
ROLLBACK;
SELECT count(*) AS links_after_rollback FROM product_tags;

-- SAVEPOINTs are named, nestable points you can roll back to without ending the transaction.
BEGIN;
INSERT INTO tags (name) VALUES ('keep-me');
SAVEPOINT before_mistake;
DELETE FROM tags;
SELECT count(*) AS tags_after_mistake FROM tags;
ROLLBACK TO before_mistake;
SELECT count(*) AS tags_after_rollback_to FROM tags;
RELEASE before_mistake;
COMMIT;
SELECT name FROM tags WHERE name = 'keep-me';

-- A SAVEPOINT outside BEGIN starts a transaction itself; RELEASE of the outermost one commits.
SAVEPOINT outer_sp;
UPDATE tags SET name = upper(name) WHERE name = 'keep-me';
RELEASE outer_sp;
SELECT name FROM tags WHERE name = 'KEEP-ME';

-- Deferred foreign keys: normally a foreign key is checked after each statement.
-- PRAGMA defer_foreign_keys = ON postpones the checks to COMMIT, so rows can be inserted
-- in any order within the transaction. (It turns itself off again at COMMIT.)
BEGIN;
PRAGMA defer_foreign_keys = ON;
INSERT INTO employees (emp_id, first_name, last_name, email, dept_id, manager_id, title, salary, hire_date)
VALUES (100, 'Tess', 'Lead', 'tess@corp.example', 6, NULL, 'Lab Lead', 99000, '2025-01-01');
INSERT INTO departments (dept_id, name, budget, location) VALUES (6, 'Labs', 50000, 'Braga');
COMMIT;
SELECT e.first_name, d.name FROM employees e JOIN departments d USING (dept_id) WHERE e.emp_id = 100;

-- If you leave a transaction open (BEGIN without COMMIT), the top bar shows "Transaction open".
-- It stays open across runs until you COMMIT or ROLLBACK, or press Reset DB.
