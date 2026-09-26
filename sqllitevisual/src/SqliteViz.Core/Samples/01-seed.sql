-- Seed: store + company schema and data
-- Runs automatically for every new session. Running it again rebuilds everything (it is idempotent).
-- Every relationship type, constraint and index kind appears at least once. The data is deterministic.

-- Drop in reverse dependency order: views first, then child tables before their parents.
DROP VIEW IF EXISTS v_employee_tree;
DROP VIEW IF EXISTS v_order_totals;
DROP TABLE IF EXISTS shipment_items;
DROP TABLE IF EXISTS order_items;
DROP TABLE IF EXISTS product_tags;
DROP TABLE IF EXISTS events;
DROP TABLE IF EXISTS audit_log;
DROP TABLE IF EXISTS routes;
DROP TABLE IF EXISTS cities;
DROP TABLE IF EXISTS orders;
DROP TABLE IF EXISTS customer_profiles;
DROP TABLE IF EXISTS customers;
DROP TABLE IF EXISTS products;
DROP TABLE IF EXISTS tags;
DROP TABLE IF EXISTS categories;
DROP TABLE IF EXISTS employees;
DROP TABLE IF EXISTS departments;

BEGIN;

-- Company: one-to-many (departments -> employees) and a self-referencing hierarchy (manager_id).
CREATE TABLE departments (
    dept_id   INTEGER PRIMARY KEY,               -- rowid alias
    name      TEXT NOT NULL UNIQUE,
    budget    REAL CHECK (budget >= 0),
    location  TEXT                                -- optional: NULL for one department
);

CREATE TABLE employees (
    emp_id     INTEGER PRIMARY KEY,
    first_name TEXT NOT NULL,
    last_name  TEXT NOT NULL,
    email      TEXT NOT NULL UNIQUE COLLATE NOCASE,
    dept_id    INTEGER REFERENCES departments (dept_id) ON DELETE SET NULL,
    manager_id INTEGER REFERENCES employees (emp_id) ON DELETE SET NULL,
    title      TEXT NOT NULL,
    salary     REAL NOT NULL CHECK (salary > 0),
    hire_date  TEXT NOT NULL                     -- ISO-8601 date
);

-- Store: customers, a one-to-one profile, orders, products, categories (tree) and tags.
CREATE TABLE customers (
    customer_id INTEGER PRIMARY KEY,
    name        TEXT NOT NULL COLLATE NOCASE,
    email       TEXT NOT NULL,
    city        TEXT,
    country     TEXT NOT NULL DEFAULT 'US',
    created_at  TEXT NOT NULL                    -- ISO-8601 date and time
);

-- One-to-one: the profile's primary key is also the foreign key.
CREATE TABLE customer_profiles (
    customer_id  INTEGER PRIMARY KEY REFERENCES customers (customer_id) ON DELETE CASCADE,
    birth_date   TEXT,
    phone        TEXT,
    loyalty_tier TEXT NOT NULL DEFAULT 'bronze' CHECK (loyalty_tier IN ('bronze', 'silver', 'gold')),
    newsletter   INTEGER NOT NULL DEFAULT 0 CHECK (newsletter IN (0, 1))
);

CREATE TABLE categories (
    category_id INTEGER PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    parent_id   INTEGER REFERENCES categories (category_id) ON DELETE CASCADE
);

CREATE TABLE products (
    product_id     INTEGER PRIMARY KEY,
    sku            TEXT NOT NULL UNIQUE,
    name           TEXT NOT NULL,
    category_id    INTEGER REFERENCES categories (category_id) ON DELETE SET NULL,
    price          REAL NOT NULL CHECK (price >= 0),
    stock          INTEGER NOT NULL DEFAULT 0,
    attributes     TEXT CHECK (json_valid(attributes)),     -- JSON text, NULL allowed
    discontinued   INTEGER NOT NULL DEFAULT 0,
    price_with_tax REAL GENERATED ALWAYS AS (round(price * 1.23, 2)) STORED,
    updated_at     TEXT                                     -- set by trg_products_touch
);

