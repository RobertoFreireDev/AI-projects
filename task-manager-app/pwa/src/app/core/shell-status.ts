/* ============================================================
   Offline-install status (CLAUDE.md §14, "Verifying any of these paths").

   The failure mode this exists for is silent: on a non-secure origin
   `navigator.serviceWorker` is simply undefined, so nothing registers,
   nothing caches, and the app looks fine right up until the local server
   stops. A home-screen icon proves nothing — this card does.
   ============================================================ */

import { Injectable, inject, isDevMode, signal } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';

export type ShellState =
  | 'checking'
  | 'ready' // shell is in the cache; the server can go away
  | 'insecure' // not https/localhost, so the browser withholds service workers
  | 'dev' // ng serve — the service worker is deliberately off
  | 'file' // opened straight from disk
  | 'failed';

@Injectable({ providedIn: 'root' })
export class ShellStatus {
  private readonly sw = inject(SwUpdate);

  readonly state = signal<ShellState>('checking');
  readonly cached = signal(0);
  readonly detail = signal('');
  /** A newer build finished downloading and is waiting for a reload. */
  readonly updateReady = signal(false);

  readonly origin = typeof location !== 'undefined' ? location.origin : '';

  /* Plain http, off localhost. If the shell still caches on such an origin,
     Chrome was told to trust it (chrome://flags) — which buys the service
     worker but not a real install: Chrome keeps a URL bar on top of an app
     installed from an http origin, and often degrades the install to a
     bookmark shortcut. Only genuine https removes it (tools/make-cert.js). */
  readonly insecureScheme =
    typeof location !== 'undefined' &&
    location.protocol === 'http:' &&
    !['localhost', '127.0.0.1'].includes(location.hostname);

  constructor() {
    if (typeof window === 'undefined') return;

    if (location.protocol === 'file:') {
      this.state.set('file');
      return;
    }
    /* `serviceWorker` is absent precisely when the origin is not secure —
       which is the LAN-IP case this app kept tripping over. */
    if (!('serviceWorker' in navigator)) {
      this.state.set('insecure');
      return;
    }
    if (isDevMode() || !this.sw.isEnabled) {
      this.state.set('dev');
      return;
    }

    this.sw.versionUpdates.subscribe((e) => {
      if (e.type === 'VERSION_READY') this.updateReady.set(true);
    });

    /* Registered is not the same as cached — wait for a worker that has
       actually finished installing before claiming offline readiness. */
    let registered = false;
    navigator.serviceWorker.ready.then(
      () => {
        registered = true;
        void this.countCache();
      },
      (err: unknown) => this.fail(err),
    );

    /* Caching a shell this small is quick, but it is still real I/O over a
       real connection. Only call it a failure once there has been ample
       time, and say which half of the job did not finish. */
    setTimeout(() => {
      if (this.state() !== 'checking') return;
      this.fail(
        new Error(
          registered
            ? 'The worker registered but has not cached anything.'
            : 'Registration did not complete.',
        ),
      );
    }, 45000);
  }

  applyUpdate(): void {
    void this.sw.activateUpdate().then(() => location.reload());
  }

  private fail(err: unknown): void {
    this.state.set('failed');
    this.detail.set(err instanceof Error ? err.message : String(err ?? ''));
  }

  /** Count what the Angular service worker actually holds. Its caches are
      named `ngsw:<base>:<hash>:assets:<group>:cache`. */
  private async countCache(): Promise<void> {
    if (!('caches' in window)) {
      this.fail(new Error('The Cache Storage API is unavailable here.'));
      return;
    }
    try {
      const names = (await caches.keys()).filter(
        (n) => n.startsWith('ngsw:') && n.includes(':assets:') && n.endsWith(':cache'),
      );
      let total = 0;
      for (const name of names) {
        const cache = await caches.open(name);
        total += (await cache.keys()).length;
      }
      this.cached.set(total);
      this.state.set(total > 0 ? 'ready' : 'checking');
    } catch (err) {
      this.fail(err);
    }
  }
}
