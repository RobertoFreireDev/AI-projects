import {
  addDays,
  dayOfWeek,
  daysInMonth,
  fmtDate,
  isValidDate,
  nthWeekday,
  weekStart,
} from './date';

describe('calendar dates', () => {
  it('round-trips across a leap day without drifting', () => {
    expect(addDays('2024-02-28', 1)).toBe('2024-02-29');
    expect(addDays('2024-02-29', 1)).toBe('2024-03-01');
    expect(addDays('2025-02-28', 1)).toBe('2025-03-01');
    expect(addDays('2026-01-01', -1)).toBe('2025-12-31');
  });

  it('knows month lengths, leap years included', () => {
    expect(daysInMonth(2024, 2)).toBe(29);
    expect(2100 % 4).toBe(0); // divisible by 4 but not a leap year
    expect(daysInMonth(2100, 2)).toBe(28);
    expect(daysInMonth(2000, 2)).toBe(29);
  });

  it('numbers weekdays from Sunday', () => {
    expect(dayOfWeek('2026-08-02')).toBe(0); // Sunday
    expect(dayOfWeek('2026-08-04')).toBe(2); // Tuesday
    expect(weekStart('2026-08-04')).toBe('2026-08-02');
    expect(weekStart('2026-08-02')).toBe('2026-08-02');
  });

  it('rejects impossible dates rather than normalising them', () => {
    expect(isValidDate('2026-02-30')).toBe(false);
    expect(isValidDate('2025-02-29')).toBe(false);
    expect(isValidDate('2024-02-29')).toBe(true);
    expect(isValidDate('2026-8-4')).toBe(false);
    expect(isValidDate(20260804)).toBe(false);
  });

  it('finds the Nth and the last weekday of a month', () => {
    // August 2026 starts on a Saturday, so it has 5 Saturdays.
    expect(nthWeekday(2026, 8, 6, '1')).toBe('2026-08-01');
    expect(nthWeekday(2026, 8, 6, '4')).toBe('2026-08-22');
    expect(nthWeekday(2026, 8, 6, 'last')).toBe('2026-08-29');
    // ...but only 4 Wednesdays.
    expect(nthWeekday(2026, 8, 3, '4')).toBe('2026-08-26');
    expect(nthWeekday(2026, 8, 3, 'last')).toBe('2026-08-26');
  });

  it('formats dates without touching the time zone', () => {
    expect(fmtDate('2026-08-04')).toBe('Aug 4, 2026');
  });
});