CREATE TABLE tags (
    tag_id INTEGER PRIMARY KEY,
    name   TEXT NOT NULL UNIQUE COLLATE NOCASE
);

-- Many-to-many junction, stored WITHOUT ROWID (the composite key is the table's b-tree key).
CREATE TABLE product_tags (
    product_id INTEGER NOT NULL REFERENCES products (product_id) ON DELETE CASCADE,
    tag_id     INTEGER NOT NULL REFERENCES tags (tag_id) ON DELETE CASCADE,
    PRIMARY KEY (product_id, tag_id)
) WITHOUT ROWID;

CREATE TABLE orders (
    order_id    INTEGER PRIMARY KEY,
    customer_id INTEGER NOT NULL REFERENCES customers (customer_id) ON DELETE RESTRICT,
    order_date  TEXT NOT NULL,
    status      TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'paid', 'shipped', 'cancelled')),
    shipped_at  TEXT,
    notes       TEXT
);

-- Many-to-many between orders and products, with a composite primary key and a VIRTUAL generated column.
CREATE TABLE order_items (
    order_id   INTEGER NOT NULL REFERENCES orders (order_id) ON DELETE CASCADE,
    product_id INTEGER NOT NULL REFERENCES products (product_id) ON DELETE RESTRICT,
    quantity   INTEGER NOT NULL CHECK (quantity > 0),
    unit_price REAL NOT NULL CHECK (unit_price >= 0),
    discount   REAL NOT NULL DEFAULT 0 CHECK (discount BETWEEN 0 AND 1),
    line_total REAL GENERATED ALWAYS AS (round(quantity * unit_price * (1 - discount), 2)) VIRTUAL,
    PRIMARY KEY (order_id, product_id)
);

-- Composite foreign key: (order_id, product_id) must be an existing order line.
CREATE TABLE shipment_items (
    shipment_item_id INTEGER PRIMARY KEY,
    order_id         INTEGER NOT NULL,
    product_id       INTEGER NOT NULL,
    quantity         INTEGER NOT NULL CHECK (quantity > 0),
    carrier          TEXT NOT NULL,
    shipped_on       TEXT NOT NULL,
    tracking         TEXT,
    FOREIGN KEY (order_id, product_id) REFERENCES order_items (order_id, product_id) ON DELETE NO ACTION
);

-- Graph data for recursive path search. The routes contain cycles.
CREATE TABLE cities (
    city_id INTEGER PRIMARY KEY,
    name    TEXT NOT NULL UNIQUE,
    country TEXT NOT NULL,
    lat     REAL,
    lon     REAL
);

CREATE TABLE routes (
    from_city INTEGER NOT NULL REFERENCES cities (city_id) ON DELETE CASCADE,
    to_city   INTEGER NOT NULL REFERENCES cities (city_id) ON DELETE CASCADE,
    km        INTEGER NOT NULL CHECK (km > 0),
    PRIMARY KEY (from_city, to_city),
    CHECK (from_city <> to_city)
) WITHOUT ROWID;

-- STRICT table: values must match the declared types. occurred_at is a Unix epoch INTEGER, for contrast
-- with the ISO-8601 TEXT dates everywhere else.
CREATE TABLE events (
    event_id    INTEGER PRIMARY KEY,
    occurred_at INTEGER NOT NULL,
    kind        TEXT NOT NULL,
    customer_id INTEGER REFERENCES customers (customer_id) ON DELETE SET NULL,
    payload     TEXT NOT NULL CHECK (json_valid(payload))
) STRICT;

