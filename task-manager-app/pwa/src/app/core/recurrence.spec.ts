/* The recurrence engine is the one piece Solution A and Solution B must
   agree on exactly (CLAUDE.md §15). These cover §4.1's rules and every
   edge case listed in §12. */

import type { Recurrence, Task } from './model';
import { lastDueDate, nextDueDate, recurrenceLabel } from './recurrence';

function task(recurrence: Recurrence, createdAt = '2026-01-01'): Task {
  return {
    id: 't',
    icon: 'task',
    name: 'test',
    notes: [],
    recurrence,
    createdAt,
    completions: [],
    checklistState: { lastResetDueDate: null, checkedItemIds: [] },
    active: true,
  };
}

describe('lastDueDate — once', () => {
  it('is due from its date onward, and never before', () => {
    const t = task({ type: 'once', date: '2026-08-04' });
    expect(lastDueDate(t, '2026-08-03')).toBeNull();
    expect(lastDueDate(t, '2026-08-04')).toBe('2026-08-04');
    expect(lastDueDate(t, '2027-01-01')).toBe('2026-08-04');
  });
});

describe('lastDueDate — daily', () => {
  const t = task({ type: 'daily', interval: 3, startDate: '2026-08-01' });

  it('counts intervals from the start date', () => {
    expect(lastDueDate(t, '2026-07-31')).toBeNull();
    expect(lastDueDate(t, '2026-08-01')).toBe('2026-08-01');
    expect(lastDueDate(t, '2026-08-03')).toBe('2026-08-01');
    expect(lastDueDate(t, '2026-08-04')).toBe('2026-08-04');
  });

  it('walks forward one interval at a time', () => {
    expect(nextDueDate(t, '2026-08-04')).toBe('2026-08-07');
  });
});

describe('lastDueDate — weekly without daysOfWeek', () => {
  const t = task({ type: 'weekly', interval: 2, startDate: '2026-08-04' }); // a Tuesday

  it('repeats on the start date weekday only', () => {
    expect(lastDueDate(t, '2026-08-04')).toBe('2026-08-04');
    expect(lastDueDate(t, '2026-08-11')).toBe('2026-08-04'); // skipped week
    expect(lastDueDate(t, '2026-08-18')).toBe('2026-08-18');
    expect(nextDueDate(t, '2026-08-18')).toBe('2026-09-01');
  });
});

describe('lastDueDate — weekly with daysOfWeek (CLAUDE.md §4.0)', () => {
  // "tirar o lixo" — Tue/Thu/Sat, every week.
  const trash = task({
    type: 'weekly',
    interval: 1,
    startDate: '2026-08-04',
    daysOfWeek: [2, 4, 6],
  });

  it('is due on each selected weekday', () => {
    expect(lastDueDate(trash, '2026-08-04')).toBe('2026-08-04'); // Tue
    expect(lastDueDate(trash, '2026-08-05')).toBe('2026-08-04'); // Wed -> last Tue
    expect(lastDueDate(trash, '2026-08-06')).toBe('2026-08-06'); // Thu
    expect(lastDueDate(trash, '2026-08-08')).toBe('2026-08-08'); // Sat
    expect(lastDueDate(trash, '2026-08-11')).toBe('2026-08-11'); // next Tue
  });

  it('never resolves to a date before the start date', () => {
    // Sunday and Monday of the start week come before startDate itself.
    expect(lastDueDate(trash, '2026-08-03')).toBeNull();
  });

  it('honours the interval by whole calendar weeks', () => {
    const biweekly = task({
      type: 'weekly',
      interval: 2,
      startDate: '2026-08-03', // Monday, week of Aug 2
      daysOfWeek: [1, 3], // Mon + Wed
    });
    expect(lastDueDate(biweekly, '2026-08-05')).toBe('2026-08-05'); // active week
    expect(lastDueDate(biweekly, '2026-08-12')).toBe('2026-08-05'); // skipped week
    expect(lastDueDate(biweekly, '2026-08-17')).toBe('2026-08-17'); // active again
  });

  it('advances to the next trigger day, then to the next active week', () => {
    expect(nextDueDate(trash, '2026-08-04')).toBe('2026-08-06');
    expect(nextDueDate(trash, '2026-08-08')).toBe('2026-08-11');
  });
});

