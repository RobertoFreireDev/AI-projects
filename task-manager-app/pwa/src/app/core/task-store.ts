/* ============================================================
   TaskStore — the single owner of `ptm_data` (CLAUDE.md §2/§3/§6).

   Everything lives in localStorage; there is no backend and no network
   call anywhere in this file. State is exposed as signals so the pages
   re-render on their own instead of the manual render() pass Solution A
   needed.
   ============================================================ */

import { Injectable, computed, signal } from '@angular/core';
import { fmtDate, isValidDate, relativeLate, todayStr } from './date';
import { iconKey } from './icons';
import type {
  AppData,
  ChecklistNote,
  Completion,
  DueEntry,
  IconGroup,
  SectionKey,
  Task,
} from './model';
import { STORAGE_KEY, emptyData } from './model';
import { lastDueDate, nextDueDate } from './recurrence';
import { validateData } from './validation';

/** How a task currently reads on the Tasks page (CLAUDE.md §7.2). */
export interface TaskStatus {
  key: 'inactive' | 'future' | 'todo' | 'pending' | 'done' | 'dormant';
  due: string | null;
  label: string;
}

export interface Buckets {
  todo: DueEntry[];
  pending: DueEntry[];
  done: DueEntry[];
}

@Injectable({ providedIn: 'root' })
export class TaskStore {
  /** Bumped on every mutation; every computed below depends on it. */
  private readonly revision = signal(0);
  private data: AppData = emptyData();

  /** The current local calendar date. Kept in a signal so a day rollover
      re-evaluates every section on its own (CLAUDE.md §12). */
  readonly today = signal(todayStr());
  readonly storageBroken = signal(false);

  /* A fresh array on every revision, deliberately: a computed that returned
     the same array reference would compare equal to its previous value and
     Angular would stop the change propagating to `buckets` — mutations
     happen inside the Task objects, so the reference alone proves nothing. */
  readonly tasks = computed<readonly Task[]>(() => {
    this.revision();
    return [...this.data.tasks];
  });
  readonly taskCount = computed(() => this.tasks().length);
  readonly activeCount = computed(() => this.tasks().filter((t) => t.active).length);

  /** Home sections for today (CLAUDE.md §6.1). */
  readonly buckets = computed<Buckets>(() => {
    const ref = this.today();
    const out: Buckets = { todo: [], pending: [], done: [] };
    for (const task of this.tasks()) {
      if (!task.active) continue;
      const due = lastDueDate(task, ref);
      if (!due) continue;
      const comp = completionFor(task, due);
      if (!comp) {
        if (due === ref) out.todo.push({ task, due });
        else if (due < ref) out.pending.push({ task, due });
      } else if (comp.completedOn === ref) {
        out.done.push({ task, due });
      }
    }
    return out;
  });

  constructor() {
    this.data = this.read();
    this.refreshCycles();
    this.watchDayRollover();
  }

  /* ---------- persistence ---------- */

  private read(): AppData {
    let raw: string | null;
    try {
      raw = localStorage.getItem(STORAGE_KEY);
    } catch {
      this.storageBroken.set(true);
      return emptyData();
    }
    if (!raw) return emptyData();
    try {
      const result = validateData(JSON.parse(raw));
      if (!result.ok) {
        console.warn('Stored data rejected:', result.error);
        return emptyData();
      }
      return result.data;
    } catch {
      return emptyData();
    }
  }

