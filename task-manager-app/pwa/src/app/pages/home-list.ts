import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Chrome } from '../core/chrome';
import { fmtDate, relativeLate } from '../core/date';
import { iconKey, iconLabel } from '../core/icons';
import { SECTION_META, isSectionKey } from '../core/model';
import type { ChecklistNote, DueEntry, SectionKey, Task } from '../core/model';
import { TaskStore } from '../core/task-store';
import { Toast } from '../core/toast';
import { Icon } from '../ui/icon';

/** One Home section scoped to one icon: the task names, their notes and
    checklists, and a Mark as done action (CLAUDE.md §6.2/§6.3). */
@Component({
  selector: 'app-home-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon],
  templateUrl: './home-list.html',
})
export class HomeList {
  private readonly chrome = inject(Chrome);
  private readonly toast = inject(Toast);
  protected readonly store = inject(TaskStore);

  /** Bound straight from the route (`/home/:section/:icon`). */
  readonly section = input.required<string>();
  readonly icon = input.required<string>();

  protected readonly fmtDate = fmtDate;

  protected readonly key = computed<SectionKey>(() => {
    const s = this.section();
    return isSectionKey(s) ? s : 'todo';
  });

  protected readonly isDone = computed(() => this.key() === 'done');

  protected readonly entries = computed<DueEntry[]>(() =>
    this.store.section(this.key()).filter((e) => iconKey(e.task.icon) === this.icon()),
  );

  constructor() {
    effect(() => {
      this.chrome.set(SECTION_META[this.key()].title, iconLabel(this.icon()));
      this.chrome.back.set(['/home']);
    });
  }

  protected pill(entry: DueEntry): { cls: string; text: string } {
    if (this.key() === 'pending') {
      return {
        cls: 'pill pending',
        text: relativeLate(entry.due, this.store.today()) || 'Overdue',
      };
    }
    if (this.isDone()) return { cls: 'pill done', text: 'Done today' };
    return { cls: 'pill todo', text: 'Due today' };
  }

  protected checklistOf(note: unknown): ChecklistNote {
    return note as ChecklistNote;
  }

  protected onToggle(entry: DueEntry, note: ChecklistNote, index: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    /* Ticking every item auto-completes the task, the same way the button
       does — both paths stay in sync (CLAUDE.md §6.3). */
    const completed = this.store.toggleItem(entry.task, note, index, checked);
    if (completed) this.toast.show('Completed: ' + entry.task.name);
  }

  protected markDone(entry: DueEntry): void {
    this.store.markDone(entry.task, entry.due);
    this.toast.show('Completed: ' + entry.task.name);
  }

  protected undo(entry: DueEntry): void {
    this.store.undoDone(entry.task, entry.due);
    this.toast.show('Undone: ' + entry.task.name);
  }

  protected trackTask(_: number, entry: DueEntry): string {
    return entry.task.id;
  }

  protected taskIcon(task: Task): string {
    return iconKey(task.icon);
  }
}
