import { Injectable, signal } from '@angular/core';

export interface ChromeAction {
  icon: string;
  label: string;
  run: () => void;
}

/** The bits of the app shell a page owns: its title, an optional back
    target, and at most one top-right action. Reset on every navigation so a
    page only has to declare what it actually uses. */
@Injectable({ providedIn: 'root' })
export class Chrome {
  readonly title = signal('Home');
  readonly subtitle = signal<string | null>(null);
  /** Router commands for the back button, or null to hide it. */
  readonly back = signal<unknown[] | null>(null);
  readonly action = signal<ChromeAction | null>(null);

  reset(): void {
    this.subtitle.set(null);
    this.back.set(null);
    this.action.set(null);
  }

  set(title: string, subtitle: string | null = null): void {
    this.title.set(title);
    this.subtitle.set(subtitle);
  }
}