  private write(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.data));
    } catch {
      this.storageBroken.set(true);
    }
  }

  /** Run a mutation, persist it, and let the signals fan the change out. */
  private mutate(fn: () => void): void {
    fn();
    this.write();
    this.revision.update((n) => n + 1);
  }

  /** The full payload, for export (CLAUDE.md §7.3). */
  snapshot(): AppData {
    this.revision();
    return this.data;
  }

  /* ---------- lookups ---------- */

  byId(id: string): Task | undefined {
    return this.tasks().find((t) => t.id === id);
  }

  dueFor(task: Task): string | null {
    return lastDueDate(task, this.today());
  }

  status(task: Task): TaskStatus {
    const ref = this.today();
    const due = lastDueDate(task, ref);
    if (!task.active) return { key: 'inactive', due, label: 'Inactive' };

    if (!due) {
      const r = task.recurrence;
      const starts = r.type === 'once' ? r.date : r.startDate;
      return {
        key: 'future',
        due: null,
        label: isValidDate(starts) ? 'Starts ' + fmtDate(starts) : 'Not started',
      };
    }

    const comp = completionFor(task, due);
    if (!comp) {
      return due === ref
        ? { key: 'todo', due, label: 'Due today' }
        : { key: 'pending', due, label: relativeLate(due, ref) || 'Overdue' };
    }
    if (comp.completedOn === ref) return { key: 'done', due, label: 'Done today' };

    const next = nextDueDate(task, due);
    return {
      key: 'dormant',
      due,
      label: next ? 'Dormant until ' + fmtDate(next) : 'Dormant — no further occurrence',
    };
  }

  section(key: SectionKey): DueEntry[] {
    return this.buckets()[key];
  }

  /* ---------- mutations (CLAUDE.md §6.3 / §7.2) ---------- */

  /** Record `{dueDate, completedOn: today}` and tick every checklist item. */
  markDone(task: Task, due: string): void {
    if (!due) return;
    this.mutate(() => {
      if (!completionFor(task, due)) {
        task.completions.push({ dueDate: due, completedOn: this.today() });
      }
      setAllChecked(task, true);
      task.checklistState.lastResetDueDate = due;
    });
  }

  /** Undo removes only today's entry for this cycle. Items are unchecked too,
      otherwise the task returns to To do with a fully ticked checklist and no
      way to re-trigger auto-complete. */
  undoDone(task: Task, due: string): void {
    const ref = this.today();
    this.mutate(() => {
      task.completions = task.completions.filter(
        (c) => !(c.dueDate === due && c.completedOn === ref),
      );
      setAllChecked(task, false);
    });
  }

  /** Reactivate (CLAUDE.md §7.2) — drops the completion entry keeping the
      current cycle satisfied, whenever it was recorded, so the task matches
      §6.1's "no entry found" case again. Nothing is ever deleted. */
  reactivate(task: Task, due: string): void {
    this.mutate(() => {
      task.completions = task.completions.filter((c) => c.dueDate !== due);
      setAllChecked(task, false);
      task.checklistState.lastResetDueDate = due;
    });
  }

  /** Toggle one checklist item. Returns true when that completed the task
      (every item ticked auto-completes it — CLAUDE.md §6.3). */
  toggleItem(task: Task, note: ChecklistNote, index: number, checked: boolean): boolean {
    const due = this.dueFor(task);
    let completed = false;
    this.mutate(() => {
      note.items[index].checked = checked;
      syncCheckedIds(task);
      if (checked && due && allChecked(task) && !completionFor(task, due)) {
        task.completions.push({ dueDate: due, completedOn: this.today() });
        task.checklistState.lastResetDueDate = due;
        completed = true;
      }
    });
    return completed;
  }

  upsert(task: Task): 'created' | 'updated' {
    const idx = this.tasks().findIndex((t) => t.id === task.id);
    this.mutate(() => {
      syncCheckedIds(task);
      if (idx >= 0) this.data.tasks[idx] = task;
      else this.data.tasks.push(task);
    });
    this.refreshCycles();
    return idx >= 0 ? 'updated' : 'created';
  }

  /** The only way a task is ever removed (CLAUDE.md §10.8) — always
      user-initiated, from the Tasks page, behind a confirmation. */
  remove(id: string): void {
    this.mutate(() => {
      this.data.tasks = this.data.tasks.filter((t) => t.id !== id);
    });
  }

  /* ---------- import (CLAUDE.md §7.3) ---------- */

  replaceAll(incoming: AppData): void {
    this.mutate(() => {
      this.data = { version: 1, settings: incoming.settings ?? {}, tasks: incoming.tasks };
    });
    this.refreshCycles();
  }

  /** Merge by task id: an incoming task overwrites the local one with the
      same id, the rest are appended in their original order. */
  merge(incoming: AppData): void {
    this.mutate(() => {
      const byId = new Map<string, Task>();
      for (const t of this.data.tasks) byId.set(t.id, t);
      for (const t of incoming.tasks) byId.set(t.id, t);

      const merged: Task[] = [];
      const taken = new Set<string>();
      for (const t of this.data.tasks) {
        if (taken.has(t.id)) continue;
        taken.add(t.id);
        merged.push(byId.get(t.id)!);
      }
      for (const t of incoming.tasks) {
        if (taken.has(t.id)) continue;
        taken.add(t.id);
        merged.push(byId.get(t.id)!);
      }

      this.data = {
        version: 1,
        settings: { ...this.data.settings, ...(incoming.settings ?? {}) },
        tasks: merged,
      };
    });
    this.refreshCycles();
  }

  /* ---------- cycle bookkeeping (CLAUDE.md §6.4) ---------- */

  /** Uncheck every checklist item whose cycle has rolled over. */
  refreshCycles(): void {
    const ref = this.today();
    let changed = false;
    for (const t of this.data.tasks) {
      const due = lastDueDate(t, ref);
      if (t.checklistState.lastResetDueDate !== due) {
        setAllChecked(t, false);
        t.checklistState.lastResetDueDate = due;
        changed = true;
      }
    }
    if (changed) {
      this.write();
      this.revision.update((n) => n + 1);
    }
  }

  /** Day rollover while the app is left open (CLAUDE.md §12). */
  private watchDayRollover(): void {
    if (typeof window === 'undefined') return;

    const check = () => {
      const now = todayStr();
      if (now === this.today()) return false;
      this.today.set(now);
      this.refreshCycles();
      return true;
    };

    setInterval(check, 20000);
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') check();
    });
  }
}

