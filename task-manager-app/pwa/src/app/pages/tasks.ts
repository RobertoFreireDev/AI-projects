import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Chrome } from '../core/chrome';
import { fmtDate } from '../core/date';
import { iconKey, iconLabel } from '../core/icons';
import type { Task } from '../core/model';
import { recurrenceLabel } from '../core/recurrence';
import { TaskStore, groupByIcon } from '../core/task-store';
import type { TaskStatus } from '../core/task-store';
import { Toast } from '../core/toast';
import { Icon } from '../ui/icon';

/** Tasks — every task, whatever its due date, with the state that explains
    why it is or isn't on Home (CLAUDE.md §7.2). The only page that can
    delete a task, and only behind a confirmation (§10.8). */
@Component({
  selector: 'app-tasks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './tasks.html',
})
export class Tasks {
  private readonly chrome = inject(Chrome);
  private readonly router = inject(Router);
  private readonly toast = inject(Toast);
  protected readonly store = inject(TaskStore);

  protected readonly search = signal('');
  protected readonly fmtDate = fmtDate;
  protected readonly iconLabel = iconLabel;
  protected readonly recurrenceLabel = recurrenceLabel;

  protected readonly groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const matched = this.store
      .tasks()
      .filter((t) => !q || t.name.toLowerCase().includes(q))
      .map((task) => ({ task }));
    return groupByIcon(matched);
  });

  constructor() {
    effect(() => {
      const n = this.store.taskCount();
      this.chrome.set('Tasks', n === 1 ? '1 task' : n + ' tasks');
    });
    this.chrome.action.set({
      icon: 'ui_plus',
      label: 'New task',
      run: () => void this.router.navigate(['/tasks', 'new']),
    });
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected status(task: Task): TaskStatus {
    return this.store.status(task);
  }

  protected pillClass(status: TaskStatus): string {
    return status.key === 'todo' || status.key === 'pending' || status.key === 'done'
      ? 'pill ' + status.key
      : 'pill';
  }

  protected taskIcon(task: Task): string {
    return iconKey(task.icon);
  }

  protected edit(task: Task): void {
    void this.router.navigate(['/tasks', task.id]);
  }

  protected remove(task: Task): void {
    if (!confirm('Delete "' + task.name + '"? This cannot be undone.')) return;
    this.store.remove(task.id);
    this.toast.show('Task deleted');
  }

  /** Only offered for a cycle already satisfied by a completion entry
      (CLAUDE.md §7.2). It removes that entry and nothing else. */
  protected reactivate(task: Task, due: string): void {
    this.store.reactivate(task, due);
    this.toast.show('Reactivated: ' + task.name);
  }
}
