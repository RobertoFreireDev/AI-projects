-- Dates and times
-- SQLite has no date type. Dates are stored as ISO-8601 TEXT, Julian day REAL, or Unix epoch INTEGER,
-- and the date functions understand all three.
SELECT date('2024-02-28') AS d, time('2024-02-28 13:45:10') AS t, datetime('2024-02-28 13:45') AS dt,
       julianday('2024-02-28') AS jd, unixepoch('2024-02-28') AS epoch;

-- The current moment ('now' is UTC). These values change each time you run.
SELECT date('now') AS today, datetime('now') AS utc_now, datetime('now', 'localtime') AS local_now;

-- Modifiers are applied left to right.
SELECT date('2024-01-31', '+1 month') AS plus_month,
       date('2024-03-15', 'start of month') AS month_start,
       date('2024-03-15', 'start of month', '+1 month', '-1 day') AS month_end,
       date('2024-03-15', 'start of year') AS year_start,
       date('2024-03-15', 'weekday 1') AS next_monday,
       date('2024-03-15', '-7 days', 'weekday 0') AS sunday_on_or_after_week_ago;

-- Unix epoch integers: events.occurred_at is one. 'unixepoch' reads them; 'auto' guesses the format.
SELECT event_id, occurred_at,
       datetime(occurred_at, 'unixepoch') AS as_text,
       datetime(occurred_at, 'auto') AS auto_detected
FROM events LIMIT 4;

-- strftime formats however you like.
SELECT order_id, order_date,
       strftime('%d/%m/%Y', order_date) AS european,
       strftime('%Y-W%W', order_date) AS year_week,
       strftime('%j', order_date) AS day_of_year,
       strftime('%w', order_date) AS weekday_0_sun
FROM orders LIMIT 5;

-- Interval math: days between two dates via julianday, and timediff (3.43+).
SELECT order_id, order_date, shipped_at,
       julianday(shipped_at) - julianday(order_date) AS days_to_ship,
       timediff(shipped_at, order_date) AS timediff
FROM orders WHERE shipped_at IS NOT NULL;

-- Seconds between two events with unixepoch arithmetic.
SELECT a.event_id AS from_event, b.event_id AS to_event, b.occurred_at - a.occurred_at AS seconds
FROM events a JOIN events b ON b.event_id = a.event_id + 1
WHERE a.customer_id = b.customer_id;

-- Age in whole years (as of a fixed date, so the result does not change).
SELECT c.name, p.birth_date,
       CAST(strftime('%Y', '2025-06-01') AS INTEGER) - CAST(strftime('%Y', p.birth_date) AS INTEGER)
         - (strftime('%m-%d', '2025-06-01') < strftime('%m-%d', p.birth_date)) AS age
FROM customer_profiles p JOIN customers c USING (customer_id)
WHERE p.birth_date IS NOT NULL;

-- Tenure in years and days, using julianday.
SELECT first_name, hire_date,
       round((julianday('2025-06-01') - julianday(hire_date)) / 365.25, 1) AS years
FROM employees ORDER BY hire_date LIMIT 5;

-- Grouping by month.
SELECT strftime('%Y-%m', order_date) AS month, count(*) AS orders, round(sum(total), 2) AS revenue
FROM v_order_totals
GROUP BY month
ORDER BY month;

-- Invalid dates give NULL rather than an error.
SELECT date('2024-02-30') AS invalid, date('not a date') AS garbage, date('2024-02-29') AS leap_day;
