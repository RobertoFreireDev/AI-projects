/* Home's To do / Pending / Done rules and the completion bookkeeping
   (CLAUDE.md §6.1, §6.3, §6.4, §7.2). */

import { STORAGE_KEY, emptyData } from './model';
import type { AppData, ChecklistNote, Recurrence, Task } from './model';
import { TaskStore } from './task-store';

function task(id: string, recurrence: Recurrence, overrides: Partial<Task> = {}): Task {
  return {
    id,
    icon: 'task',
    name: id,
    notes: [],
    recurrence,
    createdAt: '2026-01-01',
    completions: [],
    checklistState: { lastResetDueDate: null, checkedItemIds: [] },
    active: true,
    ...overrides,
  };
}

/** Seed localStorage, then build a store that reads it — the same path the
    real app takes on boot. */
function storeWith(tasks: Task[], today = '2026-08-04'): TaskStore {
  const data: AppData = { ...emptyData(), tasks };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  const store = new TaskStore();
  store.today.set(today);
  store.refreshCycles();
  return store;
}

beforeEach(() => localStorage.clear());

describe('Home sections', () => {
  it('splits tasks into To do, Pending and Done for today', () => {
    const store = storeWith([
      task('due-today', { type: 'daily', interval: 1, startDate: '2026-08-04' }),
      task('overdue', { type: 'once', date: '2026-07-20' }),
      task(
        'done-today',
        { type: 'once', date: '2026-08-01' },
        {
          completions: [{ dueDate: '2026-08-01', completedOn: '2026-08-04' }],
        },
      ),
      task('not-started', { type: 'once', date: '2026-12-25' }),
      task('inactive', { type: 'daily', interval: 1, startDate: '2026-08-04' }, { active: false }),
    ]);

    const b = store.buckets();
    expect(b.todo.map((e) => e.task.id)).toEqual(['due-today']);
    expect(b.pending.map((e) => e.task.id)).toEqual(['overdue']);
    expect(b.done.map((e) => e.task.id)).toEqual(['done-today']);
  });

  it('keeps a cycle completed on a previous day off Home entirely (§6.1)', () => {
    const store = storeWith([
      task(
        'yesterday',
        { type: 'weekly', interval: 1, startDate: '2026-08-01' },
        {
          completions: [{ dueDate: '2026-08-01', completedOn: '2026-08-03' }],
        },
      ),
    ]);

    const b = store.buckets();
    expect(b.todo).toHaveLength(0);
    expect(b.pending).toHaveLength(0);
    expect(b.done).toHaveLength(0);
    expect(store.status(store.tasks()[0]).key).toBe('dormant');
  });

  it('does not resurface a heavily overdue task after it is completed late (§12)', () => {
    const store = storeWith([task('very-late', { type: 'once', date: '2026-06-01' })]);
    const t = store.tasks()[0];

    expect(store.buckets().pending.map((e) => e.task.id)).toEqual(['very-late']);

    store.markDone(t, '2026-06-01');
    expect(store.buckets().done.map((e) => e.task.id)).toEqual(['very-late']);
    expect(store.buckets().pending).toHaveLength(0);

    // The next day it goes dormant rather than back to Pending.
    store.today.set('2026-08-05');
    store.refreshCycles();
    const b = store.buckets();
    expect(b.todo.length + b.pending.length + b.done.length).toBe(0);
  });
});

