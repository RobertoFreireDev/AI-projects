-- SELECT basics: filtering, sorting, paging
-- WHERE filters rows; comparison operators work as usual.
SELECT product_id, name, price
FROM products
WHERE price > 100;

-- DISTINCT removes duplicate rows from the result.
SELECT DISTINCT country FROM customers;

-- ORDER BY with several keys; DESC reverses one key.
SELECT name, category_id, price
FROM products
ORDER BY category_id, price DESC;

-- NULLs sort first in ascending order by default. NULLS LAST / NULLS FIRST change that.
SELECT name, city FROM customers ORDER BY city NULLS LAST;
SELECT name, city FROM customers ORDER BY city DESC NULLS FIRST;

-- LIMIT and OFFSET page through a result (page 2, 5 rows per page).
SELECT product_id, name FROM products ORDER BY product_id LIMIT 5 OFFSET 5;

-- LIKE is case-insensitive for ASCII; % matches any run, _ one character.
SELECT name FROM customers WHERE name LIKE 'grace%';

-- GLOB is case-sensitive and uses Unix wildcards (* ? [...]).
SELECT sku, name FROM products WHERE sku GLOB 'A[CU]?-*';

-- BETWEEN is inclusive on both ends. Dates stored as ISO-8601 text compare correctly as strings.
SELECT order_id, order_date FROM orders WHERE order_date BETWEEN '2024-03-01' AND '2024-06-30';

-- IN tests membership in a list.
SELECT order_id, status FROM orders WHERE status IN ('open', 'paid');

-- IS NULL / IS NOT NULL: "= NULL" is never true, because NULL compared to anything is NULL.
SELECT name, city FROM customers WHERE city IS NULL;
SELECT count(*) AS equals_null_matches FROM customers WHERE city = NULL;

-- CASE builds a value from conditions.
SELECT name, price,
       CASE
           WHEN price < 50 THEN 'budget'
           WHEN price < 500 THEN 'mid-range'
           ELSE 'premium'
       END AS price_band
FROM products;

-- COALESCE returns its first non-NULL argument.
SELECT name, COALESCE(city, '(unknown city)') AS city FROM customers;

-- IIF(condition, then, else) is shorthand for a two-way CASE.
SELECT name, stock, IIF(stock = 0, 'out of stock', 'available') AS availability FROM products;
