import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Chrome } from '../core/chrome';
import { fmtDate } from '../core/date';
import { iconLabel } from '../core/icons';
import { SECTION_META } from '../core/model';
import type { DueEntry, SectionKey } from '../core/model';
import { TaskStore, groupByIcon } from '../core/task-store';
import { Icon } from '../ui/icon';

/** Home — To do / Pending / Done for today, as grids of icon tiles
    (CLAUDE.md §6.1, §6.2). No delete action exists here, ever (§6.2). */
@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon],
  templateUrl: './home.html',
})
export class Home {
  private readonly chrome = inject(Chrome);
  protected readonly store = inject(TaskStore);

  protected readonly sections: readonly SectionKey[] = ['todo', 'pending', 'done'];
  protected readonly meta = SECTION_META;
  protected readonly iconLabel = iconLabel;

  protected readonly groups = computed(() => {
    const b = this.store.buckets();
    return this.sections.map((key) => ({
      key,
      title: SECTION_META[key].title,
      empty: SECTION_META[key].empty,
      count: b[key].length,
      tiles: groupByIcon<DueEntry>(b[key]),
    }));
  });

  protected readonly isEmpty = computed(() => this.groups().every((g) => g.count === 0));

  constructor() {
    // An effect, not a one-off call, so a day rollover retitles the page.
    effect(() => this.chrome.set('Home', fmtDate(this.store.today())));
  }

  protected countLabel(n: number): string {
    return n === 1 ? '1 task' : n + ' tasks';
  }
}
