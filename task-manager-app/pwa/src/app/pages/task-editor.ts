import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import type { OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Chrome } from '../core/chrome';
import { MONTHS, WEEKDAYS, WEEKDAYS_MIN, isValidDate, todayStr } from '../core/date';
import { ICON_SET, isIconKey } from '../core/icons';
import { LIMITS } from '../core/model';
import type {
  ChecklistNote,
  Completion,
  MonthlyMode,
  Note,
  Recurrence,
  RecurrenceType,
  Task,
  WeekdayOrdinal,
} from '../core/model';
import { recurrenceLabel, weekDaysList } from '../core/recurrence';
import { TaskStore, syncCheckedIds } from '../core/task-store';
import { Toast } from '../core/toast';
import { Icon } from '../ui/icon';

/** Every recurrence field at once, so switching type in the form never
    discards what the user already typed for another type. Only the fields
    that matter for the chosen type are written out on save (§4). */
interface DraftRecurrence {
  type: RecurrenceType;
  interval: number;
  startDate: string;
  daysOfWeek: number[];
  mode: MonthlyMode;
  dayOfMonth: number;
  weekdayOrdinal: WeekdayOrdinal;
  weekday: number;
  month: number;
  day: number;
  date: string;
}

interface Draft {
  id: string;
  icon: string;
  name: string;
  notes: Note[];
  recurrence: DraftRecurrence;
  createdAt: string;
  completions: Completion[];
  checklistState: { lastResetDueDate: string | null; checkedItemIds: string[] };
  active: boolean;
}

/** Create/edit form: icon picker, name, notes, recurrence builder
    (CLAUDE.md §7.2). */
@Component({
  selector: 'app-task-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Icon],
  templateUrl: './task-editor.html',
})
export class TaskEditor implements OnInit {
  private readonly chrome = inject(Chrome);
  private readonly router = inject(Router);
  private readonly toast = inject(Toast);
  private readonly store = inject(TaskStore);

  /** Route param: a task id, or the literal 'new'. */
  readonly id = input.required<string>();

  protected readonly limits = LIMITS;
  protected readonly iconSet = ICON_SET;
  protected readonly weekdaysMin = WEEKDAYS_MIN;
  protected readonly weekdays = WEEKDAYS;
  protected readonly months = MONTHS;
  protected readonly ordinals: ReadonlyArray<{ value: WeekdayOrdinal; label: string }> = [
    { value: '1', label: 'First' },
    { value: '2', label: 'Second' },
    { value: '3', label: 'Third' },
    { value: '4', label: 'Fourth' },
    { value: 'last', label: 'Last' },
  ];
  protected readonly types: ReadonlyArray<{ value: RecurrenceType; label: string }> = [
    { value: 'once', label: 'Once' },
    { value: 'daily', label: 'Daily' },
    { value: 'weekly', label: 'Weekly' },
    { value: 'monthly', label: 'Monthly' },
    { value: 'yearly', label: 'Yearly' },
  ];
  protected readonly units: Record<string, string> = {
    daily: 'days',
    weekly: 'weeks',
    monthly: 'months',
    yearly: 'years',
  };

  protected readonly error = signal<string | null>(null);
  protected draft!: Draft;
  protected isNew = true;

  constructor() {
    this.chrome.back.set(['/tasks']);
  }

  ngOnInit(): void {
    const id = this.id();
    const ref = todayStr();
    this.isNew = id === 'new';
    this.chrome.set(this.isNew ? 'New task' : 'Edit task');

    if (this.isNew) {
      this.draft = {
        id: newId(),
        icon: 'task',
        name: '',
        notes: [],
        recurrence: {
          type: 'daily',
          interval: 1,
          startDate: ref,
          daysOfWeek: [],
          mode: 'dayOfMonth',
          dayOfMonth: 1,
          weekdayOrdinal: '1',
          weekday: 1,
          month: 1,
          day: 1,
          date: ref,
        },
        createdAt: ref,
        completions: [],
        checklistState: { lastResetDueDate: null, checkedItemIds: [] },
        active: true,
      };
      return;
    }

    const task = this.store.byId(id);
    if (!task) {
      void this.router.navigate(['/tasks']);
      return;
    }
    const copy: Task = structuredClone(task);
    const r = copy.recurrence;
    this.draft = {
      ...copy,
      recurrence: {
        type: r.type,
        interval: r.interval || 1,
        startDate: r.startDate || copy.createdAt,
        daysOfWeek: Array.isArray(r.daysOfWeek) ? [...r.daysOfWeek] : [],
        mode: r.mode ?? 'dayOfMonth',
        dayOfMonth: r.dayOfMonth || 1,
        weekdayOrdinal:
          r.weekdayOrdinal != null ? (String(r.weekdayOrdinal) as WeekdayOrdinal) : '1',
        weekday: r.weekday ?? 1,
        month: r.month || 1,
        day: r.day || 1,
        date: r.date || copy.createdAt,
      },
    };
  }

  /* ---------- icon ---------- */

