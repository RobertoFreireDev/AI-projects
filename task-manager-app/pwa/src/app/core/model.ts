/* ============================================================
   Data model — CLAUDE.md §3 / §4.

   These types describe exactly what is stored under the `ptm_data`
   localStorage key and exactly what an exported backup contains, so a file
   exported from Solution A imports here unchanged and vice versa (§15).
   ============================================================ */

export const STORAGE_KEY = 'ptm_data';

export const LIMITS = {
  tasks: 5000,
  notes: 100,
  items: 200,
  completions: 5000,
  str: 2000,
  name: 200,
} as const;

export type RecurrenceType = 'once' | 'daily' | 'weekly' | 'monthly' | 'yearly';
export type MonthlyMode = 'dayOfMonth' | 'weekdayOfMonth';
export type WeekdayOrdinal = '1' | '2' | '3' | '4' | 'last';

export interface Recurrence {
  type: RecurrenceType;
  interval?: number;
  startDate?: string;
  /** weekly only, optional — CLAUDE.md §4.0 */
  daysOfWeek?: number[];
  mode?: MonthlyMode;
  dayOfMonth?: number;
  weekdayOrdinal?: WeekdayOrdinal;
  weekday?: number;
  month?: number;
  day?: number;
  /** once only */
  date?: string;
}

export interface TextNote {
  type: 'text';
  text: string;
}

export interface ChecklistItem {
  text: string;
  checked: boolean;
}

export interface ChecklistNote {
  type: 'checklist';
  items: ChecklistItem[];
}

export type Note = TextNote | ChecklistNote;

export interface Completion {
  dueDate: string;
  completedOn: string;
}

export interface ChecklistState {
  lastResetDueDate: string | null;
  checkedItemIds: string[];
}

export interface Task {
  id: string;
  icon: string;
  name: string;
  notes: Note[];
  recurrence: Recurrence;
  createdAt: string;
  completions: Completion[];
  checklistState: ChecklistState;
  active: boolean;
}

export interface AppData {
  version: 1;
  settings: Record<string, unknown>;
  tasks: Task[];
}

export function emptyData(): AppData {
  return { version: 1, settings: {}, tasks: [] };
}

/* ---------- Home sections (CLAUDE.md §6.1) ---------- */

export type SectionKey = 'todo' | 'pending' | 'done';

export const SECTION_META: Record<SectionKey, { title: string; empty: string }> = {
  todo: { title: 'To do', empty: 'Nothing due today.' },
  pending: { title: 'Pending', empty: 'Nothing overdue.' },
  done: { title: 'Done', empty: 'Nothing completed today yet.' },
};

export function isSectionKey(v: string): v is SectionKey {
  return v === 'todo' || v === 'pending' || v === 'done';
}

/** A task paired with the due date it is currently showing for. */
export interface DueEntry {
  task: Task;
  due: string;
}

export interface IconGroup<T> {
  icon: string;
  entries: T[];
}
