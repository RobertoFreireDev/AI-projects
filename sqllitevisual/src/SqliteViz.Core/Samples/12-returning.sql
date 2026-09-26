-- RETURNING: get rows back from INSERT, UPDATE and DELETE
-- INSERT ... RETURNING gives you the generated ids without a second query.
INSERT INTO tags (name) VALUES ('refurbished'), ('bundle')
RETURNING tag_id, name;

-- Computed expressions and generated columns can be returned too.
INSERT INTO products (sku, name, category_id, price, stock)
VALUES ('ACC-004', 'Laptop Stand', 4, 59.00, 20)
RETURNING product_id, sku, price, price_with_tax, price_with_tax - price AS tax;

-- UPDATE ... RETURNING shows the new values of every changed row.
UPDATE products SET price = round(price * 1.05, 2)
WHERE category_id = 10
RETURNING product_id, name, price AS new_price;

-- DELETE ... RETURNING shows what was removed.
DELETE FROM product_tags WHERE tag_id = 8
RETURNING product_id, tag_id;

-- RETURNING with the rows' own ids and a CASE.
UPDATE orders SET status = 'paid'
WHERE status = 'open' AND order_id IN (11, 15)
RETURNING order_id, status, CASE WHEN notes IS NULL THEN 'no notes' ELSE notes END AS notes;

-- Upsert + RETURNING: returns the row whether it was inserted or updated.
INSERT INTO tags (name) VALUES ('bundle')
ON CONFLICT (name) DO UPDATE SET name = excluded.name
RETURNING tag_id, name;
