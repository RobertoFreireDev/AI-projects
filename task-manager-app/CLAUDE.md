# CLAUDE.md — Personal Task Manager (two build targets, offline-first)

This file is the build specification for a personal task manager. Follow it precisely — where the original request was ambiguous, a decision has been made explicitly below (see **Assumptions & Design Decisions**). Do not silently deviate from these decisions; if you must, flag it.

## 1. Project Summary

An offline-first, dependency-free personal task manager for recurring and one-time tasks (bills, chores, pet care, gym, etc.), grouped visually by icon, with a Home page that separates tasks into **To do / Pending / Done** for *today*.

There are **two build targets that must behave identically** — same data model, same recurrence engine, same UI, same JSON backup format — differing only in packaging:

- **Solution A — single HTML file.** One `.html` file, opened directly (even via `file://`), zero setup. Fully specified in §2–§12.
- **Solution B — installable Angular PWA, no public hosting.** The same app, built with Angular and packaged so it can be installed to a phone's home screen and launched full-screen like a native app, run only from `localhost` — no domain, no paid hosting, no backend server to maintain. Specified in §13–§15. It has a build step and npm dependencies; §2's zero-dependency rule constrains Solution A only.

## 2. Hard Constraints (Solution A)

- **Single file**: one `.html` file containing all HTML, `<style>`, and `<script>`. No separate `.css`/`.js` files, no build step, no bundler.
- **Zero dependencies**: no CDN links, no npm packages, no web fonts, no external requests of any kind. Vanilla JS only.
- **Works fully offline**: must run correctly opened directly from disk (`file://`) or from any static host, with no network calls.
- **Data persistence**: `localStorage` only.
- **Backup**: manual JSON export (download) and import (file picker), no auto-sync.
- **Mobile-first**: design and layout for a narrow phone viewport first; larger screens are a progressive enhancement.
- **Dark mode only**: no light theme, no theme toggle.
- **Secure by default**: see §9.

## 3. Data Model

Store everything under a single namespaced `localStorage` key: `ptm_data`.

```json
{
  "version": 1,
  "settings": {},
  "tasks": [ /* Task objects, see below */ ]
}
```

### 3.1 Task object

```json
{
  "id": "string (crypto.randomUUID())",
  "icon": "string (see icon set, §5)",
  "name": "string",
  "notes": [ /* Note objects */ ],
  "recurrence": { /* Recurrence object, see §4 */ },
  "createdAt": "YYYY-MM-DD",
  "completions": [
    { "dueDate": "YYYY-MM-DD", "completedOn": "YYYY-MM-DD" }
  ],
  "checklistState": {
    "lastResetDueDate": "YYYY-MM-DD or null",
    "checkedItemIds": ["noteId:itemIndex", "..."]
  },
  "active": true
}
```

- `icon`, `name`, `notes` (array, may be empty), and `recurrence` are **mandatory** on every task — enforce this in the Tasks CRUD form.
- `completions` is the full history of satisfied due-dates. Never delete entries from it on "undo" — see §6.4.

### 3.2 Note object

