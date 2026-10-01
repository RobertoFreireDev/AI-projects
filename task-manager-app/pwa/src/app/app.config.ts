import { ApplicationConfig, isDevMode, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withHashLocation } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      /* Hash URLs (#/tasks) so the built app needs no SPA rewrite rule from
         whatever static server happens to be hosting it, and still works if
         the folder is opened from a subpath. That matters here: §13.6's
         "any static file server" is the deployment story. */
      withHashLocation(),
      // Route params arrive as component inputs (see HomeList / TaskEditor).
      withComponentInputBinding(),
    ),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      /* The default waits for the app to go stable; this app is small and
         the whole point is that the shell is cached before the user walks
         away from the machine that served it (CLAUDE.md §13.4). */
      registrationStrategy: 'registerImmediately',
    }),
  ],
};
