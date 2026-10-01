/* Import is the only path by which foreign data reaches the app, so it is
   also the only place a malformed file could corrupt the store
   (CLAUDE.md §7.3, §9, §12). */

import { readFileSync } from 'node:fs';
import { validateData } from './validation';

function payload(task: Record<string, unknown>) {
  return { version: 1, settings: {}, tasks: [task] };
}

const valid = {
  id: 'a1',
  icon: 'money',
  name: 'Pagar cartão',
  notes: [
    { type: 'text', text: 'vence dia 10' },
    { type: 'checklist', items: [{ text: 'conferir fatura', checked: false }] },
  ],
  recurrence: {
    type: 'monthly',
    interval: 1,
    startDate: '2026-01-10',
    mode: 'dayOfMonth',
    dayOfMonth: 10,
  },
  createdAt: '2026-01-01',
  completions: [{ dueDate: '2026-07-10', completedOn: '2026-07-11' }],
  checklistState: { lastResetDueDate: '2026-08-10', checkedItemIds: [] },
  active: true,
};

describe('validateData — accepts', () => {
  it('a well-formed backup', () => {
    const result = validateData(payload(valid));
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.tasks).toHaveLength(1);
    expect(result.data.tasks[0].name).toBe('Pagar cartão');
  });

  it('a task missing its optional bookkeeping fields, filling them in', () => {
    const bare = { ...valid };
    delete (bare as Record<string, unknown>)['completions'];
    delete (bare as Record<string, unknown>)['checklistState'];
    delete (bare as Record<string, unknown>)['active'];

    const result = validateData(payload(bare));
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    const t = result.data.tasks[0];
    expect(t.completions).toEqual([]);
    expect(t.checklistState).toEqual({ lastResetDueDate: null, checkedItemIds: [] });
    expect(t.active).toBe(true);
  });

  it('an unknown icon, falling back to "task" (§5)', () => {
    const result = validateData(payload({ ...valid, icon: 'spaceship' }));
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.tasks[0].icon).toBe('task');
  });

  it('and drops recurrence fields that do not belong to the chosen type', () => {
    const result = validateData(
      payload({
        ...valid,
        recurrence: {
          type: 'weekly',
          interval: 1,
          startDate: '2026-01-05',
          daysOfWeek: [6, 2, 4],
          dayOfMonth: 9, // monthly field on a weekly task
        },
      }),
    );
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.tasks[0].recurrence).toEqual({
      type: 'weekly',
      interval: 1,
      startDate: '2026-01-05',
      daysOfWeek: [2, 4, 6], // sorted
    });
  });
});

describe('cross-target compatibility (§15)', () => {
  it('accepts the example backup written for Solution A', () => {
    // The file lives at the repo root; vitest runs with /pwa as cwd.
    const raw: unknown = JSON.parse(readFileSync('../tarefas-exemplo.json', 'utf8'));
    const result = validateData(raw);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.tasks.length).toBeGreaterThan(0);
    // Round-tripping through export must not change what a re-import sees.
    const again = validateData(JSON.parse(JSON.stringify(result.data)));
    expect(again.ok).toBe(true);
    if (again.ok) expect(again.data).toEqual(result.data);
  });
});

describe('validateData — rejects', () => {
  const cases: Array<[string, unknown]> = [
    ['a non-object root', 'nope'],
    ['an unsupported version', { version: 2, tasks: [] }],
    ['a missing tasks array', { version: 1, settings: {} }],
    ['a task without a name', payload({ ...valid, name: '   ' })],
    ['a task without an icon', payload({ ...valid, icon: '' })],
    ['an invalid createdAt', payload({ ...valid, createdAt: '2026-13-01' })],
    ['an unknown recurrence type', payload({ ...valid, recurrence: { type: 'hourly' } })],
    ['a once recurrence without a date', payload({ ...valid, recurrence: { type: 'once' } })],
    [
      'an out-of-range interval',
      payload({ ...valid, recurrence: { type: 'daily', interval: 0, startDate: '2026-01-01' } }),
    ],
    [
      'daysOfWeek on a non-weekly recurrence (§4.0)',
      payload({
        ...valid,
        recurrence: { type: 'daily', interval: 1, startDate: '2026-01-01', daysOfWeek: [1] },
      }),
    ],
    [
      'a weekday outside 0..6',
      payload({
        ...valid,
        recurrence: { type: 'weekly', interval: 1, startDate: '2026-01-01', daysOfWeek: [7] },
      }),
    ],
    ['an unknown note type', payload({ ...valid, notes: [{ type: 'audio', src: 'x' }] })],
    [
      'a completion with an impossible date',
      payload({ ...valid, completions: [{ dueDate: '2026-02-30', completedOn: '2026-03-01' }] }),
    ],
    ['an over-long name', payload({ ...valid, name: 'x'.repeat(201) })],
  ];

  for (const [label, input] of cases) {
    it(label, () => {
      const result = validateData(input);
      expect(result.ok).toBe(false);
      if (!result.ok) expect(result.error).toBeTruthy();
    });
  }

  it('duplicate task ids', () => {
    const result = validateData({ version: 1, settings: {}, tasks: [valid, { ...valid }] });
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain('Duplicate task id');
  });
});
