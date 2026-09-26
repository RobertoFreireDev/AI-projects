# CLAUDE.md — SQL for Kids 🐾

A visual SQL learning game for kids aged 8–12. Players solve small challenges by building queries with a tap-and-pick visual editor. No real SQL engine: queries run in memory with plain JS functions.

## Hard rules

- **The game is ONE file: `index.html`.** HTML + CSS + JS only. No frameworks, no build step, no CDN, no external fonts, no image files. It must work by opening the file directly (`file://`) and offline.
- **Unit tests live OUTSIDE `index.html`**, in `tests/`. The game file never contains test code.
- Targets: mobile (portrait), tablet and PC. Touch-first, mouse and keyboard also work.
- Icons are emoji. Use them everywhere possible (challenge text, visual SQL, result table, menu).
- No typing required to play. Everything is picked with buttons, chips, dropdowns and +/- steppers.
- Never use `eval`, `new Function`, or string SQL parsing inside the game. Queries are data (a JSON object), executed by engine functions.

## Project layout

```
/
├── index.html              # the whole game (engine + data + levels + UI)
├── CLAUDE.md
└── tests/
    ├── load-engine.js      # extracts the engine script from index.html into a Node vm
    ├── engine.test.js      # query engine tests
    ├── levels.test.js      # level + data integrity tests
    ├── progress.test.js    # progress logic tests
    └── browser-runner.html # optional: runs the same checks in a browser via an iframe
```

## Commands

- Play: open `index.html` in a browser.
- Tests: `node --test tests/` (Node 18+, built-in `node:test` and `node:assert`, **no npm dependencies**).
- Run tests after every change to the engine, data or levels. All tests must pass before finishing a task.

## How the tests reach code inside the single file

`index.html` has two script blocks:

1. `<script id="sqlkids-engine">` — **pure logic only**: data, levels, query engine, result comparison, progress logic. It must not touch `document`, `window`, `localStorage` or any DOM/browser API. It ends with:
   ```js
   globalThis.SqlKids = { DATA, LEVELS, ICONS, run, validate, compareResults, createProgress, KidError };
   ```
2. `<script id="sqlkids-ui">` — everything DOM: screens, visual editor, rendering, storage. It uses `globalThis.SqlKids`.

`tests/load-engine.js` reads `index.html`, extracts the content of `<script id="sqlkids-engine">` with a regex, runs it in `node:vm` with a fresh context, and returns `context.SqlKids`. Keep that script tag id and the pure-logic contract stable or the tests break.

`tests/browser-runner.html` loads `../index.html` in a hidden iframe and runs the same assertions against `iframe.contentWindow.SqlKids` (for checking without Node).

## Data model (in memory)

Four tables. Every value that has a matching icon is shown with it.

**animal** — `id`, `name`, `type`, `age`, `color`, `legs`
Example: `{ id: 1, name: "Tom", type: "cat", age: 3, color: "orange", legs: 4 }`

**food** — `id`, `name`, `kind`, `color`
Example: `{ id: 1, name: "apple", kind: "fruit", color: "red" }`

**animal_eats_food** — `animal_id`, `food_id`, `amount` (portions per day)

**animal_lives** — `animal_id`, `place`

Seed data guidelines:
- ~12 animals, ~12 foods, ~25 eats rows, ~15 lives rows.
- Some animals live in 2 places (so joins produce extra rows and DISTINCT matters).
- At least one animal has no food row and one has no place row (so INNER JOIN visibly drops them).
- Repeated values in `type`, `color`, `kind`, `place` (so DISTINCT and ORDER BY are meaningful).
- Friendly names ("Tom", "Luna", "Bolt"...). Small ages (1–15).
- All foreign keys must be valid (tested).

### Icon map (`ICONS`)

- Tables: animal 🐾, food 🍽️, animal_eats_food 😋, animal_lives 🏡
- Columns: id 🔢, name 🏷️, type 🐾, age 🎂, color 🎨, legs 🦵, kind 📦, amount 🥄, place 📍, animal_id 🐾🔢, food_id 🍽️🔢
- Animal types: cat 🐱, dog 🐶, rabbit 🐰, bear 🐻, monkey 🐵, frog 🐸, fish 🐟, duck 🦆, owl 🦉, squirrel 🐿️, lion 🦁, penguin 🐧
- Foods: apple 🍎, banana 🍌, carrot 🥕, honey 🍯, cheese 🧀, bread 🍞, nuts 🥜, grapes 🍇, corn 🌽, meat 🍖, milk 🥛, lettuce 🥬
- Food kinds: fruit 🍓, vegetable 🥦, sweet 🍬, protein 💪, grain 🌾
- Places: tree 🌳, lake 🏞️, farm 🚜, forest 🌲, cave 🪨, ocean 🌊, house 🏠, desert 🏜️, mountain ⛰️, ice 🧊
- Colors: shown as a colored dot 🔴🟠🟡🟢🔵🟣🟤⚫⚪ next to the word
- Numbers: plain large digits (no icon needed)

