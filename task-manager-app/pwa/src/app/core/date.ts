/* ============================================================
   Calendar dates — plain "YYYY-MM-DD" strings only.

   All arithmetic goes through a proleptic-Gregorian day count so there is
   never a Date object with a time-of-day involved (CLAUDE.md §4). The only
   place a real Date appears is `todayStr()`, which reads the local wall
   clock once and immediately drops the time.
   ============================================================ */

export const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];
export const MONTHS_SHORT = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];
export const WEEKDAYS = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
];
export const WEEKDAYS_SHORT = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
export const WEEKDAYS_MIN = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];
export const ORDINALS: Record<string, string> = {
  '1': '1st',
  '2': '2nd',
  '3': '3rd',
  '4': '4th',
  last: 'last',
};

function pad2(n: number): string {
  return (n < 10 ? '0' : '') + n;
}

export function ymd(y: number, m: number, d: number): string {
  return y + '-' + pad2(m) + '-' + pad2(d);
}

export function parts(s: string): { y: number; m: number; d: number } {
  return { y: +s.slice(0, 4), m: +s.slice(5, 7), d: +s.slice(8, 10) };
}

export function isLeap(y: number): boolean {
  return (y % 4 === 0 && y % 100 !== 0) || y % 400 === 0;
}

export function daysInMonth(y: number, m: number): number {
  return [31, isLeap(y) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1];
}

/* days_from_civil / civil_from_days (Howard Hinnant's algorithms) */
export function toDays(s: string): number {
  const p = parts(s);
  const m = p.m;
  const d = p.d;
  const y = p.y - (m <= 2 ? 1 : 0);
  const era = Math.floor(y / 400);
  const yoe = y - era * 400;
  const doy = Math.floor((153 * (m + (m > 2 ? -3 : 9)) + 2) / 5) + d - 1;
  const doe = yoe * 365 + Math.floor(yoe / 4) - Math.floor(yoe / 100) + doy;
  return era * 146097 + doe - 719468;
}

export function fromDays(days: number): string {
  const z = days + 719468;
  const era = Math.floor(z / 146097);
  const doe = z - era * 146097;
  const yoe = Math.floor(
    (doe - Math.floor(doe / 1460) + Math.floor(doe / 36524) - Math.floor(doe / 146096)) / 365,
  );
  const y = yoe + era * 400;
  const doy = doe - (365 * yoe + Math.floor(yoe / 4) - Math.floor(yoe / 100));
  const mp = Math.floor((5 * doy + 2) / 153);
  const d = doy - Math.floor((153 * mp + 2) / 5) + 1;
  const m = mp + (mp < 10 ? 3 : -9);
  return ymd(m <= 2 ? y + 1 : y, m, d);
}

export function addDays(s: string, n: number): string {
  return fromDays(toDays(s) + n);
}

/** 0 = Sunday .. 6 = Saturday */
export function dayOfWeek(s: string): number {
  return (((toDays(s) + 4) % 7) + 7) % 7;
}

export function isValidDate(s: unknown): s is string {
  return typeof s === 'string' && DATE_RE.test(s) && fromDays(toDays(s)) === s;
}

/** The local calendar date, with the time of day discarded immediately. */
export function todayStr(): string {
  const d = new Date();
  return ymd(d.getFullYear(), d.getMonth() + 1, d.getDate());
}

export function fmtDate(s: string): string {
  if (!isValidDate(s)) return String(s);
  const p = parts(s);
  return MONTHS_SHORT[p.m - 1] + ' ' + p.d + ', ' + p.y;
}

export function daysBetween(a: string, b: string): number {
  return toDays(b) - toDays(a);
}

export function relativeLate(due: string, ref: string): string {
  const n = daysBetween(due, ref);
  if (n <= 0) return '';
  return n === 1 ? '1 day late' : n + ' days late';
}

/** The Sunday on/before `s` — the canonical start of `s`'s calendar week
    (CLAUDE.md §4.1, weekly + daysOfWeek). */
export function weekStart(s: string): string {
  return addDays(s, -dayOfWeek(s));
}

/** Nth (or last) `weekday` of a month, as a date string. */
export function nthWeekday(y: number, m: number, weekday: number, ordinal: string): string {
  const dim = daysInMonth(y, m);
  if (ordinal === 'last') {
    const lastDow = dayOfWeek(ymd(y, m, dim));
    return ymd(y, m, dim - ((lastDow - weekday + 7) % 7));
  }
  const firstDow = dayOfWeek(ymd(y, m, 1));
  let day = 1 + ((weekday - firstDow + 7) % 7) + (Number(ordinal) - 1) * 7;
  if (day > dim) day -= 7; // guard: shouldn't happen for ordinals 1..4
  return ymd(y, m, day);
}
