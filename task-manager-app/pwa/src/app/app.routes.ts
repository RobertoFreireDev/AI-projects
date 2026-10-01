import { Routes } from '@angular/router';

/** Three pages (CLAUDE.md §7), plus Home's drill-down into one section
    scoped to one icon. Every page is lazily loaded, so the initial bundle
    only carries the shell. */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  {
    path: 'home',
    loadComponent: () => import('./pages/home').then((m) => m.Home),
  },
  {
    // :section is todo | pending | done, :icon is a task icon key
    path: 'home/:section/:icon',
    loadComponent: () => import('./pages/home-list').then((m) => m.HomeList),
  },
  {
    path: 'tasks',
    loadComponent: () => import('./pages/tasks').then((m) => m.Tasks),
  },
  {
    // 'new' creates, any other value edits that task id
    path: 'tasks/:id',
    loadComponent: () => import('./pages/task-editor').then((m) => m.TaskEditor),
  },
  {
    path: 'settings',
    loadComponent: () => import('./pages/settings').then((m) => m.Settings),
  },
  { path: '**', redirectTo: 'home' },
];
