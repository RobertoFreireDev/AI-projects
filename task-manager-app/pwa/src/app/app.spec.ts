/* End-to-end-ish smoke tests: boot the real app through the real router and
   check each page actually renders what CLAUDE.md §6/§7 describes. */

import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { SwUpdate } from '@angular/service-worker';
import { App } from './app';
import { routes } from './app.routes';
import { STORAGE_KEY } from './core/model';
import type { AppData } from './core/model';
import { TaskStore } from './core/task-store';

/** One task due today, one overdue, one already done today. */
function seed(): void {
  const today = new Date();
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  const ref = iso(new Date(today.getFullYear(), today.getMonth(), today.getDate()));
  const past = iso(new Date(today.getFullYear(), today.getMonth(), today.getDate() - 9));

  const data: AppData = {
    version: 1,
    settings: {},
    tasks: [
      {
        id: 'gym-1',
        icon: 'gym',
        name: 'Treino de peito',
        notes: [{ type: 'checklist', items: [{ text: 'supino', checked: false }] }],
        recurrence: { type: 'daily', interval: 1, startDate: ref },
        createdAt: ref,
        completions: [],
        checklistState: { lastResetDueDate: null, checkedItemIds: [] },
        active: true,
      },
      {
        id: 'bill-1',
        icon: 'money',
        name: 'Pagar cartão Inter',
        notes: [{ type: 'text', text: 'vence dia 10' }],
        recurrence: { type: 'once', date: past },
        createdAt: past,
        completions: [],
        checklistState: { lastResetDueDate: null, checkedItemIds: [] },
        active: true,
      },
    ],
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

beforeEach(() => {
  localStorage.clear();
  seed();
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes, withComponentInputBinding()),
      // The real SwUpdate needs a registered worker; jsdom has none.
      {
        provide: SwUpdate,
        useValue: { isEnabled: false, versionUpdates: { subscribe: () => {} } },
      },
    ],
  });
});

/** Navigate the real router to `url` and hand back the rendered page. */
async function render(url: string): Promise<HTMLElement> {
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  harness.detectChanges();
  return harness.routeNativeElement!;
}

describe('app shell (§8)', () => {
  it('renders a topbar title and a three-tab bottom bar', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/home');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.topbar h1')?.textContent).toContain('Home');
    const tabs = [...host.querySelectorAll<HTMLAnchorElement>('.tabbar a')];
    expect(tabs.map((a) => a.textContent?.trim())).toEqual(['Home', 'Tasks', 'Settings']);
    expect(tabs[0].getAttribute('aria-current')).toBe('page');
  });
});

describe('Home (§6)', () => {
  it('shows a tile per icon in each section', async () => {
    const page = await render('/home');
    const text = page.textContent ?? '';
    expect(text).toContain('To do');
    expect(text).toContain('Pending');
    expect(text).toContain('Done');
    // One gym tile due today, one money tile overdue.
    expect(page.querySelectorAll('.tile.todo')).toHaveLength(1);
    expect(page.querySelectorAll('.tile.pending')).toHaveLength(1);
    expect(page.querySelector('.tile.todo')?.textContent).toContain('Gym');
  });

  it('drills down into one section scoped to one icon, and completes a task', async () => {
    const page = await render('/home/todo/gym');
    expect(page.textContent).toContain('Treino de peito');
    expect(page.textContent).toContain('supino');

    const doneButton = page.querySelector<HTMLButtonElement>('.btn.done');
    expect(doneButton?.textContent).toContain('Mark as done');
    doneButton!.click();

    const store = TestBed.inject(TaskStore);
    expect(store.buckets().done.map((e) => e.task.id)).toEqual(['gym-1']);
    expect(store.buckets().todo).toHaveLength(0);
  });

  it('offers no delete action on the section grid (§6.2)', async () => {
    const home = await render('/home');
    expect(home.textContent?.toLowerCase()).not.toContain('delete');
  });

  it('offers no delete action in a drilled-down list either (§6.2)', async () => {
    const list = await render('/home/pending/money');
    expect(list.textContent).toContain('Pagar cartão Inter');
    expect(list.textContent?.toLowerCase()).not.toContain('delete');
  });
});

describe('Tasks (§7.2)', () => {
  it('lists every task with the state that explains where it is', async () => {
    const page = await render('/tasks');
    const text = page.textContent ?? '';
    expect(text).toContain('Treino de peito');
    expect(text).toContain('Pagar cartão Inter');
    expect(text).toContain('Every day');
    expect(text).toContain('Due today');
    expect(text).toContain('9 days late');
  });

  it('opens the editor prefilled for an existing task', async () => {
    const page = await render('/tasks/bill-1');
    const name = page.querySelector<HTMLInputElement>('input[type="text"]');
    expect(name?.value).toBe('Pagar cartão Inter');
    expect(page.querySelector('.picker')).toBeTruthy();
    expect(page.textContent).toContain('Delete task');
  });

  it('starts the editor empty for a new task', async () => {
    const page = await render('/tasks/new');
    expect(page.querySelector<HTMLInputElement>('input[type="text"]')?.value).toBe('');
    expect(page.textContent).not.toContain('Delete task');
  });
});

describe('Settings (§7.3)', () => {
  it('offers export and import, and reports the offline install state', async () => {
    const page = await render('/settings');
    const text = page.textContent ?? '';
    expect(text).toContain('Export JSON');
    expect(text).toContain('Choose JSON file');
    expect(text).toContain('Offline install');
    expect(page.querySelector('input[type="file"]')).toBeTruthy();
  });
});
