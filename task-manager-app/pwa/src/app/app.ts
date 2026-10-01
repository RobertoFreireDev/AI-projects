import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  NavigationEnd,
  NavigationStart,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { Chrome } from './core/chrome';
import { TaskStore } from './core/task-store';
import { Toast } from './core/toast';
import { Icon } from './ui/icon';

/** The app shell: sticky topbar, routed page, bottom tab bar (CLAUDE.md §8). */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);
  protected readonly chrome = inject(Chrome);
  protected readonly toast = inject(Toast);
  protected readonly store = inject(TaskStore);

  protected readonly tabs = [
    { path: '/home', label: 'Home', icon: 'ui_home' },
    { path: '/tasks', label: 'Tasks', icon: 'ui_list' },
    { path: '/settings', label: 'Settings', icon: 'ui_settings' },
  ];

  constructor() {
    this.router.events.subscribe((e) => {
      /* Each page declares its own title/back/action on construction, so
         wipe the previous page's before the next one is created. */
      if (e instanceof NavigationStart) this.chrome.reset();
      if (e instanceof NavigationEnd) window.scrollTo(0, 0);
    });
  }

  protected goBack(): void {
    const target = this.chrome.back();
    if (target) void this.router.navigate(target);
  }
}