  protected pickIcon(key: string): void {
    this.draft.icon = key;
  }

  /* ---------- notes ---------- */

  protected addTextNote(): void {
    this.draft.notes.push({ type: 'text', text: '' });
  }

  protected addChecklist(): void {
    this.draft.notes.push({ type: 'checklist', items: [{ text: '', checked: false }] });
  }

  protected removeNote(index: number): void {
    this.draft.notes.splice(index, 1);
  }

  protected addItem(note: ChecklistNote): void {
    note.items.push({ text: '', checked: false });
  }

  protected removeItem(note: ChecklistNote, index: number): void {
    note.items.splice(index, 1);
  }

  protected asChecklist(note: Note): ChecklistNote {
    return note as ChecklistNote;
  }

  /* ---------- recurrence ---------- */

  protected toggleWeekday(day: number): void {
    const at = this.draft.recurrence.daysOfWeek.indexOf(day);
    if (at === -1) this.draft.recurrence.daysOfWeek.push(day);
    else this.draft.recurrence.daysOfWeek.splice(at, 1);
  }

  protected hasWeekday(day: number): boolean {
    return this.draft.recurrence.daysOfWeek.includes(day);
  }

  protected clampInterval(): void {
    const r = this.draft.recurrence;
    r.interval = Math.min(999, Math.max(1, Math.floor(Number(r.interval) || 1)));
  }

  protected clampDayOfMonth(): void {
    const r = this.draft.recurrence;
    r.dayOfMonth = Math.min(31, Math.max(1, Math.floor(Number(r.dayOfMonth) || 1)));
  }

  protected clampDay(): void {
    const r = this.draft.recurrence;
    r.day = Math.min(31, Math.max(1, Math.floor(Number(r.day) || 1)));
  }

  protected oncePreview(): string {
    const r = this.draft.recurrence;
    return isValidDate(r.date) ? recurrenceLabel({ type: 'once', date: r.date }) : '';
  }

  /* ---------- save / delete ---------- */

  protected save(): void {
    const d = this.draft;
    const name = (d.name || '').trim();
    if (!name) return this.fail('Give the task a name.');
    if (!isIconKey(d.icon)) return this.fail('Pick an icon.');

    const r = d.recurrence;
    let recurrence: Recurrence;

    if (r.type === 'once') {
      if (!isValidDate(r.date)) return this.fail('Pick a valid date for the one-off task.');
      recurrence = { type: 'once', date: r.date };
    } else {
      if (!isValidDate(r.startDate)) return this.fail('Pick a valid start date.');
      recurrence = {
        type: r.type,
        interval: Math.max(1, Math.floor(Number(r.interval) || 1)),
        startDate: r.startDate,
      };
      if (r.type === 'weekly') {
        // sorted + de-duped; omitted when none picked (CLAUDE.md §4.0)
        const dows = weekDaysList({ type: 'weekly', daysOfWeek: r.daysOfWeek });
        if (dows) recurrence.daysOfWeek = dows;
      }
      if (r.type === 'monthly') {
        recurrence.mode = r.mode;
        if (r.mode === 'weekdayOfMonth') {
          recurrence.weekdayOrdinal = String(r.weekdayOrdinal) as WeekdayOrdinal;
          recurrence.weekday = Number(r.weekday);
        } else {
          recurrence.dayOfMonth = Number(r.dayOfMonth);
        }
      }
      if (r.type === 'yearly') {
        recurrence.month = Number(r.month);
        recurrence.day = Number(r.day);
      }
    }

    const notes: Note[] = d.notes
      .map((n): Note =>
        n.type === 'text'
          ? { type: 'text', text: (n.text || '').trim() }
          : {
              type: 'checklist',
              items: n.items
                .map((i) => ({ text: (i.text || '').trim(), checked: !!i.checked }))
                .filter((i) => i.text),
            },
      )
      .filter((n) => (n.type === 'text' ? n.text : n.items.length));

    const task: Task = {
      id: d.id,
      icon: d.icon,
      name,
      notes,
      recurrence,
      createdAt: isValidDate(d.createdAt) ? d.createdAt : todayStr(),
      completions: Array.isArray(d.completions) ? d.completions : [],
      checklistState: d.checklistState ?? { lastResetDueDate: null, checkedItemIds: [] },
      active: d.active !== false,
    };
    syncCheckedIds(task);

    const outcome = this.store.upsert(task);
    this.error.set(null);
    this.toast.show(outcome === 'updated' ? 'Task updated' : 'Task created');
    void this.router.navigate(['/tasks']);
  }

  protected remove(): void {
    if (!confirm('Delete "' + this.draft.name + '"? This cannot be undone.')) return;
    this.store.remove(this.draft.id);
    this.toast.show('Task deleted');
    void this.router.navigate(['/tasks']);
  }

  private fail(message: string): void {
    this.error.set(message);
    window.scrollTo(0, 0);
  }
}

function newId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  const b = new Uint8Array(16);
  crypto.getRandomValues(b);
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const hex = [...b].map((x) => (x + 0x100).toString(16).slice(1)).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