Two kinds. Use `checklist` whenever the task represents a list of sub-items (per the user's own rule: *"sempre que tiver uma lista de coisas, crie um checklist"*); use `text` for a single freeform note.

```json
{ "type": "text", "text": "string" }
```

```json
{
  "type": "checklist",
  "items": [
    { "text": "string", "checked": false }
  ]
}
```

Checklist item `checked` state is **per-cycle**, not permanent — see §6.3 for the reset rule.

## 4. Recurrence Model

```json
{
  "type": "once | daily | weekly | monthly | yearly",
  "interval": 1,
  "startDate": "YYYY-MM-DD",

  "daysOfWeek": [2, 4, 6],                  // weekly only, optional — see 4.0

  "mode": "dayOfMonth | weekdayOfMonth",   // monthly only
  "dayOfMonth": 1,                          // monthly, mode=dayOfMonth
  "weekdayOrdinal": "1 | 2 | 3 | 4 | last",// monthly, mode=weekdayOfMonth
  "weekday": 0,                             // 0=Sunday..6=Saturday

  "month": 1,                               // yearly
  "day": 1,                                 // yearly

  "date": "YYYY-MM-DD"                      // once
}
```

| type | meaning |
|---|---|
| `once` | Single occurrence on `date`. |
| `daily` | Every `interval` days, counted from `startDate`. |
| `weekly` | Every `interval` weeks, counted from `startDate`. If `daysOfWeek` is set, the task is due on **each** of those weekdays within every active week, instead of just the single weekday implied by `startDate` — see §4.0. |
| `monthly`, `mode=dayOfMonth` | Every `interval` months, on `dayOfMonth`. If a month is shorter than `dayOfMonth`, clamp to that month's last day (e.g. `31` in February → last day of February). |
| `monthly`, `mode=weekdayOfMonth` | Every `interval` months, on the Nth `weekday` (e.g. "2nd Tuesday"), or `last` for the last occurrence of that weekday in the month. |
| `yearly` | Every `interval` years, on `month`/`day`. Feb 29 on a non-leap year clamps to Feb 28. |

`startDate` defaults to `createdAt` if not set explicitly by the user, and anchors the interval math for `daily`/`weekly`/`monthly`/`yearly`.

### 4.0 Multiple weekdays on one task (`weekly` + `daysOfWeek`)

A single task can recur on more than one day of the week — e.g. "tirar o lixo" on Tuesday, Thursday, and Saturday — without being split into separate tasks. This only applies to `type: "weekly"`:

- `daysOfWeek` is an **optional** array of weekday numbers (`0`=Sunday .. `6`=Saturday). Omit it entirely for the old single-weekday-from-`startDate` behavior (backward compatible with every existing task in this file and in `tarefas-exemplo.json`).
- When present, the task is due on every date matching one of those weekdays, inside every "active" week.
- With `interval: 1`, every week is active — so "toda terça, quinta e sábado" is simply `daysOfWeek: [2, 4, 6]`, `interval: 1`.
- With `interval > 1` (e.g. "every 2 weeks, on Mon/Wed"), weeks are counted from the calendar week containing `startDate`; a week is active when `weekIndex % interval == 0`, where `weekIndex` is the number of full weeks between `startDate`'s week and the candidate week.
- This still produces **one `lastDueDate` per task**, same as every other recurrence type — completing it marks that one due-date done, per §6.1/§6.3. It does not create three independent sub-schedules; it's one schedule with three trigger days.
- Only use this when the task content is genuinely the same regardless of which day it falls on (like taking out the trash). If each day has different content (like the gym routine in `tarefas-exemplo.json`), keep those as separate tasks — `daysOfWeek` intentionally doesn't carry per-day notes.

### 4.1 `lastDueDate(task, today)` — core algorithm

Compute the most recent scheduled occurrence that is `<= today`:

- **once**: `date` if `date <= today`, else `null`.
- **daily**: `daysSince = today - startDate` (in days). If `daysSince < 0`, `null`. Else `startDate + floor(daysSince / interval) * interval` days.
- **weekly, no `daysOfWeek`**: same as daily but in units of `7 * interval` days (single implicit weekday, taken from `startDate`).
- **weekly, with `daysOfWeek`**: find `weekStart(d)` = the Sunday on/before `d`. Let `anchorWeek = weekStart(startDate)`. For a candidate date `d <= today`, it's a valid occurrence only if `d`'s weekday is in `daysOfWeek` **and** `((weekStart(d) - anchorWeek) / 7 days) % interval == 0`. Return the most recent such `d`.
- **monthly / dayOfMonth**: walk backward in steps of `interval` months from the month containing `today` (or forward from `startDate`, whichever is simpler to implement) until you find the closest month whose clamped `dayOfMonth` date is `<= today`.
- **monthly / weekdayOfMonth**: same idea, but the target date within each candidate month is "the Nth `weekday`" (or "last `weekday`") of that month.
- **yearly**: same pattern, in units of `interval` years.

Use **local calendar dates only** (`YYYY-MM-DD` strings), never `Date` objects with time-of-day math, to avoid timezone/DST off-by-one bugs.

## 5. Icon Set

All icons must be **inline SVG**, embedded directly in the HTML/JS (no external file, no icon font, no CDN). Recommended source: **Lucide** icons (ISC license — free, permissive, no attribution required). Copy only the `<path>` data you need for the icons below and store them as an object of SVG strings in JS; render with `currentColor` so they inherit theme color.

| App icon key | Suggested Lucide icon | Used for |
|---|---|---|
| `cat` | `cat` | Pets |
| `house` | `home` | Home / condo |
| `work` | `briefcase` | Work-related |
| `gym` | `dumbbell` | Exercise |
| `nature` | `trees` | Outdoors |
| `food` | `utensils` | Food |
| `shop` | `shopping-cart` | Shopping |
| `clean` | `sparkles` | Cleaning |
| `task` | `check-square` | Generic task (fallback) |
| `document` | `file-text` | Paperwork / accounting |
| `money` | `wallet` | Bills / payments |
| `battery` | `battery-charging` | Electronics / charging |

`money` and `battery` were added beyond the user's base list to cover the example data (bill payments, recharging devices) — the list is explicitly open-ended ("etc."), so extend it the same way if new task categories appear later.

If a task's `icon` value doesn't match any known key, fall back to `task`.

## 6. Home Page — To do / Pending / Done

### 6.1 Section definitions (evaluated for "today")

For every active task, compute `due = lastDueDate(task, today)` and check `completions` for an entry where `dueDate === due`:

- **No entry found:**
  - `due === today` → **To do**
  - `due < today` → **Pending**
  - `due === null` or `due > today` (not started yet) → not shown on Home at all
- **Entry found** (`completions` has `{dueDate: due, completedOn}`):
  - `completedOn === today` → **Done**
  - `completedOn !== today` → not shown on Home (already satisfied in a past cycle, dormant until the next due date)

This means completing a task **late** still permanently satisfies that cycle — it will not reappear as Pending the next day, and it only shows in Done on the day it was actually clicked, per the user's own example.

**Confirmed: this is a visibility state, not deletion.** A task that becomes dormant (including a `once`-type task after its single occurrence is completed) is never removed from data — it simply stops matching any Home section. It always remains listed on the Tasks page, where it can be edited, deleted (explicitly, by the user), or reactivated — see §7.2.

### 6.2 Layout per section

- Group each section's tasks by `icon`.
- Render a grid of icon tiles — one tile per icon present in that section, tile shows the icon plus a small badge in the bottom-right corner with the count of tasks under it. Hide a section entirely (or show an empty state) if it has zero tasks.
- Tapping a tile opens a list view scoped to (section, icon): task name, notes/checklist, and a **Mark as done** action (To do/Pending only). Include a "Back to Home" button.
- No delete action anywhere on the Home page, ever — not even implicitly. A task leaving a Home section (e.g. going dormant after completion) is never deleted, only hidden from that view. Management (edit/delete/reactivate) only happens on the Tasks page — see §7.2.

### 6.3 Completing a task

- Clicking **Mark as done**: record `{ dueDate: due, completedOn: today }` in `completions`, and mark all checklist items (if any) as checked.
- Manually checking every checklist item also auto-completes the task the same way (keep both paths in sync).
- Recommended (not mandatory): allow **undo** on a task shown in Done today — this only removes today's specific `completions` entry, nothing else.

### 6.4 Checklist reset per cycle

Checklist `checked` state must reset when a new cycle starts, not stay checked forever:

- Compare `task.checklistState.lastResetDueDate` to the current `due = lastDueDate(task, today)`.
- If different, uncheck every checklist item and set `lastResetDueDate = due`.
- Do this on app load and whenever the day changes while the app is open.

## 7. Pages

### 7.1 Home
As specified in §6.

### 7.2 Tasks (full CRUD)
- List all tasks (regardless of due date), grouped or searchable as makes sense. Visually indicate a task's current state (e.g. "dormant until DD/MM", "pending", "done today") so the user can tell why it's not showing on Home.
- Create/edit form: icon picker (grid of the icon set), name, notes (add/remove text notes and checklists, add/remove checklist items), recurrence builder (type, interval, and the type-specific fields from §4).
- **Reactivate**: for any dormant task (its current `due = lastDueDate(task, today)` already has a matching `completions` entry), show a **Reactivate** action that removes that specific `completions` entry. This makes the task match §6.1's "no entry found" case again, so it reappears on Home as To do or Pending. This is the **only** way to bring a completed `once`-type task back into the flow, since it has no future natural occurrence — for recurring tasks it's a manual "undo" beyond the same-day window covered in §6.3.
- Delete task (explicit, user-initiated, with confirmation). This is the only way a task is ever removed — never automatic, never from the Home page.

### 7.3 Settings
- **Export**: build the full `ptm_data` object, `JSON.stringify`, trigger a file download (`Blob` + `URL.createObjectURL` + `<a download>`).
- **Import**: file picker → read as text → `JSON.parse` in a `try/catch` → validate strictly against the schema in §3 (reject on any structural mismatch, show a clear error) → ask the user **merge** or **replace** → write to `localStorage`, refresh UI.

## 8. UI Style

- Dark mode only — define a small palette via CSS custom properties (background, surface, text, muted text, one accent color, a "done" green, a "pending" warning color).
- System font stack only (`-apple-system, "Segoe UI", Roboto, sans-serif`) — no web fonts.
- Mobile-first: base CSS targets a narrow phone viewport; use `min-width` media queries to enhance for tablet/desktop.
- Bottom tab bar navigation for the 3 pages (Home / Tasks / Settings), thumb-reachable, minimum 44px tap targets.
- Simple, modern visual language: rounded corners, soft shadows/elevation, generous spacing, one accent color used sparingly. Avoid visual clutter.
- Include `<meta name="viewport" content="width=device-width, initial-scale=1">`.

## 9. Security Requirements

- Strict **Content-Security-Policy** meta tag disallowing any network access, e.g. `default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: 'self';` — the app must never make a network request.
- Never build DOM from user-supplied strings with `innerHTML`. Use `textContent` / `createElement` for all task names, notes, and checklist text. `innerHTML` is only acceptable for the app's own static, hard-coded SVG icon strings.
- No `eval`, no `Function()`, no dynamic script injection.
- Validate imported JSON strictly (types, required fields, reasonable length limits on strings, reasonable array sizes) before it ever touches `localStorage` or the DOM.
- No analytics, no trackers, no third-party calls of any kind.
- Namespace the `localStorage` key (`ptm_data`) to avoid collisions if the file is ever hosted alongside other tools.

## 10. Assumptions & Design Decisions

These fill gaps in the original request. Revisit them if they don't match intent:

1. **Mixed-frequency checklists** ("Cuidar dos Gatos" mixed 2-day / daily / weekly items) are split into **separate tasks**, one per distinct frequency, since recurrence is modeled at the task level, not the note-item level. See the example import file for how this was applied.
2. **Completion mechanism**: checking all checklist items auto-completes the task; a "Mark as done" button also exists and checks everything at once. A task without a checklist is completed only via the button.
3. **Pending is tied to the due date, not the calendar day it's viewed on** — see §6.1/6.3. This avoids a completed-late task incorrectly reappearing as pending.
4. **No per-day backlog**: the app tracks only the single most recent due date per task, not every individual missed occurrence. Marking done clears the current cycle only.
5. **Unspecified day-of-month** in the source examples (e.g. "todo mês" with no explicit day) defaults to **day 1**, consistent with the user's own "1° dia de cada 3 meses" example.
6. **Future-dated tasks** (recurrence not yet started, or a `once` task with a future date) are hidden from Home until their due date arrives, but remain visible/editable on the Tasks page.
7. `money` and `battery` were added to the icon set to cover bills and electronics from the example data.
8. **No task is ever deleted implicitly.** Home page has no delete action at all, and completing a task (including a `once`-type task) only changes its visibility state, never its existence. **Per-task deletion happens exclusively on the Tasks page**, explicitly, with confirmation. A completed `once`-type task can be brought back via **Reactivate** on the Tasks page (§7.2). The one exception is Settings → Import → **Replace** (§7.3), which is a full backup restore and can overwrite/remove tasks as a bulk operation — this is not a per-task delete action and should be clearly labeled as destructive/irreversible in the UI, distinct from the Tasks page delete.
9. **Multiple weekdays per task** (§4.0) is an optional `daysOfWeek` array added to `weekly` recurrence, backward compatible with tasks that don't set it. Use it only when the same task content applies on every trigger day (e.g. trash day); when content differs by day, keep separate tasks as done for the gym example in §11.

## 11. Example Data

A ready-to-import file, `tarefas-exemplo.json`, is provided separately with the user's real task list translated into this schema (content kept in Portuguese, not translated). Use it to validate the import flow and the recurrence engine end-to-end.

## 12. Edge Cases to Test

- `dayOfMonth: 31` landing on February, April, June, etc. (clamping).
- `weekdayOfMonth` with `ordinal: last` in months with 4 vs 5 occurrences of that weekday.
- Yearly task on Feb 29 in a non-leap year.
- Marking a heavily overdue task done (due date weeks in the past) — must move to Done today and not resurface as Pending.
- Import of a malformed/partial JSON file — must fail gracefully with a clear message and not corrupt existing data.
- Empty states: a section with zero tasks, a task with zero notes.
- Day rollover while the app is left open in the browser (checklist reset, section re-evaluation).

## 13. Solution B — Installable PWA (localhost only, no public hosting)

**Goal:** the exact same task manager, installable to a phone's home screen and launchable full-screen like a native app — without ever deploying to a public server, buying a domain, or paying for hosting.

### 13.1 What "no server / no host" means in practice

Browsers gate PWA installability behind a security check. Per MDN's official PWA installability guide: a PWA can only be installed when the page is served over `https`, or over `localhost`/`127.0.0.1` in a local dev setup — this is a stricter bar than a generic secure context, which otherwise treats `file://` pages as safe. So "no hosting" here means: no public domain, no paid server, no reverse proxy, no always-on backend — but *some* local process still has to answer HTTP requests on `localhost`/`127.0.0.1` for the browser to treat the origin as installable. That process can be a one-line command run from a terminal, and it does not need to keep running after the first install (see §13.4).

### 13.2 Framework and file structure

Solution B is an **Angular application** (Angular 22, standalone components, signals, zoneless change detection, hash-based routing). This is the one place the two targets diverge structurally: Solution A's "single file, zero dependencies, no build step" (§2) is a Solution A constraint and stays untouched there, while Solution B accepts npm dependencies and a build step in exchange for real routing, typed models, and a test suite over the recurrence engine.

What does *not* change: the built output is still fully self-contained and offline — no CDN, no web font, no third-party origin, no network call at runtime.

```
/pwa                        Angular workspace
  angular.json              build config
  ngsw-config.json          §13.4 — what the service worker prefetches
  public/                   copied verbatim into the output
    manifest.webmanifest    §13.3
    icon-192.png            §13.5
    icon-512.png
  src/
    index.html              CSP, viewport, manifest link, iOS meta tags
    styles.css              §8's stylesheet, global — same class vocabulary as Solution A
    app/
      app.ts/.html          shell: topbar + routed page + bottom tabs + toast
      app.routes.ts         §7's three pages, lazily loaded
      app.config.ts         router + service worker registration
      core/                 date.ts, recurrence.ts, model.ts, validation.ts,
                            task-store.ts, icons.ts, chrome.ts, toast.ts,
                            shell-status.ts  (+ .spec.ts alongside)
      pages/                home, home-list, tasks, task-editor, settings
      ui/icon.ts            icon rendering
  dist/tasks/browser/       build output — this is what gets served
```

The manifest, service worker and icons still exist as standalone files in the output, as the platform requires.

### 13.3 Web app manifest (`manifest.webmanifest`)

Linked from `index.html`: `<link rel="manifest" href="manifest.webmanifest">`. Per MDN, Chromium-based browsers (Chrome, Edge, Samsung Internet) require the manifest to include a `name` or `short_name`; an `icons` array with at least a 192px and a 512px icon; `start_url`; `display` and/or `display_override`; and `prefer_related_applications` set to `false` or left out entirely.

```json
{
  "name": "Task Manager",
  "short_name": "Tasks",
  "start_url": "./",
  "scope": "./",
  "display": "standalone",
  "background_color": "#0e1014",
  "theme_color": "#0e1014",
  "prefer_related_applications": false,
  "icons": [
    { "src": "icon-192.png", "type": "image/png", "sizes": "192x192", "purpose": "any maskable" },
    { "src": "icon-512.png", "type": "image/png", "sizes": "512x512", "purpose": "any maskable" }
  ]
}
```

Colors match the dark palette from §8. `display: "standalone"` removes browser UI so the installed app looks native. The glyph sits inside the maskable safe zone (§13.5), so one file serves both purposes.

### 13.4 Service worker (`@angular/service-worker`)

Not strictly required for the install prompt itself, but required for this app's own "works offline" requirement (§2) to hold true *after* installation, once the local server is no longer running.

Angular's `ngsw-worker.js` handles this, configured by `ngsw-config.json` and registered by `provideServiceWorker` in `app.config.ts`:

- **Prefetch the whole app shell on install** — `index.html`, the manifest, the stylesheet, *every* JS chunk (including the lazily-loaded route chunks, so all three pages work offline, not just the first one visited), and both icons.
- **Serve from cache first**, so once the shell is cached the installed app opens with the machine that served it switched off.
- **Versioning is automatic.** Angular derives the cache identity from a content hash of the built files, recorded in `ngsw.json`, and drops superseded caches itself. There is no hand-maintained cache constant to bump — and therefore no way to forget to bump it and leave installed copies pinned to a stale shell.
- Registration uses `registerImmediately` rather than waiting for app stability: the shell must be cached before the user walks away from the machine that served it.
- Task data itself is never cached by the service worker — it only ever lives in `localStorage`, per §2/§3, and is untouched by this caching layer.

### 13.5 Icons

Two flat PNG files, `icon-192.png` (192×192) and `icon-512.png` (512×512), generated once from the app's own glyph (a checkmark on the §8 dark background). No external icon-generator service or dependency — `tools/make-icons.js` produces them with pure Node as a one-time build step, writing into `pwa/public/` so the build copies them next to `index.html`. Never generated at runtime.

### 13.6 Building and running it locally to install (no public hosting)

```
cd pwa
npm install
npm run build          # -> pwa/dist/tasks/browser
cd ..
node tools/serve.js    # http://localhost:8000
```

Any static file server pointed at **`pwa/dist/tasks/browser`** satisfies §13.1 — `python3 -m http.server 8000` from inside that folder works identically. It must be the build output, not `src`: the service worker only exists in a production build.

`cd pwa && npm start` runs a live-reload dev server for editing instead, with the service worker deliberately disabled; the Settings page reports that rather than claiming a false "offline ready".

Then open `http://localhost:8000` in the browser on that same machine — Chromium browsers will offer to install it once the manifest/icons/HTTPS-or-localhost criteria are met.

For installing from a *phone*, that plain-http server is not enough — see §14. The short version is `node tools/make-cert.js` once, then `node tools/serve.js --https`, which serves the same build over TLS on 8443 with a certificate the phone can be taught to trust.

## 14. Installing on a Phone Without Public Hosting

`localhost` is scoped to a single device — a phone browsing to the PC's LAN IP (e.g. `http://192.168.1.20:8000`) is **not** treated as a secure/installable origin by Chromium, since the requirement is specifically `localhost`/`127.0.0.1`/`https`, not "any local network address." Three practical ways around this, without any public hosting:

1. **Android + Chrome, via USB (most reliable):** run the server on the PC (§13.6) with the phone connected by USB and USB debugging enabled, then run `adb reverse tcp:8000 tcp:8000`. Now `http://localhost:8000` in Chrome *on the phone* really is `localhost` from the phone's own point of view, satisfying the install requirement — Chrome will offer to install it. After the first successful load, the service worker (§13.4) has cached the shell, so neither the USB connection nor the PC server needs to stay on for daily use.
2. **Fully on-device, no PC involved:** copy `pwa/dist/tasks/browser` to the phone and run a static server directly on it (e.g. via Termux on Android running the same `python3 -m http.server` command), then open `http://localhost:PORT` in Chrome on that same device.
3. **iOS Safari:** historically more lenient than Chromium's install-prompt gate about the page's origin for Share → Add to Home Screen — a page loaded over the PC's LAN IP in Safari can often be added directly. This varies by iOS version, so verify on the actual device; the `adb reverse` technique is Android/Chrome-specific and doesn't apply here.

4. **Android + Chrome, LAN IP marked trusted (no USB, no extra tooling) — superseded by path 5, see the address-bar note below.** Chrome can be told to treat one specific insecure origin as secure: on the phone, open `chrome://flags/#unsafely-treat-insecure-origin-as-secure`, enter the PC's LAN origin (e.g. `http://192.168.100.28:8000` — origin only, no trailing slash), set it to **Enabled**, and relaunch Chrome. That origin then satisfies the secure-context requirement, so the service worker registers, the shell caches per §13.4, and Chrome offers a real **Install app** (WebAPK) rather than a bare *Add to Home screen* shortcut. It requires no `adb` and no USB debugging, which is why it was chosen over path 1 here. Two caveats: the flag is bound to that exact scheme+IP+port, so a DHCP change breaks re-installs (an already-cached install keeps working), and it does relax a genuine browser security guarantee for that one origin — do not point it at anything untrusted.

5. **Android + Chrome, real HTTPS on the LAN IP with a locally trusted certificate — this is the path actually in use.** The origin requirement is `https` *or* `localhost`; path 4 only makes Chrome *pretend* the first is met, and pretending is not enough for the install itself. `tools/make-cert.js` generates a private CA plus a leaf certificate naming `localhost`, `127.0.0.1` and every LAN IPv4 of the PC (it drives the `openssl` binary — locating Git for Windows' copy when PATH has none, since PowerShell's PATH normally doesn't — nothing fetched, keys never leave the machine). `node tools/serve.js --https` then serves the same build over TLS on port 8443, and alongside it a tiny plain-http page on 8000 whose only content is the CA download and the install steps — the phone cannot fetch the CA over the connection the CA exists to validate. Install `rootCA.crt` once under Android's *Install a certificate → CA certificate*, restart Chrome, and `https://<pc-ip>:8443` is a genuinely secure origin: no `chrome://flags` override, service worker registers, and Chrome offers a real **Install app**. Re-running `make-cert.js` after a DHCP change re-issues only the leaf, so the phone never has to trust anything twice. Caveats: the CA private key under `tools/certs/` (gitignored) must stay on that PC, and `https://host:port` is a **different origin** from `http://host:port`, so `localStorage` starts empty — export a backup before switching and restore it after.

**Why the address bar appears, and what removes it.** `display: "standalone"` (§13.3) is necessary but not sufficient. Chrome keeps browser UI over an installed app whenever the origin is not genuinely secure: over plain http it commonly degrades **Install app** to a bookmark shortcut that opens in an ordinary Chrome tab, and the `chrome://flags` override of path 4 does not change this — it relaxes the *secure-context* check that gates service workers, not the install. Path 1, 2 or 5 (real `localhost`, or real `https`) is what produces a chromeless window. So an address bar on the installed app is a symptom of the origin, not of the manifest — check the origin before touching `display`/`display_override`.

**Verifying any of these paths.** The failure mode is silent: on a non-secure origin `navigator.serviceWorker` is simply undefined, so registration never happens, nothing caches, and the app appears to work right up until the server stops. Solution B's Settings page therefore carries an **Offline install** card reporting whether the shell is actually cached. Confirm it reads *"Offline ready"* before disconnecting — that, not the presence of a home-screen icon, is what proves §13.4 is in effect.

Whichever path is used, the result is the same: an app icon on the home screen that launches full-screen, matching Solution A's UI and behavior.

## 15. Keeping Solution A and Solution B in Sync

Both targets are the **same application** with two different shells. What must never diverge is behavior:

- Identical data model (§3), identical `lastDueDate`/recurrence engine (§4), identical Home/Tasks/Settings behavior (§6–§7), identical `ptm_data` export/import schema. A backup exported from Solution A imports cleanly into Solution B and vice versa. **This is the contract**; if the two ever disagree about a due date or a section, that is a bug in one of them.
- Solution B's CSS is byte-for-byte §8's stylesheet, so both targets look identical.
- All of §9's security requirements apply to Solution B, and it meets them more strictly than Solution A can: no inline script at all (so `script-src 'self'` with no `'unsafe-inline'`), and icons rendered from structured shape data rather than through `innerHTML`. Additionally: scope the service worker and manifest to same-origin only — never fetch, cache, or reference any third-party origin.

**On sharing source.** The original plan was to reuse Solution A's HTML/CSS/JS verbatim inside Solution B's `index.html`. That stopped being possible once Solution B became an Angular app: the logic is now a TypeScript port, function for function, rather than the same file included twice. That is a real duplication and it is called out as a deviation in `PWA-README.md`. What holds it together instead:

- The port keeps Solution A's function names and structure (`lastDueDate`, `nextDueDate`, `weekDaysList`, `nthWeekday`, `occurrenceInMonth`, `validateData`), so the two read side by side.
- **The engine is now tested.** `pwa/src/app/core/*.spec.ts` pins every rule in §4.1 and every edge case in §12, plus the §6 section rules and §9's import validation. Solution A has no tests; these specs are the authoritative statement of what this document specifies, and are the tiebreaker when the two targets disagree.
- Changes to recurrence or completion semantics must be made in both targets in the same change, with the spec here updated first.

