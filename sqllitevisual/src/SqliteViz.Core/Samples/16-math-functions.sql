-- Math functions
-- Available when SQLite is built with SQLITE_ENABLE_MATH_FUNCTIONS. SqliteViz registers the same
-- names itself if the library lacks them, so this sample works either way.
SELECT sqrt(2) AS sqrt2, pow(2, 10) AS pow, power(10, -2) AS power, exp(1) AS e;

-- Logarithms: ln is natural; log(x) is base 10; log(b, x) uses base b.
SELECT ln(exp(2)) AS ln, log(1000) AS log10_of_1000, log(2, 1024) AS log2_of_1024,
       log2(8) AS log2, log10(0.001) AS log10;

-- Trigonometry works in radians; degrees() and radians() convert.
SELECT pi() AS pi, degrees(pi()) AS half_turn, radians(90) AS right_angle,
       round(sin(radians(30)), 6) AS sin30, round(cos(0), 6) AS cos0, round(tan(radians(45)), 6) AS tan45,
       degrees(atan2(1, 1)) AS atan2_deg;

-- Rounding: ceil and floor go up/down, trunc goes toward zero, round goes to nearest.
SELECT x, ceil(x) AS ceil, floor(x) AS floor, trunc(x) AS trunc, round(x) AS round, sign(x) AS sign
FROM (SELECT 2.5 AS x UNION ALL SELECT -2.5 UNION ALL SELECT 0.0 UNION ALL SELECT -0.4);

-- mod works on REAL values (the % operator works on integers).
SELECT mod(10.5, 3) AS mod_real, 10 % 3 AS percent_int;

-- Out-of-domain inputs return NULL instead of an error.
SELECT sqrt(-1) AS sqrt_negative, ln(0) AS ln_zero, acos(2) AS acos_out_of_range;

-- A practical one: great-circle distance between cities (haversine), in km.
SELECT a.name AS from_city, b.name AS to_city,
       round(2 * 6371 * asin(sqrt(
           pow(sin(radians(b.lat - a.lat) / 2), 2) +
           cos(radians(a.lat)) * cos(radians(b.lat)) * pow(sin(radians(b.lon - a.lon) / 2), 2)
       ))) AS straight_km,
       r.km AS road_km
FROM routes r
JOIN cities a ON a.city_id = r.from_city
JOIN cities b ON b.city_id = r.to_city
ORDER BY road_km;

-- Standard deviation of product prices, from sqrt and aggregates.
SELECT round(avg(price), 2) AS mean,
       round(sqrt(avg(price * price) - avg(price) * avg(price)), 2) AS std_dev
FROM products;
