import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import type { ElementRef } from '@angular/core';
import { Router } from '@angular/router';
import { Chrome } from '../core/chrome';
import { fmtDate, todayStr } from '../core/date';
import type { AppData } from '../core/model';
import { ShellStatus } from '../core/shell-status';
import { TaskStore } from '../core/task-store';
import { Toast } from '../core/toast';
import { validateData } from '../core/validation';
import { Icon } from '../ui/icon';

interface PendingImport {
  data: AppData;
  count: number;
  name: string;
}

/** Settings — manual JSON backup/restore, plus the offline-install status
    card that proves the shell really is cached (CLAUDE.md §7.3, §14). */
@Component({
  selector: 'app-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './settings.html',
})
export class Settings {
  private readonly chrome = inject(Chrome);
  private readonly router = inject(Router);
  private readonly toast = inject(Toast);
  protected readonly store = inject(TaskStore);
  protected readonly shell = inject(ShellStatus);

  private readonly filePicker = viewChild.required<ElementRef<HTMLInputElement>>('filePicker');

  protected readonly pending = signal<PendingImport | null>(null);
  protected readonly importError = signal<string | null>(null);
  protected readonly fmtDate = fmtDate;

  constructor() {
    this.chrome.set('Settings');
  }

  /* ---------- export (CLAUDE.md §7.3) ---------- */

  protected export(): void {
    const payload = JSON.stringify(this.store.snapshot(), null, 2);
    const blob = new Blob([payload], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'ptm-backup-' + todayStr() + '.json';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
    this.toast.show('Backup downloaded');
  }

  /* ---------- import ---------- */

  protected chooseFile(): void {
    this.filePicker().nativeElement.click();
  }

  protected onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // let the same file be picked again after a cancel
    if (!file) return;

    this.pending.set(null);
    this.importError.set(null);

    const reader = new FileReader();
    reader.onerror = () => this.importError.set('Could not read that file.');
    reader.onload = () => {
      let parsed: unknown;
      try {
        parsed = JSON.parse(String(reader.result));
      } catch {
        this.importError.set('That file is not valid JSON.');
        return;
      }
      /* Strict validation before anything touches localStorage or the DOM
         (CLAUDE.md §9). A bad file leaves existing data untouched. */
      const result = validateData(parsed);
      if (!result.ok) {
        this.importError.set('Rejected — ' + result.error);
        return;
      }
      this.pending.set({ data: result.data, count: result.data.tasks.length, name: file.name });
    };
    reader.readAsText(file);
  }

  protected cancelImport(): void {
    this.pending.set(null);
    this.importError.set(null);
  }

  protected merge(): void {
    const p = this.pending();
    if (!p) return;
    this.store.merge(p.data);
    this.finishImport('Data merged');
  }

  /** The one bulk operation that can remove tasks — deliberately separate
      from the per-task delete on the Tasks page (CLAUDE.md §10.8). */
  protected replaceAll(): void {
    const p = this.pending();
    if (!p) return;
    const local = this.store.taskCount();
    if (!confirm(`Replace all ${local} local task(s) with ${p.count} imported one(s)?`)) return;
    this.store.replaceAll(p.data);
    this.finishImport('Data replaced');
  }

  private finishImport(message: string): void {
    this.pending.set(null);
    this.importError.set(null);
    this.toast.show(message);
    void this.router.navigate(['/home']);
  }
}