Rule: result cells render `icon + text` (never icon alone, so kids learn the words).

## Visual SQL (query model)

The editor builds this object. The engine only accepts this shape.

```js
{
  select:  { distinct: false, all: true, columns: [] },  // all=true means *
  from:    "animal",
  joins:   [ { table: "animal_lives",
               on: { left:  { table: "animal", column: "id" },
                     right: { table: "animal_lives", column: "animal_id" } } } ],
  where:   [ { link: null,  left: { table: "animal", column: "age" }, op: ">", right: { value: 3 } },
             { link: "and", left: { table: "animal_lives", column: "place" }, op: "=", right: { value: "lake" } } ],
  orderBy: [ { col: { table: "animal", column: "age" }, dir: "desc" } ],
  top:     null,   // number or null
  offset:  null    // number or null
}
```

Supported features (nothing else):
- **SELECT**: `*` (label it "ALL ⭐"), a list of columns, optional **DISTINCT** toggle.
- **FROM**: one table.
- **JOIN ... ON**: INNER JOIN only, `left = right` equality only, up to 2 joins.
- **WHERE**: conditions `column op value` or `column op column`. Ops: `=`, `≠`, `>`, `<`, `≥`, `≤`. Linked with AND / OR, where AND binds tighter than OR (standard SQL). No parentheses.
- **ORDER BY**: one or more columns, each ⬆️ asc or ⬇️ desc.
- **TOP n** and **OFFSET n**.

Engine execution order: FROM → JOIN → WHERE → SELECT (columns) → DISTINCT → ORDER BY → OFFSET → TOP.
Note: ORDER BY may use a column not in SELECT; sort before projecting away columns, then apply DISTINCT on projected values while keeping the first occurrence in sorted order.

Engine rules:
- `run(query, DATA)` returns `{ columns: [{table, column}], rows: [[...]] }`.
- With joins, `*` returns all columns of all joined tables in join order.
- Strings compare case-insensitively for `=`/`≠`; numbers compare numerically; comparing a number with a text gives a friendly error.
- Invalid queries throw `KidError` with a kid-friendly message and emoji, e.g. `"🤔 Pick a table in FROM first!"`, `"🧐 Which 'id' do you mean? Both tables have one!"`, `"🙈 TOP must be 1 or more."`
- `validate(query)` returns a list of `KidError` messages without running.

## Level checking

- A level has a `solution` query. The checker runs the solution and the player's query and calls `compareResults(expected, actual, { ordered })`.
- Same columns (same order) and same rows. Rows compare as a multiset unless the level has `ordered: true` (levels teaching ORDER BY / TOP / OFFSET).
- Never hardcode expected rows in levels; always derive them from `solution`.

## Levels

```js
{
  id: "w1-l1",
  world: 1,
  title: "Hello, animals! 👋",
  story: "The zoo keeper wants to see ALL the animals 🐾. Show every [[animal]] with every column!",
  hint: "Use SELECT ALL ⭐ and FROM 🐾 animal.",
  unlocks: ["select", "from"],   // clauses visible in the editor for this level
  ordered: false,
  solution: { ... }
}
```

- Story tokens: `[[table]]`, `[[table.column]]` and `[[value]]` render as icon chips (e.g. `[[animal.age]]` → 🎂 age chip, `[[lake]]` → 🏞️ lake chip).
- The editor only shows clauses listed in `unlocks`, so early levels are simple.
- Levels are sequential: completing one unlocks the next. Completed levels show ✅ and can be replayed.

Suggested worlds (3–5 levels each, ~25 total):
1. 🌱 SELECT * / FROM
2. 🏷️ Pick columns
3. 🔍 WHERE (=, then >, <, then AND, then OR)
4. 🔢 ORDER BY (asc, desc, two columns)
5. 🌟 DISTINCT
6. 🏆 TOP / OFFSET ("the 3 oldest", "skip the first 2")
7. 🔗 JOIN (animal + animal_lives, animal + animal_eats_food)
8. 🚀 Double JOIN (animal → animal_eats_food → food) + everything combined

