-- JSON functions
-- json() validates and minifies; json_valid() checks without failing.
SELECT json(' { "a" : [1, 2, 3] } ') AS minified, json_valid('{"a":1}') AS ok, json_valid('{a:1}') AS bad;

-- json_extract takes a path. -> returns JSON text, ->> returns an SQL value (3.38+).
SELECT name,
       json_extract(attributes, '$.cpu') AS cpu_extract,
       attributes -> '$.colors' AS colors_json,
       attributes ->> '$.colors[0]' AS first_color,
       attributes ->> 'ram_gb' AS ram_gb
FROM products WHERE category_id = 3;

-- json_type tells you what is at a path.
SELECT name, json_type(attributes) AS root, json_type(attributes, '$.colors') AS colors_type,
       json_type(attributes, '$.anc') AS anc_type
FROM products WHERE attributes IS NOT NULL LIMIT 6;

-- Build JSON: json_object and json_array.
SELECT json_object('id', customer_id, 'name', name, 'tags', json_array(country, city)) AS customer_json
FROM customers LIMIT 3;

-- Aggregate into JSON: json_group_array and json_group_object.
SELECT o.order_id,
       json_group_array(json_object('sku', p.sku, 'qty', oi.quantity)) AS items,
       json_group_object(p.sku, oi.line_total) AS totals_by_sku
FROM orders o
JOIN order_items oi ON oi.order_id = o.order_id
JOIN products p ON p.product_id = oi.product_id
WHERE o.order_id IN (1, 7)
GROUP BY o.order_id;

-- json_each turns an array (or object) into rows: one row per color, per product.
SELECT p.name, c.key AS position, c.value AS color
FROM products p, json_each(p.attributes, '$.colors') c
ORDER BY p.name, c.key;

-- Which products come in black?
SELECT name FROM products
WHERE EXISTS (SELECT 1 FROM json_each(attributes, '$.colors') WHERE value = 'black');

-- json_tree walks every element recursively, with its full path.
SELECT fullkey, type, atom FROM json_tree((SELECT payload FROM events WHERE event_id = 11));

-- Editing JSON: set inserts or replaces, insert only adds, replace only changes, remove deletes.
SELECT json_set(attributes, '$.ram_gb', 32, '$.gpu', 'integrated') AS set_,
       json_insert(attributes, '$.ram_gb', 99, '$.ssd_gb', 512) AS insert_,
       json_replace(attributes, '$.ram_gb', 24, '$.missing', 1) AS replace_,
       json_remove(attributes, '$.colors') AS remove_
FROM products WHERE product_id = 1;

-- json_patch merges an object into another (RFC 7396); null deletes a key.
SELECT json_patch('{"a":1,"b":{"c":2}}', '{"b":{"d":3},"a":null}') AS patched;

-- Updating stored JSON.
UPDATE products SET attributes = json_set(attributes, '$.warranty_years', 2) WHERE category_id = 3;
SELECT name, attributes FROM products WHERE category_id = 3;

-- jsonb (3.45+) stores JSON in a binary format (a BLOB); json() turns it back into text.
SELECT typeof(jsonb('{"a":1}')) AS storage, length(jsonb('{"a":[1,2,3]}')) AS bytes,
       json(jsonb('{"a":[1,2,3]}')) AS back_to_text, jsonb('{"a":1}') ->> '$.a' AS extracted;

-- Querying event payloads.
SELECT event_id, kind, payload ->> '$.order_id' AS order_id, payload ->> '$.total' AS total
FROM events WHERE kind = 'checkout';

-- An expression index on a JSON path lets SQLite SEARCH instead of SCAN.
CREATE INDEX idx_events_order ON events (payload ->> '$.order_id');
EXPLAIN QUERY PLAN SELECT event_id FROM events WHERE payload ->> '$.order_id' = 14;
SELECT event_id, kind FROM events WHERE payload ->> '$.order_id' = 14;
