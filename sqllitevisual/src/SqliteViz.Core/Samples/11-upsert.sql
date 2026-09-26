-- Upsert: INSERT ... ON CONFLICT
-- DO NOTHING: skip the row if it would violate a uniqueness constraint.
INSERT INTO tags (name) VALUES ('sale') ON CONFLICT (name) DO NOTHING;
INSERT INTO tags (name) VALUES ('limited') ON CONFLICT (name) DO NOTHING;
SELECT * FROM tags ORDER BY tag_id;

-- tags.name is COLLATE NOCASE, so 'SALE' conflicts with 'sale' too.
INSERT INTO tags (name) VALUES ('SALE') ON CONFLICT DO NOTHING;
SELECT count(*) AS sale_tags FROM tags WHERE name = 'sale';

-- DO UPDATE: the row that failed to insert is available as "excluded".
INSERT INTO products (sku, name, category_id, price, stock)
VALUES ('ACC-001', 'USB-C Hub', 4, 49.90, 30)
ON CONFLICT (sku) DO UPDATE SET stock = stock + excluded.stock;
SELECT sku, stock FROM products WHERE sku = 'ACC-001';

-- DO UPDATE ... WHERE: only update when the incoming price is lower.
INSERT INTO products (sku, name, category_id, price)
VALUES ('LAP-001', 'UltraBook 14', 3, 1199.00), ('LAP-002', 'WorkStation 16', 3, 2499.00)
ON CONFLICT (sku) DO UPDATE SET price = excluded.price WHERE excluded.price < products.price;
SELECT sku, price FROM products WHERE sku LIKE 'LAP-%';

-- Several ON CONFLICT clauses: each names the constraint it handles; the last may omit it.
INSERT INTO customers (customer_id, name, email, created_at)
VALUES (1, 'Alice Johnson', 'alice@example.com', '2025-05-01 00:00:00')
ON CONFLICT (customer_id) DO UPDATE SET email = excluded.email
ON CONFLICT DO NOTHING;

-- Upsert on a composite key: add to a line's quantity if the product is already on the order.
INSERT INTO order_items (order_id, product_id, quantity, unit_price)
VALUES (11, 15, 3, 25.00), (11, 3, 1, 49.90)
ON CONFLICT (order_id, product_id) DO UPDATE SET quantity = quantity + excluded.quantity;
SELECT order_id, product_id, quantity, line_total FROM order_items WHERE order_id = 11;

-- A counter table is the classic upsert use.
CREATE TABLE page_hits (path TEXT PRIMARY KEY, hits INTEGER NOT NULL);
INSERT INTO page_hits VALUES ('/', 1) ON CONFLICT (path) DO UPDATE SET hits = hits + 1;
INSERT INTO page_hits VALUES ('/', 1) ON CONFLICT (path) DO UPDATE SET hits = hits + 1;
INSERT INTO page_hits VALUES ('/about', 1) ON CONFLICT (path) DO UPDATE SET hits = hits + 1;
SELECT * FROM page_hits;
DROP TABLE page_hits;