Example challenge (world 8): "Which animals 🐾 older than 🎂 3 eat 🍎 apples? Show their names, oldest first!"

## Screens

**Menu screen**
- Big title with logo emoji, world sections, level buttons in a grid (locked 🔒, open, completed ✅).
- "Clear progress 🧹" button → custom kid-friendly confirm modal (not `window.confirm`).

**Level screen** (top to bottom, single column on phone, wider centered column on tablet/PC):
1. Challenge card: title, story with icon chips, 💡 hint button.
2. Visual SQL editor: one colored block per clause, each with the real keyword plus a small kid label (SELECT "show me", FROM "look in", JOIN "connect with", WHERE "only if", ORDER BY "sort by", TOP "only the first", OFFSET "skip").
3. Buttons: ▶️ Run and ✅ Check answer.
4. Result table: icon headers, icon + text cells, row count ("🐾 5 rows"). Wide tables scroll horizontally inside their own container.
5. Feedback: success celebration (confetti/emoji burst, "Next level ➡️") or gentle retry message. Never say "wrong"; say "Almost! Try again 💪".

Back button to menu is always visible.

## Design for ages 8–12

- Font: system rounded stack `ui-rounded, "Segoe UI Rounded", "Arial Rounded MT Bold", "Nunito", system-ui, sans-serif`. Body ≥ 18px, buttons ≥ 22px, titles ≥ 32px. Use `rem`.
- Touch targets ≥ 48×48px, generous spacing, rounded corners (16px+), soft shadows.
- Palette (CSS variables on `:root`):
  - background `#FFF8E7` (warm cream), text `#2B2D42`
  - SELECT `#4EA8DE` blue, FROM `#57CC99` green, JOIN `#9B5DE5` purple, WHERE `#FF9F1C` orange, ORDER BY `#F15BB5` pink, TOP/OFFSET `#00BBF9` teal
  - success `#38B000`, gentle error `#FF6B6B`
- Text on colored blocks must keep WCAG AA contrast. Never rely on color alone: every clause also has its keyword and an icon.
- Light, friendly animations (bounce on tap, wiggle on error). Respect `prefers-reduced-motion`.
- Responsive with flexbox/grid; layout works from 320px wide phones to desktop.

## Progress storage

- Pure logic in the engine: `createProgress(storage)` where `storage` has `get(key)` / `set(key, value)`. Methods: `isUnlocked(id)`, `isCompleted(id)`, `complete(id)`, `reset()`.
- The UI passes a `localStorage` adapter wrapped in try/catch; if storage fails, fall back to an in-memory object so the game still works.
- Tests pass a fake in-memory storage.

## Unit tests (outside the single file, in `tests/`)

Must cover every SQL feature:
- SELECT `*`, single column, multiple columns, column order preserved
- DISTINCT on one and several columns; DISTINCT combined with ORDER BY
- FROM each table
- INNER JOIN: one join, two joins, rows without a match are dropped, `*` with joins, qualified columns
- WHERE: every operator on numbers and text, column vs value, column vs column, AND, OR, AND-before-OR precedence, no matching rows (empty result)
- ORDER BY: asc, desc, multi-key, text and numbers, sorting by a column not selected
- TOP, OFFSET, TOP + OFFSET, OFFSET beyond row count (empty), TOP larger than row count
- Errors: missing FROM, unknown table/column, ambiguous column, join with bad ON, TOP/OFFSET < 0, number vs text comparison — all throw `KidError` with a non-empty message
- `compareResults`: same rows, different order (ordered vs unordered), different columns, duplicates counted correctly
- Levels: every level's `solution` validates, runs, returns at least 1 row, and only uses clauses listed in its `unlocks`; level ids are unique
- Data integrity: all `animal_id` / `food_id` foreign keys exist; every value in type/food name/kind/place columns has an icon
- Progress: first level unlocked, completing unlocks the next, reset clears everything

## Workflow for Claude

1. Engine and data first, with tests, before building the UI.
2. After any engine/data/level change: run `node --test tests/` and fix failures.
3. When adding a SQL feature or level, add tests for it in the same task.
4. Keep `<script id="sqlkids-engine">` free of DOM code.
5. Keep code readable: small functions, comments explaining engine steps.
