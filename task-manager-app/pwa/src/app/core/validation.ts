/* ============================================================
   Strict validation of anything that arrives from outside the running app
   — an imported backup file, or whatever happens to be sitting under the
   `ptm_data` localStorage key (CLAUDE.md §7.3 / §9).

   Nothing reaches the store or the DOM until it has passed through here.
   ============================================================ */

import { isValidDate } from './date';
import { iconKey } from './icons';
import { LIMITS } from './model';
import type { AppData, ChecklistState, Note, Recurrence, Task, WeekdayOrdinal } from './model';
import { weekDaysList } from './recurrence';

export type ValidationResult = { ok: true; data: AppData } | { ok: false; error: string };

type Dict = Record<string, unknown>;

function isPlainObject(o: unknown): o is Dict {
  return !!o && typeof o === 'object' && !Array.isArray(o);
}

function isStr(v: unknown, max: number): v is string {
  return typeof v === 'string' && v.length <= max;
}

function isInt(v: unknown, min: number, max: number): v is number {
  return typeof v === 'number' && isFinite(v) && v % 1 === 0 && v >= min && v <= max;
}

function validateRecurrence(r: unknown, path: string): string | null {
  if (!isPlainObject(r)) return path + ': recurrence must be an object';
  const types = ['once', 'daily', 'weekly', 'monthly', 'yearly'];
  if (typeof r['type'] !== 'string' || types.indexOf(r['type']) === -1) {
    return path + ': unknown recurrence type';
  }
  if (r['interval'] != null && !isInt(r['interval'], 1, 999)) {
    return path + ': interval must be an integer 1..999';
  }
  if (r['startDate'] != null && !isValidDate(r['startDate'])) return path + ': invalid startDate';

  if (r['daysOfWeek'] != null) {
    // CLAUDE.md §4.0
    if (r['type'] !== 'weekly') return path + ': daysOfWeek only applies to weekly recurrence';
    const dows = r['daysOfWeek'];
    if (!Array.isArray(dows) || dows.length > 7) {
      return path + ': daysOfWeek must be an array of at most 7 weekdays';
    }
    const seen = new Set<number>();
    for (const dv of dows) {
      if (!isInt(dv, 0, 6)) return path + ': daysOfWeek entries must be integers 0..6';
      if (seen.has(dv)) return path + ': duplicate weekday in daysOfWeek';
      seen.add(dv);
    }
  }

  if (r['type'] === 'once' && !isValidDate(r['date'])) {
    return path + ': "once" needs a valid date';
  }

  if (r['type'] === 'monthly') {
    if (r['mode'] !== 'dayOfMonth' && r['mode'] !== 'weekdayOfMonth') {
      return path + ': invalid monthly mode';
    }
    if (r['mode'] === 'dayOfMonth') {
      if (!isInt(r['dayOfMonth'], 1, 31)) return path + ': dayOfMonth must be 1..31';
    } else {
      if (['1', '2', '3', '4', 'last'].indexOf(String(r['weekdayOrdinal'])) === -1) {
        return path + ': invalid weekdayOrdinal';
      }
      if (!isInt(r['weekday'], 0, 6)) return path + ': weekday must be 0..6';
    }
  }

  if (r['type'] === 'yearly') {
    if (!isInt(r['month'], 1, 12)) return path + ': month must be 1..12';
    if (!isInt(r['day'], 1, 31)) return path + ': day must be 1..31';
  }

  return null;
}

function validateTask(t: unknown, i: number): string | null {
  const p = 'tasks[' + i + ']';
  if (!isPlainObject(t)) return p + ' is not an object';
  if (!isStr(t['id'], 100) || !t['id']) return p + ': missing/invalid id';
  if (!isStr(t['icon'], 40) || !t['icon']) return p + ': missing/invalid icon';
  if (!isStr(t['name'], LIMITS.name) || !t['name'].trim()) return p + ': missing/invalid name';

  const notes = t['notes'];
  if (!Array.isArray(notes) || notes.length > LIMITS.notes) {
    return p + ': notes must be an array (max ' + LIMITS.notes + ')';
  }
  for (let n = 0; n < notes.length; n++) {
    const note: unknown = notes[n];
    const np = p + '.notes[' + n + ']';
    if (!isPlainObject(note)) return np + ' is not an object';
    if (note['type'] === 'text') {
      if (!isStr(note['text'], LIMITS.str)) return np + ': text note needs a string "text"';
    } else if (note['type'] === 'checklist') {
      const items = note['items'];
      if (!Array.isArray(items) || items.length > LIMITS.items) {
        return np + ': items must be an array (max ' + LIMITS.items + ')';
      }
      for (let k = 0; k < items.length; k++) {
        const it: unknown = items[k];
        if (!isPlainObject(it) || !isStr(it['text'], LIMITS.str)) {
          return np + '.items[' + k + ']: invalid text';
        }
        if (it['checked'] != null && typeof it['checked'] !== 'boolean') {
          return np + '.items[' + k + ']: checked must be boolean';
        }
      }
    } else {
      return np + ': unknown note type';
    }
  }

  const re = validateRecurrence(t['recurrence'], p);
  if (re) return re;

  if (!isValidDate(t['createdAt'])) return p + ': invalid createdAt';

  if (t['completions'] != null) {
    const comps = t['completions'];
    if (!Array.isArray(comps) || comps.length > LIMITS.completions) {
      return p + ': completions must be an array';
    }
    for (let c = 0; c < comps.length; c++) {
      const comp: unknown = comps[c];
      if (
        !isPlainObject(comp) ||
        !isValidDate(comp['dueDate']) ||
        !isValidDate(comp['completedOn'])
      ) {
        return p + '.completions[' + c + ']: invalid entry';
      }
    }
  }

  if (t['checklistState'] != null) {
    const cs = t['checklistState'];
    if (!isPlainObject(cs)) return p + ': checklistState must be an object';
    if (cs['lastResetDueDate'] != null && !isValidDate(cs['lastResetDueDate'])) {
      return p + ': invalid lastResetDueDate';
    }
    if (cs['checkedItemIds'] != null) {
      const ids = cs['checkedItemIds'];
      if (!Array.isArray(ids) || ids.length > LIMITS.items * LIMITS.notes) {
        return p + ': checkedItemIds must be an array';
      }
      for (let q = 0; q < ids.length; q++) {
        if (!isStr(ids[q], 80)) return p + '.checkedItemIds[' + q + ']: invalid id';
      }
    }
  }

  if (t['active'] != null && typeof t['active'] !== 'boolean')
    return p + ': active must be boolean';
  return null;
}