-- Written by trg_products_price_audit. AUTOINCREMENT never reuses an id, even after deletes.
CREATE TABLE audit_log (
    audit_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name TEXT NOT NULL,
    row_id     INTEGER NOT NULL,
    action     TEXT NOT NULL,
    old_value  TEXT,
    new_value  TEXT,
    changed_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Indexes: single-column, UNIQUE on an expression, partial, composite, and covering.
CREATE INDEX idx_employees_dept ON employees (dept_id);
CREATE UNIQUE INDEX ux_customers_email ON customers (lower(email));
CREATE INDEX idx_orders_open ON orders (order_date) WHERE status = 'open';
CREATE INDEX idx_orders_customer_date ON orders (customer_id, order_date);
CREATE INDEX idx_order_items_product ON order_items (product_id, quantity, unit_price);

CREATE VIEW v_order_totals AS
SELECT o.order_id,
       o.customer_id,
       c.name                                   AS customer,
       o.order_date,
       o.status,
       count(oi.product_id)                     AS lines,
       coalesce(sum(oi.quantity), 0)            AS units,
       round(coalesce(sum(oi.line_total), 0), 2) AS total
FROM orders o
JOIN customers c ON c.customer_id = o.customer_id
LEFT JOIN order_items oi ON oi.order_id = o.order_id
GROUP BY o.order_id;

CREATE VIEW v_employee_tree AS
WITH RECURSIVE tree (emp_id, name, title, manager_id, depth, path) AS (
    SELECT emp_id, first_name || ' ' || last_name, title, manager_id, 0, first_name || ' ' || last_name
    FROM employees
    WHERE manager_id IS NULL
    UNION ALL
    SELECT e.emp_id, e.first_name || ' ' || e.last_name, e.title, e.manager_id, t.depth + 1,
           t.path || ' > ' || e.first_name || ' ' || e.last_name
    FROM employees e
    JOIN tree t ON e.manager_id = t.emp_id
)
SELECT emp_id, name, title, manager_id, depth, path FROM tree;

-- Audit trigger: every price change is written to audit_log.
CREATE TRIGGER trg_products_price_audit
AFTER UPDATE OF price ON products
WHEN OLD.price IS NOT NEW.price
BEGIN
    INSERT INTO audit_log (table_name, row_id, action, old_value, new_value)
    VALUES ('products', NEW.product_id, 'UPDATE price', OLD.price, NEW.price);
END;

-- Touch trigger: keeps updated_at current. It lists the columns it watches, and updated_at is not one of
-- them, so its own UPDATE does not fire it again (recursive_triggers is on in this build).
CREATE TRIGGER trg_products_touch
AFTER UPDATE OF sku, name, category_id, price, stock, attributes, discontinued ON products
BEGIN
    UPDATE products SET updated_at = datetime('now') WHERE product_id = NEW.product_id;
END;

-- Guard trigger: shipped orders cannot be deleted.
CREATE TRIGGER trg_orders_protect_shipped
BEFORE DELETE ON orders
WHEN OLD.status = 'shipped'
BEGIN
    SELECT RAISE(ABORT, 'Shipped orders cannot be deleted');
END;

-- Data. Research has no employees, the CEO has no department (NULL).
INSERT INTO departments (dept_id, name, budget, location) VALUES
    (1, 'Engineering', 500000, 'Lisbon'),
    (2, 'Sales',       250000, 'Porto'),
    (3, 'Support',     120000, 'Lisbon'),
    (4, 'Finance',     180000, NULL),
    (5, 'Research',     90000, 'Coimbra');

INSERT INTO employees (emp_id, first_name, last_name, email, dept_id, manager_id, title, salary, hire_date) VALUES
    (1,  'Ada',       'Lovelace', 'ada@corp.example',       NULL, NULL, 'CEO',                 250000, '2015-03-01'),
    (2,  'Grace',     'Hopper',   'grace@corp.example',     1,    1,    'CTO',                 180000, '2016-06-15'),
    (3,  'Alan',      'Turing',   'alan@corp.example',      1,    2,    'Principal Engineer',  150000, '2017-01-10'),
    (4,  'Linus',     'Torvalds', 'linus@corp.example',     1,    2,    'Engineering Manager', 140000, '2018-09-03'),
    (5,  'Margaret',  'Hamilton', 'margaret@corp.example',  1,    4,    'Senior Engineer',     125000, '2019-04-22'),
    (6,  'Ken',       'Thompson', 'ken@corp.example',       1,    4,    'Engineer',             98000, '2021-11-08'),
    (7,  'Barbara',   'Liskov',   'barbara@corp.example',   1,    3,    'Engineer',            102000, '2022-02-14'),
    (8,  'Don',       'Draper',   'don@corp.example',       2,    1,    'VP Sales',            160000, '2016-01-04'),
    (9,  'Peggy',     'Olson',    'peggy@corp.example',     2,    8,    'Account Executive',    85000, '2020-07-01'),
    (10, 'Joan',      'Holloway', 'joan@corp.example',      2,    8,    'Account Executive',    88000, '2019-10-15'),
    (11, 'Pete',      'Campbell', 'pete@corp.example',      2,    10,   'Sales Associate',      62000, '2023-03-20'),
    (12, 'Leslie',    'Knope',    'leslie@corp.example',    3,    1,    'Support Lead',         90000, '2018-05-30'),
    (13, 'Ron',       'Swanson',  'ron@corp.example',       3,    12,   'Support Engineer',     70000, '2021-08-16'),
    (14, 'April',     'Ludgate',  'april@corp.example',     3,    12,   'Support Engineer',     68000, '2024-01-08'),
    (15, 'Oscar',     'Martinez', 'oscar@corp.example',     4,    1,    'Controller',          115000, '2017-12-01'),
    (16, 'Angela',    'Martin',   'angela@corp.example',    4,    15,   'Accountant',           72000, '2022-06-06');

-- 'Grace Lee' and 'grace lee' look like duplicates but are different customers. Nina has no orders.
INSERT INTO customers (customer_id, name, email, city, country, created_at) VALUES
    (1,  'Alice Johnson', 'alice@example.com',     'Lisbon',    'PT', '2023-01-15 10:00:00'),
    (2,  'Bob Smith',     'bob@example.com',       'Porto',     'PT', '2023-02-03 12:30:00'),
    (3,  'Carla Gomez',   'carla@example.es',      'Madrid',    'ES', '2023-03-21 09:15:00'),
    (4,  'Grace Lee',     'grace.lee@example.com', 'Boston',    'US', '2023-04-02 16:45:00'),
    (5,  'grace lee',     'glee@example.org',      'Seattle',   'US', '2023-05-11 08:05:00'),
    (6,  'Hiro Tanaka',   'hiro@example.jp',       'Tokyo',     'JP', '2023-06-30 23:10:00'),
    (7,  'Ingrid Berg',   'ingrid@example.se',     'Stockholm', 'SE', '2023-08-08 07:40:00'),
    (8,  'Jamal Wright',  'jamal@example.com',     NULL,        'US', '2023-09-19 14:00:00'),
    (9,  'Kofi Mensah',   'kofi@example.gh',       'Accra',     'GH', '2023-10-27 11:25:00'),
    (10, 'Lena Müller',   'lena@example.de',       'Berlin',    'DE', '2023-12-01 18:00:00'),
    (11, 'Marco Rossi',   'marco@example.it',      'Rome',      'IT', '2024-01-12 10:10:00'),
    (12, 'Nina Petrova',  'nina@example.com',      NULL,        'US', '2024-02-29 13:37:00');

INSERT INTO customer_profiles (customer_id, birth_date, phone, loyalty_tier, newsletter) VALUES
    (1,  '1990-04-12', '+351 912 000 001', 'gold',   1),
    (2,  '1985-11-30', NULL,               'silver', 0),
    (3,  '1992-07-08', '+34 600 000 003',  'bronze', 1),
    (4,  NULL,         '+1 617 555 0104',  'gold',   1),
    (6,  '1978-02-25', NULL,               'silver', 0),
    (7,  '2000-12-01', '+46 70 000 0007',  'bronze', 0),
    (9,  '1995-05-17', NULL,               'bronze', 1),
    (10, '1988-09-09', '+49 151 0000010',  'gold',   0),
    (12, '1999-01-01', NULL,               'bronze', 1);

INSERT INTO categories (category_id, name, parent_id) VALUES
    (1,  'Electronics', NULL),
    (2,  'Computers',   1),
    (3,  'Laptops',     2),
    (4,  'Accessories', 2),
    (5,  'Audio',       1),
    (6,  'Headphones',  5),
    (7,  'Home',        NULL),
    (8,  'Kitchen',     7),
    (9,  'Books',       NULL),
    (10, 'Programming', 9);

-- The Smart Kettle (14) is never ordered. The Gift Card has no category.
INSERT INTO products (product_id, sku, name, category_id, price, stock, attributes, discontinued) VALUES
    (1,  'LAP-001', 'UltraBook 14',                          3,    1299.00, 12,  '{"cpu":"M3","ram_gb":16,"colors":["silver","gray"]}', 0),
    (2,  'LAP-002', 'WorkStation 16',                        3,    2199.00, 4,   '{"cpu":"i9","ram_gb":64,"colors":["black"]}', 0),
    (3,  'ACC-001', 'USB-C Hub',                             4,    49.90,   120, '{"ports":7,"colors":["gray"]}', 0),
    (4,  'ACC-002', 'Mechanical Keyboard',                   4,    129.00,  35,  '{"switch":"brown","layout":"ISO","colors":["black","white"]}', 0),
    (5,  'ACC-003', 'Wireless Mouse',                        4,    39.99,   80,  '{"dpi":4000,"colors":["black"]}', 0),
    (6,  'AUD-001', 'Noise-Cancelling Headphones',           6,    349.00,  20,  '{"anc":true,"battery_h":30,"colors":["black","sand"]}', 0),
    (7,  'AUD-002', 'Studio Monitors',                       5,    499.00,  6,   '{"watts":50}', 0),
    (8,  'AUD-003', 'Wired Earbuds',                         6,    19.90,   0,   NULL, 1),
    (9,  'KIT-001', 'Espresso Machine',                      8,    399.00,  9,   '{"bar":15,"colors":["steel"]}', 0),
    (10, 'KIT-002', 'Chef Knife',                            8,    89.50,   40,  '{"steel":"VG-10","length_cm":21}', 0),
    (11, 'BOO-001', 'SQL Antipatterns',                      10,   34.95,   50,  '{"pages":352,"format":"paperback"}', 0),
    (12, 'BOO-002', 'Designing Data-Intensive Applications', 10,   45.00,   25,  '{"pages":616,"format":"paperback"}', 0),
    (13, 'BOO-003', 'The Art of SQL',                        10,   39.00,   15,  '{"pages":372,"format":"hardcover"}', 0),
    (14, 'KIT-003', 'Smart Kettle',                          8,    79.00,   18,  '{"liters":1.7,"colors":["white"]}', 0),
    (15, 'GFT-001', 'Gift Card',                             NULL, 25.00,   999, '{"digital":true}', 0);

-- The 'eco' tag is never used.
INSERT INTO tags (tag_id, name) VALUES
    (1, 'bestseller'), (2, 'new'), (3, 'sale'), (4, 'gift'),
    (5, 'wireless'), (6, 'eco'), (7, 'premium'), (8, 'clearance');

INSERT INTO product_tags (product_id, tag_id) VALUES
    (1, 1), (1, 7), (2, 7), (3, 1), (4, 2), (5, 3), (5, 5), (6, 1), (6, 5), (6, 7),
    (8, 8), (9, 7), (10, 4), (11, 1), (12, 1), (12, 4), (13, 3), (14, 2), (15, 4);

INSERT INTO orders (order_id, customer_id, order_date, status, shipped_at, notes) VALUES
    (1,  1,  '2024-01-05', 'shipped',   '2024-01-07', NULL),
    (2,  2,  '2024-01-18', 'shipped',   '2024-01-20', 'Gift wrap please'),
    (3,  1,  '2024-02-02', 'shipped',   '2024-02-05', NULL),
    (4,  3,  '2024-02-14', 'paid',      NULL,         NULL),
    (5,  4,  '2024-03-01', 'shipped',   '2024-03-04', NULL),
    (6,  6,  '2024-03-22', 'cancelled', NULL,         'Customer changed mind'),
    (7,  7,  '2024-04-10', 'shipped',   '2024-04-12', NULL),
    (8,  2,  '2024-05-05', 'shipped',   '2024-05-06', NULL),
    (9,  9,  '2024-06-18', 'paid',      NULL,         NULL),
    (10, 10, '2024-07-01', 'shipped',   '2024-07-03', NULL),
    (11, 11, '2024-08-12', 'open',      NULL,         NULL),
    (12, 1,  '2024-09-09', 'shipped',   '2024-09-11', NULL),
    (13, 5,  '2024-10-15', 'paid',      NULL,         NULL),
    (14, 3,  '2024-11-29', 'shipped',   '2024-12-02', 'Black Friday'),
    (15, 8,  '2024-12-20', 'open',      NULL,         NULL),
    (16, 6,  '2025-01-08', 'open',      NULL,         NULL),
    (17, 4,  '2025-02-14', 'paid',      NULL,         NULL),
    (18, 10, '2025-03-03', 'open',      NULL,         'Call before delivery');

INSERT INTO order_items (order_id, product_id, quantity, unit_price, discount) VALUES
    (1, 1, 1, 1299.00, 0),    (1, 3, 1, 49.90, 0),
    (2, 6, 1, 349.00, 0.10),  (2, 11, 2, 34.95, 0),
    (3, 4, 1, 129.00, 0),     (3, 5, 1, 39.99, 0),
    (4, 9, 1, 399.00, 0),     (4, 10, 2, 89.50, 0),
    (5, 2, 1, 2199.00, 0.05), (5, 3, 2, 49.90, 0),
    (6, 7, 2, 499.00, 0),
    (7, 12, 1, 45.00, 0),     (7, 11, 1, 34.95, 0),     (7, 13, 1, 39.00, 0),
    (8, 6, 1, 349.00, 0),
    (9, 1, 1, 1299.00, 0.10), (9, 4, 1, 129.00, 0),
    (10, 5, 3, 39.99, 0),
    (11, 15, 2, 25.00, 0),
    (12, 12, 1, 45.00, 0),    (12, 3, 1, 49.90, 0.20),
    (13, 9, 1, 399.00, 0),
    (14, 6, 2, 299.00, 0),    (14, 8, 4, 19.90, 0),
    (15, 10, 1, 89.50, 0),
    (16, 4, 2, 129.00, 0.15),
    (17, 2, 1, 2199.00, 0),
    (18, 11, 3, 34.95, 0),    (18, 1, 1, 1299.00, 0);

-- Order 14 shipped only 3 of its 4 earbuds. Some shipments have no tracking number.
INSERT INTO shipment_items (shipment_item_id, order_id, product_id, quantity, carrier, shipped_on, tracking) VALUES
    (1,  1,  1,  1, 'DHL',      '2024-01-07', 'DHL-0001'),
    (2,  1,  3,  1, 'DHL',      '2024-01-07', 'DHL-0002'),
    (3,  2,  6,  1, 'CTT',      '2024-01-20', 'CTT-0003'),
    (4,  2,  11, 2, 'CTT',      '2024-01-20', NULL),
    (5,  3,  4,  1, 'UPS',      '2024-02-05', 'UPS-0005'),
    (6,  3,  5,  1, 'UPS',      '2024-02-05', 'UPS-0006'),
    (7,  5,  2,  1, 'FedEx',    '2024-03-04', 'FDX-0007'),
    (8,  5,  3,  2, 'FedEx',    '2024-03-04', 'FDX-0008'),
    (9,  7,  12, 1, 'PostNord', '2024-04-12', 'PN-0009'),
    (10, 7,  11, 1, 'PostNord', '2024-04-12', 'PN-0010'),
    (11, 7,  13, 1, 'PostNord', '2024-04-12', NULL),
    (12, 8,  6,  1, 'CTT',      '2024-05-06', 'CTT-0012'),
    (13, 10, 5,  3, 'DHL',      '2024-07-03', 'DHL-0013'),
    (14, 12, 12, 1, 'CTT',      '2024-09-11', 'CTT-0014'),
    (15, 12, 3,  1, 'CTT',      '2024-09-11', 'CTT-0015'),
    (16, 14, 6,  2, 'SEUR',     '2024-12-02', 'SEUR-0016'),
    (17, 14, 8,  3, 'SEUR',     '2024-12-02', 'SEUR-0017');

-- Reykjavik has no routes.
INSERT INTO cities (city_id, name, country, lat, lon) VALUES
    (1, 'Lisbon',    'PT', 38.72, -9.14),
    (2, 'Porto',     'PT', 41.15, -8.61),
    (3, 'Madrid',    'ES', 40.42, -3.70),
    (4, 'Barcelona', 'ES', 41.39,  2.17),
    (5, 'Paris',     'FR', 48.86,  2.35),
    (6, 'Lyon',      'FR', 45.76,  4.84),
    (7, 'Berlin',    'DE', 52.52, 13.40),
    (8, 'Munich',    'DE', 48.14, 11.58),
    (9, 'Reykjavik', 'IS', 64.15, -21.94);

-- Directed routes. Lisbon <-> Porto and Berlin <-> Munich go both ways; Barcelona -> Paris -> Lyon -> Barcelona is a cycle.
INSERT INTO routes (from_city, to_city, km) VALUES
    (1, 2, 313),  (2, 1, 313),  (1, 3, 625),  (2, 3, 560),
    (3, 4, 621),  (3, 5, 1270), (4, 5, 1035), (5, 6, 465),
    (6, 4, 644),  (5, 7, 1054), (6, 8, 735),  (7, 8, 585),
    (8, 7, 585);

INSERT INTO events (event_id, occurred_at, kind, customer_id, payload) VALUES
    (1,  1732785300, 'login',       3,    '{"device":"mobile","ip":"10.0.0.3"}'),
    (2,  1732785450, 'search',      3,    '{"query":"headphones","results":2}'),
    (3,  1732785665, 'add_to_cart', 3,    '{"product_id":6,"quantity":2}'),
    (4,  1732888920, 'checkout',    3,    '{"order_id":14,"total":677.6,"items":[6,8]}'),
    (5,  1732889445, 'logout',      3,    '{}'),
    (6,  1733040000, 'page_view',   NULL, '{"path":"/","referrer":"newsletter"}'),
    (7,  1734723912, 'login',       8,    '{"device":"desktop","ip":"10.0.0.8"}'),
    (8,  1734724360, 'checkout',    8,    '{"order_id":15,"total":89.5,"items":[10]}'),
    (9,  1736335800, 'checkout',    6,    '{"order_id":16,"total":219.3,"items":[4]}'),
    (10, 1739516700, 'page_view',   4,    '{"path":"/products/2","referrer":null}'),
    (11, 1741018800, 'search',      10,   '{"query":"laptop","results":2,"filters":{"max_price":1500}}'),
    (12, 1741018870, 'checkout',    10,   '{"order_id":18,"total":1403.85,"items":[11,1]}');

COMMIT;
