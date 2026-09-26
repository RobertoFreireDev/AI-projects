-- Core functions: strings, types, casts, rounding
-- substr is 1-based; a negative start counts from the end.
SELECT name, substr(name, 1, 5) AS first5, substr(name, -4) AS last4 FROM products LIMIT 4;

-- instr finds a substring (0 if absent); replace, trim, upper, lower.
SELECT email,
       instr(email, '@') AS at_position,
       substr(email, instr(email, '@') + 1) AS domain,
       replace(email, 'example', 'sample') AS replaced,
       upper(email) AS upper_email
FROM customers LIMIT 5;

SELECT '[' || trim('   padded   ') || ']' AS trimmed,
       '[' || ltrim('xxhixx', 'x') || ']' AS ltrimmed,
       '[' || rtrim('xxhixx', 'x') || ']' AS rtrimmed;

-- length counts characters; octet_length counts bytes (UTF-8 here). 'Müller' has a 2-byte ü.
SELECT name, length(name) AS chars, octet_length(name) AS bytes FROM customers WHERE customer_id = 10;

-- printf / format: C-style formatting.
SELECT printf('%-25s|%8.2f|%05d', name, price, stock) AS formatted FROM products LIMIT 5;
SELECT format('%s has %d items', 'Order 7', 3) AS message, format('%,d', 1234567) AS thousands;

-- concat ignores NULLs; || turns the whole result into NULL if any part is NULL.
SELECT name, city,
       concat(name, ' (', city, ')') AS with_concat,
       name || ' (' || city || ')' AS with_pipes,
       concat_ws(', ', name, city, country) AS with_separator
FROM customers WHERE customer_id IN (1, 8);

-- hex, unicode and char convert between text, bytes and code points.
SELECT hex('SQL') AS hex_text, unicode('ü') AS code_point, char(83, 81, 76, 33) AS from_codes;

-- typeof shows the storage class of each value. A column's values can differ in type.
SELECT typeof(1), typeof(1.5), typeof('1'), typeof(x'01'), typeof(NULL), typeof(1 = 1);

-- CAST converts between storage classes. Text that does not look like a number becomes 0.
SELECT CAST('42' AS INTEGER) AS int_from_text,
       CAST('3.7abc' AS REAL) AS real_prefix,
       CAST('hello' AS INTEGER) AS not_a_number,
       CAST(12.9 AS INTEGER) AS truncated,
       CAST(100 AS TEXT) || '%' AS as_text;

-- Integer division truncates. Multiply by 1.0 (or CAST) to get a REAL result.
SELECT 7 / 2 AS integer_division, 7 / 2.0 AS real_division, 7 % 2 AS remainder;

-- nullif(a, b) returns NULL when a = b: the classic guard against division by zero.
SELECT stock, 100 / nullif(stock, 0) AS per_unit FROM products WHERE product_id IN (1, 8);

-- round, abs, and scalar min/max (with two or more arguments they compare values, not rows).
SELECT round(2.5) AS r1, round(-2.5) AS r2, round(3.14159, 2) AS r3,
       abs(-7) AS absolute, max(3, 9, 4) AS largest, min('b', 'a', 'c') AS smallest;

-- likely / unlikely are planner hints and return their argument unchanged.
SELECT name FROM products WHERE likely(discontinued = 0) AND price > 300;

-- quote returns a value as an SQL literal: handy for generating SQL.
SELECT quote(name) AS literal, quote(city) AS maybe_null, quote(x'ABCD') AS blob_literal
FROM customers WHERE customer_id IN (1, 8);