/** Keep only the fields that matter for the chosen recurrence type. */
export function normaliseRecurrence(raw: Dict): Recurrence {
  const type = raw['type'] as Recurrence['type'];
  if (type === 'once') return { type: 'once', date: raw['date'] as string };

  const out: Recurrence = {
    type,
    interval: Math.max(1, Math.floor(Number(raw['interval']) || 1)),
    startDate: raw['startDate'] as string | undefined,
  };

  if (type === 'weekly') {
    /* Dropped when absent or empty, which is exactly the legacy
       single-weekday-from-startDate behavior (CLAUDE.md §4.0). */
    const dows = weekDaysList({ type: 'weekly', daysOfWeek: raw['daysOfWeek'] as number[] });
    if (dows) out.daysOfWeek = dows;
  }
  if (type === 'monthly') {
    out.mode = raw['mode'] as Recurrence['mode'];
    if (out.mode === 'weekdayOfMonth') {
      out.weekdayOrdinal = String(raw['weekdayOrdinal']) as WeekdayOrdinal;
      out.weekday = Number(raw['weekday']);
    } else {
      out.dayOfMonth = Number(raw['dayOfMonth']);
    }
  }
  if (type === 'yearly') {
    out.month = Number(raw['month']);
    out.day = Number(raw['day']);
  }
  return out;
}

/** Turn a validated raw task into the canonical shape, filling in the
    bookkeeping fields (completions/checklistState/active) when absent. */
export function normaliseTask(raw: Dict): Task {
  const notes: Note[] = (raw['notes'] as Dict[]).map((n) =>
    n['type'] === 'text'
      ? { type: 'text', text: n['text'] as string }
      : {
          type: 'checklist',
          items: (n['items'] as Dict[]).map((it) => ({
            text: it['text'] as string,
            checked: it['checked'] === true,
          })),
        },
  );

  const cs: Dict = isPlainObject(raw['checklistState']) ? raw['checklistState'] : {};
  const checklistState: ChecklistState = {
    lastResetDueDate: isValidDate(cs['lastResetDueDate']) ? cs['lastResetDueDate'] : null,
    checkedItemIds: Array.isArray(cs['checkedItemIds'])
      ? [...(cs['checkedItemIds'] as string[])]
      : [],
  };

  return {
    id: raw['id'] as string,
    icon: iconKey(raw['icon']),
    name: raw['name'] as string,
    notes,
    recurrence: normaliseRecurrence(raw['recurrence'] as Dict),
    createdAt: raw['createdAt'] as string,
    completions: Array.isArray(raw['completions'])
      ? (raw['completions'] as Dict[]).map((c) => ({
          dueDate: c['dueDate'] as string,
          completedOn: c['completedOn'] as string,
        }))
      : [],
    checklistState,
    active: raw['active'] !== false,
  };
}

/** Validate a whole `ptm_data` payload. Nothing is written anywhere unless
    this returns `{ ok: true }`. */
export function validateData(raw: unknown): ValidationResult {
  if (!isPlainObject(raw)) return { ok: false, error: 'Root value is not an object' };
  if (raw['version'] !== 1) return { ok: false, error: 'Unsupported version (expected 1)' };
  if (raw['settings'] != null && !isPlainObject(raw['settings'])) {
    return { ok: false, error: 'settings must be an object' };
  }
  const tasks = raw['tasks'];
  if (!Array.isArray(tasks)) return { ok: false, error: 'tasks must be an array' };
  if (tasks.length > LIMITS.tasks) {
    return { ok: false, error: 'Too many tasks (max ' + LIMITS.tasks + ')' };
  }

  const seen = new Set<string>();
  const out: Task[] = [];
  for (let i = 0; i < tasks.length; i++) {
    const err = validateTask(tasks[i], i);
    if (err) return { ok: false, error: err };
    const t = tasks[i] as Dict;
    const id = t['id'] as string;
    if (seen.has(id)) return { ok: false, error: 'Duplicate task id: ' + id };
    seen.add(id);
    out.push(normaliseTask(t));
  }

  return {
    ok: true,
    data: {
      version: 1,
      settings: (raw['settings'] as Record<string, unknown>) ?? {},
      tasks: out,
    },
  };
}
