import { Injectable, signal } from '@angular/core';

/** Transient one-line confirmations, rendered once by the app shell. */
@Injectable({ providedIn: 'root' })
export class Toast {
  readonly message = signal<string | null>(null);
  private timer: ReturnType<typeof setTimeout> | null = null;

  show(message: string): void {
    this.message.set(message);
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => this.message.set(null), 2600);
  }
}