/* ============================================================
   Free helpers — pure, shared with the pages.
   ============================================================ */

export function completionFor(task: Task, due: string | null): Completion | null {
  if (!due) return null;
  return task.completions.find((c) => c.dueDate === due) ?? null;
}

export function checklists(task: Task): ChecklistNote[] {
  return task.notes.filter((n): n is ChecklistNote => n.type === 'checklist' && n.items.length > 0);
}

export function allChecked(task: Task): boolean {
  const lists = checklists(task);
  return lists.length > 0 && lists.every((n) => n.items.every((it) => it.checked));
}

export function setAllChecked(task: Task, value: boolean): void {
  for (const n of task.notes) {
    if (n.type === 'checklist') for (const it of n.items) it.checked = value;
  }
  syncCheckedIds(task);
}

/** `checkedItemIds` is a derived mirror of `item.checked`, kept in the
    documented "noteIndex:itemIndex" shape for export fidelity (§3.1). */
export function syncCheckedIds(task: Task): void {
  const ids: string[] = [];
  task.notes.forEach((n, ni) => {
    if (n.type !== 'checklist') return;
    n.items.forEach((it, ii) => {
      if (it.checked) ids.push(ni + ':' + ii);
    });
  });
  task.checklistState.checkedItemIds = ids;
}

/** Group entries by icon, biggest group first (CLAUDE.md §6.2). */
export function groupByIcon<T extends { task: Task }>(entries: readonly T[]): IconGroup<T>[] {
  const map = new Map<string, T[]>();
  for (const e of entries) {
    const k = iconKey(e.task.icon);
    const list = map.get(k);
    if (list) list.push(e);
    else map.set(k, [e]);
  }
  return [...map.entries()]
    .map(([icon, list]) => ({ icon, entries: list }))
    .sort((a, b) => b.entries.length - a.entries.length || a.icon.localeCompare(b.icon));
}
