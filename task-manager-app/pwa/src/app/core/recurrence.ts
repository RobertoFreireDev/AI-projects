/* ============================================================
   Recurrence engine — CLAUDE.md §4 / §4.0 / §4.1.

   A direct port of Solution A's engine. Both targets must agree on every
   due date (§15), so this file is covered by recurrence.spec.ts, including
   the §12 edge cases.
   ============================================================ */

import {
  MONTHS_SHORT,
  ORDINALS,
  WEEKDAYS,
  WEEKDAYS_SHORT,
  addDays,
  dayOfWeek,
  daysBetween,
  daysInMonth,
  fmtDate,
  isValidDate,
  nthWeekday,
  parts,
  weekStart,
  ymd,
} from './date';
import type { Recurrence, Task } from './model';

/** Sorted, de-duplicated weekday list for a `weekly` recurrence, or null when
    the task doesn't use §4.0 — null means "the single weekday implied by
    startDate", i.e. the original pre-daysOfWeek behavior. */
export function weekDaysList(r: Recurrence | null | undefined): number[] | null {
  if (!r || r.type !== 'weekly' || !Array.isArray(r.daysOfWeek)) return null;
  const seen = new Set<number>();
  const out: number[] = [];
  for (const raw of r.daysOfWeek) {
    const d = Math.floor(Number(raw));
    if (!isFinite(d) || d < 0 || d > 6 || seen.has(d)) continue;
    seen.add(d);
    out.push(d);
  }
  out.sort((a, b) => a - b);
  return out.length ? out : null;
}

/** The date a `monthly` recurrence lands on inside one specific month. */
function occurrenceInMonth(y: number, m: number, r: Recurrence): string {
  if (r.mode === 'weekdayOfMonth') {
    return nthWeekday(
      y,
      m,
      Number(r.weekday) || 0,
      r.weekdayOrdinal == null ? '1' : String(r.weekdayOrdinal),
    );
  }
  return ymd(y, m, Math.min(Number(r.dayOfMonth) || 1, daysInMonth(y, m)));
}

/** The most recent scheduled occurrence <= `ref`, or null (CLAUDE.md §4.1). */
export function lastDueDate(task: Task | null | undefined, ref: string): string | null {
  const r = task?.recurrence;
  if (!r) return null;

  const start = isValidDate(r.startDate)
    ? r.startDate
    : isValidDate(task.createdAt)
      ? task.createdAt
      : null;
  const interval = Math.max(1, Math.floor(Number(r.interval) || 1));

  if (r.type === 'once') {
    return isValidDate(r.date) && r.date <= ref ? r.date : null;
  }
  if (!start || ref < start) return null;

  /* weekly + daysOfWeek (CLAUDE.md §4.0): several trigger days inside every
     active week, still resolving to a single most-recent due date. */
  const dows = weekDaysList(r);
  if (dows) {
    const anchor = weekStart(start);
    let wi = daysBetween(anchor, weekStart(ref)) / 7; // whole weeks, >= 0
    wi -= wi % interval; // back to an active week
    while (wi >= 0) {
      const ws = addDays(anchor, wi * 7);
      for (let di = dows.length - 1; di >= 0; di--) {
        // latest day first
        const cand = addDays(ws, dows[di]);
        if (cand <= ref && cand >= start) return cand; // never before startDate
      }
      wi -= interval;
    }
    return null;
  }

  if (r.type === 'daily' || r.type === 'weekly') {
    const step = r.type === 'weekly' ? 7 * interval : interval;
    const elapsed = daysBetween(start, ref);
    return addDays(start, Math.floor(elapsed / step) * step);
  }

  if (r.type === 'monthly') {
    const s = parts(start);
    const t = parts(ref);
    let k = Math.floor(((t.y - s.y) * 12 + (t.m - s.m)) / interval);
    while (k >= 0) {
      const idx = s.y * 12 + (s.m - 1) + k * interval;
      const cand = occurrenceInMonth(Math.floor(idx / 12), (idx % 12) + 1, r);
      if (cand < start) return null; // walking back can only get earlier
      if (cand <= ref) return cand;
      k--;
    }
    return null;
  }

  if (r.type === 'yearly') {
    const sy = parts(start).y;
    const ty = parts(ref).y;
    const mo = Math.min(12, Math.max(1, Number(r.month) || 1));
    let k = Math.floor((ty - sy) / interval);
    while (k >= 0) {
      const year = sy + k * interval;
      const cand = ymd(year, mo, Math.min(Number(r.day) || 1, daysInMonth(year, mo)));
      if (cand < start) return null;
      if (cand <= ref) return cand;
      k--;
    }
    return null;
  }

  return null;
}

/** The occurrence that follows `due`, or null when there is none
    (a `once` task has no future occurrence — see CLAUDE.md §7.2). */
export function nextDueDate(task: Task, due: string | null): string | null {
  const r = task.recurrence;
  if (!r || !due || r.type === 'once') return null;
  const interval = Math.max(1, Math.floor(Number(r.interval) || 1));

  if (r.type === 'daily') return addDays(due, interval);

  if (r.type === 'weekly') {
    const dows = weekDaysList(r);
    if (!dows) return addDays(due, 7 * interval);
    /* `due` always sits in an active week, so the next occurrence is either a
       later trigger day in that same week or the first one `interval` weeks on. */
    const ws = weekStart(due);
    const dow = dayOfWeek(due);
    for (const d of dows) if (d > dow) return addDays(ws, d);
    return addDays(ws, interval * 7 + dows[0]);
  }

  if (r.type === 'monthly') {
    const p = parts(due);
    const idx = p.y * 12 + (p.m - 1) + interval;
    return occurrenceInMonth(Math.floor(idx / 12), (idx % 12) + 1, r);
  }

  if (r.type === 'yearly') {
    const year = parts(due).y + interval;
    const mo = Math.min(12, Math.max(1, Number(r.month) || 1));
    return ymd(year, mo, Math.min(Number(r.day) || 1, daysInMonth(year, mo)));
  }

  return null;
}

/** Human-readable summary of a recurrence, for the Tasks list. */
export function recurrenceLabel(r: Recurrence | null | undefined): string {
  if (!r) return '';
  const n = Math.max(1, Math.floor(Number(r.interval) || 1));

  switch (r.type) {
    case 'once':
      return 'Once on ' + fmtDate(r.date ?? '');
    case 'daily':
      return n === 1 ? 'Every day' : 'Every ' + n + ' days';
    case 'weekly': {
      const dows = weekDaysList(r);
      const base = n === 1 ? 'Every week' : 'Every ' + n + ' weeks';
      return dows ? base + ' on ' + dows.map((i) => WEEKDAYS_SHORT[i]).join(', ') : base;
    }
    case 'monthly': {
      const when =
        r.mode === 'weekdayOfMonth'
          ? 'on the ' +
            (ORDINALS[String(r.weekdayOrdinal)] ?? '1st') +
            ' ' +
            WEEKDAYS[Number(r.weekday) || 0]
          : 'on day ' + (Number(r.dayOfMonth) || 1);
      return (n === 1 ? 'Every month ' : 'Every ' + n + ' months ') + when;
    }
    case 'yearly':
      return (
        (n === 1 ? 'Every year ' : 'Every ' + n + ' years ') +
        'on ' +
        MONTHS_SHORT[(Number(r.month) || 1) - 1] +
        ' ' +
        (Number(r.day) || 1)
      );
  }
  return '';
}
