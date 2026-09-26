-- Full-text search with FTS5 (optional)
-- FTS5 is compiled into this build if the next query returns a row.
SELECT compile_options FROM pragma_compile_options WHERE compile_options = 'ENABLE_FTS5';

-- An FTS5 virtual table indexes words for fast text search.
CREATE VIRTUAL TABLE product_search USING fts5(name, description, tokenize = 'porter unicode61');

INSERT INTO product_search (rowid, name, description)
SELECT product_id, name,
       CASE product_id
           WHEN 1 THEN 'Thin and light laptop with all-day battery and a bright display'
           WHEN 2 THEN 'Powerful laptop workstation for compiling, rendering and data work'
           WHEN 3 THEN 'Seven-port hub: charge your laptop and connect displays'
           WHEN 4 THEN 'Tactile keyboard with brown switches for long typing sessions'
           WHEN 6 THEN 'Headphones with active noise cancelling and long battery life'
           WHEN 11 THEN 'Common database design mistakes and how to avoid them'
           WHEN 12 THEN 'Deep dive into databases, replication and distributed data systems'
           ELSE name
       END
FROM products;

-- MATCH finds rows containing the words. The porter stemmer makes "batteries" match "battery".
SELECT rowid, name FROM product_search WHERE product_search MATCH 'batteries';

-- Phrases, AND/OR/NOT, prefixes (data*) and column filters (name:...).
SELECT rowid, name FROM product_search WHERE product_search MATCH '"noise cancelling"';
SELECT rowid, name FROM product_search WHERE product_search MATCH 'laptop NOT workstation';
SELECT rowid, name FROM product_search WHERE product_search MATCH 'data*';
SELECT rowid, name FROM product_search WHERE product_search MATCH 'name:keyboard OR name:hub';

-- bm25() ranks results (lower is more relevant); highlight() marks the matching words.
SELECT rowid, name, round(bm25(product_search), 3) AS score,
       highlight(product_search, 1, '[', ']') AS description
FROM product_search
WHERE product_search MATCH 'laptop'
ORDER BY bm25(product_search);

-- snippet() shows a short excerpt around the match.
SELECT name, snippet(product_search, 1, '<', '>', '…', 5) AS excerpt
FROM product_search WHERE product_search MATCH 'database OR databases';

-- Join search results back to the real table.
SELECT p.sku, p.price, s.name
FROM product_search s JOIN products p ON p.product_id = s.rowid
WHERE product_search MATCH 'laptop'
ORDER BY p.price;

DROP TABLE product_search;