describe('completing a task', () => {
  it('records the cycle, ticks the checklist, and undoes only today (§6.3)', () => {
    const store = storeWith([
      task(
        'chores',
        { type: 'daily', interval: 1, startDate: '2026-08-04' },
        {
          notes: [
            {
              type: 'checklist',
              items: [
                { text: 'a', checked: false },
                { text: 'b', checked: false },
              ],
            },
          ],
        },
      ),
    ]);
    const t = store.tasks()[0];

    store.markDone(t, '2026-08-04');
    expect(t.completions).toEqual([{ dueDate: '2026-08-04', completedOn: '2026-08-04' }]);
    expect((t.notes[0] as ChecklistNote).items.every((i) => i.checked)).toBe(true);
    expect(t.checklistState.checkedItemIds).toEqual(['0:0', '0:1']);

    store.undoDone(t, '2026-08-04');
    expect(t.completions).toEqual([]);
    expect((t.notes[0] as ChecklistNote).items.some((i) => i.checked)).toBe(false);
    expect(store.buckets().todo).toHaveLength(1);
  });

  it('auto-completes when the last checklist item is ticked (§6.3)', () => {
    const store = storeWith([
      task(
        'cats',
        { type: 'daily', interval: 1, startDate: '2026-08-04' },
        {
          notes: [
            {
              type: 'checklist',
              items: [
                { text: 'água', checked: false },
                { text: 'ração', checked: false },
              ],
            },
          ],
        },
      ),
    ]);
    const t = store.tasks()[0];
    const note = t.notes[0] as ChecklistNote;

    expect(store.toggleItem(t, note, 0, true)).toBe(false);
    expect(t.completions).toHaveLength(0);

    expect(store.toggleItem(t, note, 1, true)).toBe(true);
    expect(t.completions).toEqual([{ dueDate: '2026-08-04', completedOn: '2026-08-04' }]);
    expect(store.buckets().done).toHaveLength(1);
  });

  it('persists through a reload', () => {
    const store = storeWith([task('bills', { type: 'once', date: '2026-08-04' })]);
    store.markDone(store.tasks()[0], '2026-08-04');

    const reloaded = new TaskStore();
    reloaded.today.set('2026-08-04');
    expect(reloaded.tasks()[0].completions).toEqual([
      { dueDate: '2026-08-04', completedOn: '2026-08-04' },
    ]);
  });
});

describe('checklist reset per cycle (§6.4)', () => {
  it('unchecks every item when the due date rolls over', () => {
    const store = storeWith([
      task(
        'daily-list',
        { type: 'daily', interval: 1, startDate: '2026-08-01' },
        {
          notes: [{ type: 'checklist', items: [{ text: 'x', checked: true }] }],
          checklistState: { lastResetDueDate: '2026-08-03', checkedItemIds: ['0:0'] },
        },
      ),
    ]);

    const t = store.tasks()[0];
    expect(t.checklistState.lastResetDueDate).toBe('2026-08-04');
    expect((t.notes[0] as ChecklistNote).items[0].checked).toBe(false);
    expect(t.checklistState.checkedItemIds).toEqual([]);
  });
});

describe('reactivate (§7.2)', () => {
  it('brings a completed one-off task back into the flow', () => {
    const store = storeWith([
      task(
        'once-done',
        { type: 'once', date: '2026-07-01' },
        {
          completions: [{ dueDate: '2026-07-01', completedOn: '2026-07-01' }],
        },
      ),
    ]);
    const t = store.tasks()[0];
    expect(store.status(t).key).toBe('dormant');

    store.reactivate(t, '2026-07-01');
    expect(t.completions).toEqual([]);
    expect(store.buckets().pending.map((e) => e.task.id)).toEqual(['once-done']);
  });
});

describe('import (§7.3)', () => {
  const incoming: AppData = {
    version: 1,
    settings: {},
    tasks: [
      task(
        'shared',
        { type: 'daily', interval: 2, startDate: '2026-08-01' },
        { name: 'from backup' },
      ),
    ],
  };

  it('merge overwrites by id and keeps everything else', () => {
    const store = storeWith([
      task('shared', { type: 'daily', interval: 1, startDate: '2026-08-01' }, { name: 'local' }),
      task('local-only', { type: 'once', date: '2026-08-04' }),
    ]);

    store.merge(incoming);
    expect(store.tasks().map((t) => t.name)).toEqual(['from backup', 'local-only']);
  });

  it('replace discards local tasks entirely', () => {
    const store = storeWith([task('local-only', { type: 'once', date: '2026-08-04' })]);
    store.replaceAll(incoming);
    expect(store.tasks().map((t) => t.id)).toEqual(['shared']);
  });
});

describe('stored data', () => {
  it('is discarded rather than trusted when it fails validation (§9)', () => {
    localStorage.setItem(STORAGE_KEY, '{"version":1,"tasks":[{"id":"broken"}]}');
    expect(new TaskStore().tasks()).toEqual([]);
  });

  it('survives a non-JSON value under the key', () => {
    localStorage.setItem(STORAGE_KEY, 'not json at all');
    expect(new TaskStore().tasks()).toEqual([]);
  });
});
