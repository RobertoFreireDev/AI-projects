# Task Manager — Solution B (Angular PWA)

The installable PWA target described in CLAUDE.md §13–§15. Solution A, the
single-file dependency-free build, is `index.html` at the repository root and is
not part of this workspace.

**Read [`../PWA-README.md`](../PWA-README.md) first** — it covers the file
layout, how to install the app on a phone without any hosting, the LAN-IP trap
that silently disables offline mode, and what has and has not been verified.

## Commands

| Command | What it does |
|---|---|
| `npm install` | one-time setup |
| `npm run build` | production build → `dist/tasks/browser` (the only build with a service worker) |
| `npm start` | dev server on `http://localhost:4200`, service worker disabled |
| `npm run test:run` | run the test suite once |
| `npm test` | run the test suite in watch mode |
| `npm run serve` | serve the build output on `http://localhost:8000`, for installing |
| `npm run serve:adb` | same, plus `adb reverse` for a USB-connected Android phone |
| `npm run serve:lan` | same, also reachable on the LAN (read the LAN-IP trap first) |
| `npm run icons` | regenerate `public/icon-*.png` — only needed if the glyph or palette changes |

## Where the logic lives

`src/app/core/` holds everything that isn't presentation: the calendar
arithmetic (`date.ts`), the recurrence engine (`recurrence.ts`), the data model
and limits (`model.ts`), strict import validation (`validation.ts`) and the
store that owns `ptm_data` (`task-store.ts`). Each has a `.spec.ts` beside it.

Those specs are the authoritative statement of what CLAUDE.md §4, §6 and §9
specify, and they are what keeps this target and Solution A in agreement — see
§15 and the *Keeping the two targets in sync* section of `../PWA-README.md`.