describe('lastDueDate — monthly by day of month', () => {
  it('clamps a 31st to the last day of shorter months (§12)', () => {
    const t = task({
      type: 'monthly',
      interval: 1,
      startDate: '2026-01-31',
      mode: 'dayOfMonth',
      dayOfMonth: 31,
    });
    expect(lastDueDate(t, '2026-02-28')).toBe('2026-02-28');
    expect(lastDueDate(t, '2026-04-30')).toBe('2026-04-30');
    expect(lastDueDate(t, '2026-05-30')).toBe('2026-04-30');
    expect(lastDueDate(t, '2026-05-31')).toBe('2026-05-31');
    // ...and February 2024 gets its 29th.
    const leap = task({
      type: 'monthly',
      interval: 1,
      startDate: '2024-01-31',
      mode: 'dayOfMonth',
      dayOfMonth: 31,
    });
    expect(lastDueDate(leap, '2024-02-29')).toBe('2024-02-29');
  });

  it('respects an interval greater than one month', () => {
    const t = task({
      type: 'monthly',
      interval: 3,
      startDate: '2026-01-01',
      mode: 'dayOfMonth',
      dayOfMonth: 1,
    });
    expect(lastDueDate(t, '2026-03-31')).toBe('2026-01-01');
    expect(lastDueDate(t, '2026-04-01')).toBe('2026-04-01');
    expect(nextDueDate(t, '2026-04-01')).toBe('2026-07-01');
  });
});

describe('lastDueDate — monthly by weekday of month', () => {
  const t = task({
    type: 'monthly',
    interval: 1,
    startDate: '2026-01-01',
    mode: 'weekdayOfMonth',
    weekdayOrdinal: '2',
    weekday: 2, // 2nd Tuesday
  });

  it('lands on the Nth weekday', () => {
    expect(lastDueDate(t, '2026-08-31')).toBe('2026-08-11');
    expect(nextDueDate(t, '2026-08-11')).toBe('2026-09-08');
  });

  it('handles "last" in months with 4 and with 5 of that weekday (§12)', () => {
    const last = task({
      type: 'monthly',
      interval: 1,
      startDate: '2026-01-01',
      mode: 'weekdayOfMonth',
      weekdayOrdinal: 'last',
      weekday: 6, // Saturday
    });
    // August 2026 has 5 Saturdays, September has 4.
    expect(lastDueDate(last, '2026-08-31')).toBe('2026-08-29');
    expect(lastDueDate(last, '2026-09-30')).toBe('2026-09-26');
  });
});

describe('lastDueDate — yearly', () => {
  it('clamps Feb 29 to Feb 28 in non-leap years (§12)', () => {
    const t = task({ type: 'yearly', interval: 1, startDate: '2024-02-29', month: 2, day: 29 });
    expect(lastDueDate(t, '2024-03-01')).toBe('2024-02-29');
    expect(lastDueDate(t, '2025-03-01')).toBe('2025-02-28');
    expect(nextDueDate(t, '2025-02-28')).toBe('2026-02-28');
    expect(nextDueDate(t, '2027-02-28')).toBe('2028-02-29');
  });

  it('is not due before its start year', () => {
    const t = task({ type: 'yearly', interval: 2, startDate: '2026-06-15', month: 6, day: 15 });
    expect(lastDueDate(t, '2026-06-14')).toBeNull();
    expect(lastDueDate(t, '2027-12-31')).toBe('2026-06-15');
    expect(lastDueDate(t, '2028-06-15')).toBe('2028-06-15');
  });
});

describe('lastDueDate — fallbacks', () => {
  it('falls back to createdAt when no startDate is set', () => {
    const t = task({ type: 'daily', interval: 1 }, '2026-08-01');
    expect(lastDueDate(t, '2026-08-04')).toBe('2026-08-04');
    expect(lastDueDate(t, '2026-07-31')).toBeNull();
  });

  it('has no next occurrence for a one-off task (CLAUDE.md §7.2)', () => {
    expect(nextDueDate(task({ type: 'once', date: '2026-08-04' }), '2026-08-04')).toBeNull();
  });
});

describe('recurrenceLabel', () => {
  it('describes each type in plain words', () => {
    expect(recurrenceLabel({ type: 'once', date: '2026-08-04' })).toBe('Once on Aug 4, 2026');
    expect(recurrenceLabel({ type: 'daily', interval: 1 })).toBe('Every day');
    expect(recurrenceLabel({ type: 'daily', interval: 2 })).toBe('Every 2 days');
    expect(recurrenceLabel({ type: 'weekly', interval: 1, daysOfWeek: [2, 4, 6] })).toBe(
      'Every week on Tue, Thu, Sat',
    );
    expect(
      recurrenceLabel({ type: 'monthly', interval: 3, mode: 'dayOfMonth', dayOfMonth: 1 }),
    ).toBe('Every 3 months on day 1');
    expect(
      recurrenceLabel({
        type: 'monthly',
        interval: 1,
        mode: 'weekdayOfMonth',
        weekdayOrdinal: 'last',
        weekday: 5,
      }),
    ).toBe('Every month on the last Friday');
    expect(recurrenceLabel({ type: 'yearly', interval: 1, month: 2, day: 29 })).toBe(
      'Every year on Feb 29',
    );
  });
});
