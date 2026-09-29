# CLAUDE.md — Blockprint

> Working name: **Blockprint** (rename freely). A browser platform for **system design challenges**. Each challenge describes a product with requirements; the player solves it by building an **architecture block diagram** (clients, load balancers, caches, databases, queues…) on a canvas. The diagram is checked automatically against the challenge's expected result and gets 1–3 stars.
>
> **Platform and content are separate.**
> - `index.html` is the **platform**: it loads a challenge pack, displays the challenge text, renders and edits the diagram, runs the diagram logic, and validates the answer. It contains **no challenge content**.
> - `challenges.json` is the **content**: all challenges (text, numbers, rules, expected solutions), chapters and glossary. The official pack holds 100 challenges, but the platform must work with any valid pack.

Read this whole file before writing code. When a decision here conflicts with a later request from the user, the user wins — then update this file.

> **Current status (user decisions, 2026-09-29)**
> - Milestones 1–5 and 7–9 are implemented in `index.html` (platform complete).
> - `challenges.json` (pack `version` 1.0.0) holds **all 100 challenges** (`c001`–`c100`, 10 chapters of 10, one boss per chapter) following the plan in §8, plus 123 glossary concepts. It passes `?selftest` with 0 errors, 0 warnings, 100/100. Chapters 1–5 use `"palette": "chapter"`, 6–10 use `"all"`.
> - **No unit tests for now**: the `tests/` folder (§2, §12) is deferred. Until it exists, check a pack with `index.html?selftest` (schema + expected results), and check the page by hand over HTTP and from `file://`. The "`node tests/run.mjs` must pass" rules apply once the tests are written.

---

## 1. Hard constraints (never break these)

1. **Two runtime files, with strict roles.**
   - `index.html` = platform. All HTML, CSS and JS live inside it. It must never contain challenge text, rules or solutions (a tiny built-in demo challenge for the "no pack loaded" screen is the only exception).
   - `challenges.json` = content. Pure JSON data: no code, no functions, no HTML, no expressions to evaluate.
2. **Zero external packages.** No npm dependencies, no CDNs, no web fonts, no remote images, no frameworks, no bundler, no transpiler. Only what the browser ships.
3. **Works when hosted and when opened from `file://`.** Browsers block `fetch()` of local files on `file://`, so the platform loads the pack in this order (details in §5.1): `fetch` next to the page → last pack cached in the browser → the player picks or drops the JSON file. No ES module imports from other files, no service worker requirement.
4. **Vanilla JS (ES2020), classic `<script>` tags**, `'use strict'`. No `eval`, no `new Function`, no `innerHTML` with pack or user text (use `textContent` or the safe text renderer in §5.4). Pack content is untrusted data.
5. **Tests live outside the product files** (`tests/`) and run with plain Node built-ins (`node:test`, `node:assert`, `node:vm`, `node:fs`). No npm install, ever.
6. **The diagram is SVG.** No `<canvas>` for the editor, no `foreignObject` inside the diagram SVG (it breaks PNG export).
7. Target: latest Chrome, Firefox, Safari, Edge; desktop first, fully usable on tablet; usable (list mode) on phone.

---

## 2. Repository layout

```
/
├── index.html          ← the platform (engine + UI), no challenge content
├── challenges.json     ← the official content pack (100 challenges)
├── CLAUDE.md           ← this file
└── tests/
    ├── run.mjs         ← entry: node tests/run.mjs
    ├── load-core.mjs   ← extracts <script id="core"> from index.html, runs it in node:vm
    ├── load-pack.mjs   ← reads and JSON.parses challenges.json (or a pack path given as argument)
    ├── validator.test.mjs
    ├── catalog.test.mjs
    ├── pack-schema.test.mjs
    ├── challenges.test.mjs
    └── fixtures/       ← small hand-written packs, valid and broken, for loader/schema tests
```

Commands:

- Play (hosted or local server): serve the folder statically, e.g. `python3 -m http.server`, then open `http://localhost:8000/`. The page fetches `challenges.json` automatically.
- Play (no server): open `index.html` directly, then choose or drop `challenges.json` on the load screen. The pack is remembered for next time.
- Test: `node tests/run.mjs` (must exit 0 before any task is considered done). `node tests/run.mjs path/to/other-pack.json` validates another pack.
- In-browser self-test: open `index.html?selftest` → validates the loaded pack and runs every challenge's expected-result checks in a results overlay.

---

## 3. Inside `index.html` — section map

The file is large. Every section starts with a banner comment so it can be found with grep. **Edit with targeted replacements; never rewrite the whole file.**

```
<!doctype html>
<html>
<head>
  <style>
    /* ==== [CSS:TOKENS] ==== */      design tokens, light + dark
    /* ==== [CSS:LAYOUT] ==== */      app shell, panels, responsive
    /* ==== [CSS:EDITOR] ==== */      canvas, nodes, edges, groups
    /* ==== [CSS:COMPONENTS] ==== */  buttons, forms, dialogs, toasts
  </style>
</head>
<body>
  <!-- ==== [HTML:SHELL] ==== -->     static skeleton only; views are rendered by JS

  <script id="core">
    // Pure logic. NO DOM, NO window, NO localStorage. Must run in node:vm.
    // No challenge content here — it all comes from the pack.
    /* ==== [CORE:VERSION] ==== */     ENGINE_VERSION (integer), supported pack schema versions
    /* ==== [CORE:CATALOG] ==== */     block types, edge kinds, group types (engine vocabulary)
    /* ==== [CORE:PACK] ==== */        parsePack(text) → { pack, errors, warnings }; normalize defaults; expand compact diagrams
    /* ==== [CORE:SCHEMA] ==== */      checkPack(pack): structure, references, versions (used by loader, tests, ?selftest)
    /* ==== [CORE:GRAPH] ==== */       graph helpers: selectors, adjacency, paths, group membership
    /* ==== [CORE:VALIDATOR] ==== */   rule engine + scoring
    /* ==== [CORE:LAYOUT] ==== */      auto-layout for expected solutions
    /* ==== [CORE:SELFTEST] ==== */    runPackSelfTest(pack): expected-result checks per challenge
    /* ==== [CORE:EXPORT] ==== */      exports to globalThis.Core
  </script>

  <script id="app">
    // Browser-only code. Reads Core, never duplicates its logic.
    /* ==== [APP:STORE] ==== */        localStorage wrapper, schema version, migrations
    /* ==== [APP:LOADER] ==== */       pack sources: ?pack / fetch challenges.json → cache → file picker / drag-drop; demo challenge
    /* ==== [APP:STATE] ==== */        app state, undo/redo stack
    /* ==== [APP:ROUTER] ==== */       hash routes
    /* ==== [APP:EDITOR] ==== */       SVG canvas, pointer interactions
    /* ==== [APP:PANELS] ==== */       palette, properties, brief, results
    /* ==== [APP:VIEWS] ==== */        home grid, workspace, glossary, settings
    /* ==== [APP:EXPORT] ==== */       JSON import/export, PNG/SVG export
    /* ==== [APP:SELFTEST] ==== */     ?selftest overlay
    /* ==== [APP:BOOT] ==== */
  </script>
</body>
</html>
```

`Core` is the single shared global: `globalThis.Core = { ENGINE_VERSION, CATALOG, parsePack, checkPack, expandDiagram, validate, score, layout, runPackSelfTest }`. Core never holds a pack in global state — every function receives the pack or challenge it works on. For debugging in the browser also expose `window.Blockprint = { Core, state }` (the loaded pack lives in `state.pack`).

**Who owns what:** the engine owns the *vocabulary* (block types, props, edge kinds, group types, check types). The pack owns the *content* (chapters, challenges, concepts, rules, expected solutions) and may only use that vocabulary. A pack that references an unknown block type, prop, or check type is rejected with a clear error.

---

## 4. Data model

### 4.1 Diagram (what the player builds, what gets saved)

```js
{
  v: 1,
  nodes:  [{ id: 'n1', type: 'lb', x: 320, y: 160, label: 'Public LB',
             props: { layer: 'L7', algorithm: 'least-connections', healthChecks: true } }],
  edges:  [{ id: 'e1', from: 'n1', to: 'n2', kind: 'sync',
             props: { protocol: 'HTTP', timeoutMs: 2000, retries: 2, backoff: true, jitter: true, circuitBreaker: false } }],
  groups: [{ id: 'g1', type: 'region', x: 40, y: 40, w: 900, h: 500, label: 'eu-west' }]
}
```

- Edges are **directed**: caller → callee, producer → consumer, primary → replica.
- **Group membership is geometric**: a node belongs to every group whose rectangle contains the node's center. Groups can nest (subnet inside VPC inside region).
- Node `label` is cosmetic. Rules never depend on labels.
- Node ids and edge ids are generated (`n` + counter); never reused inside one diagram.

### 4.2 Edge kinds

| kind | Meaning | Style |
|---|---|---|
| `sync` | Request/response call (HTTP, gRPC, SQL) | solid line, filled arrow |
| `async` | Message/event (publish, enqueue, consume) | dashed line, open arrow |
| `replication` | Data copied from source to replica/standby | dotted line, double arrow head |
| `data` | Bulk/batch data flow (ETL, backup, CDC, upload) | thick line, filled arrow |

When the player connects two blocks, preselect the most likely kind (e.g. anything → `queue` is `async`; `sql-db` → `read-replica` is `replication`). The player can change it in the properties panel.

### 4.3 Block catalog (`CORE:CATALOG`)

Each entry: `{ type, label, category, cost, glyph, blurb, props }`.

- `cost` 1–5 (used by budget rules — teaches cost trade-offs). A block with a `replicas` prop costs `cost × replicas`.
- `glyph` = tiny inline SVG path drawn by code (no emoji, no images).
- `blurb` = one plain sentence explaining what the block is for (shown in palette tooltip and glossary).
- `props` = schema: `{ key: { kind: 'bool'|'int'|'enum'|'text', options?, default, min?, max?, label } }`.

| Category | Types (and key props) |
|---|---|
| Clients & edge | `client-web`, `client-mobile`, `dns`, `geo-dns` (routing: latency/geo), `cdn` (cacheTtl), `waf` |
| Traffic | `lb` (layer L4/L7, algorithm, healthChecks, sticky), `reverse-proxy` (tls, cache), `api-gateway` (auth, rateLimit, loadShedding, routes), `rate-limiter` (algorithm: token-bucket/leaky-bucket/sliding-window), `bff` |
| Compute | `app-server` (replicas, stateless, autoscale, minReplicas, maxReplicas), `service` (role, replicas, sidecar, idempotent), `worker` (replicas, idempotent, pool), `scheduler` (leaderElection), `function` (serverless), `ws-gateway` (replicas), `orchestrator`, `transcoder`, `embedding-worker` |
| Data | `sql-db` (isolation, locking: none/optimistic/pessimistic, appendOnly, shardKey), `read-replica`, `kv-store` (ttl, geo, n, r, w), `document-db`, `wide-column-db`, `graph-db`, `timeseries-db` (downsampling), `search-index`, `vector-index`, `cache` (strategy: cache-aside/read-through/write-through/write-behind/write-around, eviction: LRU/LFU, ttlJitter, stampedeProtection, replicas), `bloom-filter`, `connection-pooler`, `shard-router` (hashing: modulo/consistent/rendezvous), `id-generator`, `object-storage` (tier: hot/cold/archive, signedUrls, lifecycle), `data-warehouse`, `data-lake`, `geo-index`, `dedup-store` |
| Messaging | `queue` (delayed, priority, maxSize, maxRetries, delivery: at-most-once/at-least-once), `dlq`, `pubsub-topic`, `event-stream` (partitions, partitionKey, retentionDays, consumerGroup), `cdc-connector`, `outbox-relay`, `stream-processor` (windowing), `batch-job` (etl/elt) |
| Coordination | `coordination-service` (locks, leaderElection), `service-registry`, `transaction-coordinator` (protocol: 2PC), `mesh-control-plane` (mtls) |
| Security | `identity-provider` (protocol: OAuth2/OIDC, tokens: JWT/opaque), `secrets-manager`, `kms` |
| Observability | `log-collector`, `metrics-store`, `tracing-collector` (correlationIds), `alerting`, `dashboard` |
| External | `third-party-api`, `payment-provider`, `email-provider`, `sms-provider`, `push-service` |
| Groups (containers) | `region`, `availability-zone`, `vpc`, `subnet-public`, `subnet-private` |

Rules:
- The catalog is engine code, not pack content: packs can't add block types (they would have no glyph, props or behaviour). A pack that needs a new block requires an engine change and an `ENGINE_VERSION` bump.
- Adding a block type means: catalog entry + glyph + blurb + default props + a catalog test.
- Block types and prop keys are kebab-case/camelCase and stable forever (packs and saved diagrams depend on them). Renaming requires a migration in `APP:STORE` and a pack schema note.
- `service.role` options come from the challenge (`roles` in the JSON), shown as a dropdown so rules never depend on typed names.

---

## 5. The challenge pack (`challenges.json`)

### 5.1 Loading (`APP:BOOT` + `CORE:PACK`)

On start, the platform tries these sources in order and uses the first that works:

1. `?pack=<url>` query parameter (same-origin URL or relative path), else `challenges.json` next to `index.html`, via `fetch` with `cache: 'no-cache'`. This is the normal path when hosted.
2. The last successfully loaded pack cached in the browser (`blockprint.v1.pack`). If step 1 also succeeded and the fetched pack's `id` + `version` differ from the cache, the fetched one wins and replaces the cache.
3. **Load screen**: shown when nothing above worked (typical on `file://`). It explains in one sentence why, and offers "Choose challenges file" (`<input type="file" accept=".json,application/json">`) plus drag-and-drop anywhere on the page. A small built-in demo challenge lets people try the editor without a pack.

Settings has "Load another pack" (same picker) and shows the current pack's title, version and challenge count.

Every source goes through the same path: `text → Core.parsePack(text) → { pack, errors, warnings }`.
- `JSON.parse` errors are reported with line/column when the browser provides a position.
- Any error → the pack is rejected, the previous pack (if any) stays active, and the load screen lists the errors (path + message, e.g. `challenges[19].expected.rules[2].check.path[1]: unknown block type "cahce"`).
- Warnings (e.g. a concept never used) are shown in `?selftest` and the console, not to players.
- Loading never executes anything from the file.

### 5.2 Top-level structure

```json
{
  "format": "blockprint-pack",
  "schema": 1,
  "engine": 1,
  "id": "core-100",
  "title": "System Design: 100 Challenges",
  "version": "1.0.0",
  "language": "en",
  "chapters": [
    { "id": "ch01", "n": 1, "title": "Foundations", "intro": "Clients, servers, tiers and the first database." }
  ],
  "concepts": [
    { "id": "cache-aside", "term": "Cache-aside", "def": "The app reads the cache first and loads from the database on a miss, then fills the cache.", "blocks": ["cache"] }
  ],
  "challenges": [ ]
}
```

- `format` must equal `"blockprint-pack"`; `schema` is the pack format version the file follows; `engine` is the minimum `ENGINE_VERSION` it needs. A pack with a newer `schema` or `engine` than the platform supports is rejected with "This pack needs a newer version of the platform."
- `id` + `version` identify the pack for caching and progress (see §11).
- `chapters` order is display order. Challenge order inside a chapter is array order in `challenges`.
- `concepts` is the glossary. Every `concepts` id used by a challenge must exist here; `blocks` must be catalog types.

### 5.3 Challenge object

```json
{
  "id": "c020",
  "chapter": "ch02",
  "title": "Short links, long life",
  "difficulty": 3,
  "boss": true,
  "concepts": ["estimation", "read-write-ratio", "kv-store", "id-generation", "caching"],
  "brief": "A marketing team wants short links like go.example/Ab3x that redirect instantly and never break.",
  "functional": ["Create a short link", "Redirect to the original URL"],
  "nonFunctional": ["Redirect p99 under 50 ms", "100:1 reads to writes", "Links never collide"],
  "numbers": [
    { "label": "New links per month", "value": "100M" },
    { "label": "Read/write ratio", "value": "100:1" },
    { "label": "Retention", "value": "5 years" }
  ],
  "palette": "chapter",
  "roles": ["links"],
  "start": null,
  "locked": [],
  "budget": 22,
  "estimate": [
    { "q": "Writes per second (average)?", "answer": 38.6, "unit": "writes/s", "tolerance": 0.5,
      "workings": "100M / (30 × 86,400 s) ≈ 38.6" }
  ],
  "tradeoff": {
    "q": "Why is a key-value store a good fit here?",
    "choices": ["…", "…", "…"], "correct": 1, "why": "One sentence."
  },
  "learn": "2–4 sentences: the takeaway, shown on the results sheet.",
  "expected": {
    "rules": [ ],
    "solution": { },
    "alt": [ ],
    "naive": [ { "diagram": { }, "fails": ["r-cache"] } ]
  }
}
```

Field notes:
- `id`: unique in the pack, never changes once published (progress is keyed by it). Official pack uses `c001`…`c100`.
- `chapter`: a chapter `id` from `chapters`.
- `difficulty`: 1–5. `boss`: optional, at most one per chapter; the chapter's boss unlocks the next chapter.
- `palette`: `"chapter"` (all blocks used by expected solutions of this and earlier chapters), `"all"`, or an explicit array of block types.
- `start` / `locked`: optional pre-filled diagram (compact form, §7) for "fix this architecture" challenges; `locked` lists its node ids that cannot be deleted.
- `estimate[].tolerance` is relative (0.5 = ±50%). `workings` is shown after answering.
- **`expected` is the answer key**:
  - `rules` — what the player's diagram is validated against (§6). **The player's answer is judged only by these rules**, never by comparing shapes with `solution`, because many designs are valid.
  - `solution` — the reference design (compact form, §7). Shown by "Show solution" and used by self-tests to prove the rules are satisfiable.
  - `alt` — other designs that must also pass (keeps rules from being too strict).
  - `naive` — common wrong answers and which rule ids must fail for each (keeps rules from being too loose).
- Unknown fields are a **warning**, not an error, so packs can carry authoring notes (use a `"_note"` field; `_`-prefixed fields don't even warn).

### 5.4 Text in the pack

Text fields (`brief`, `learn`, `def`, `intro`, `hint`, `why`, rule `text`) are plain text with a tiny safe subset rendered by `renderText()` in `APP:PANELS`: `**bold**`, `` `code` ``, and blank-line paragraphs. No HTML, no links, no images. The renderer builds DOM nodes with `textContent`; anything else is shown literally.

Titles are generic; briefs may say "like a popular photo app" but pack titles never use trademarks.

---

## 6. Rule engine (`CORE:VALIDATOR`)

Rules come from the pack (`challenge.expected.rules`) as **declarative JSON data**; the engine owns the meaning of every check type. The validator is a pure function:
`validate(diagram, challenge, CATALOG) → [{ id, pass, severity, text, hint, detail }]`.

`checkPack` validates every rule on load: known check type, required fields present, selectors reference catalog types/props/valid values, roles exist in `challenge.roles`, rule ids unique within the challenge. Shorthand in the tables below is written JS-style for brevity; in the pack every key is quoted JSON.

### 6.1 Rule shape

```json
{ "id": "r-cache",
  "severity": "required",
  "text": "Hot links are served from a cache",
  "hint": "Redirects repeat the same keys all day.",
  "weight": 1,
  "check": { "path": [ { "type": "app-server" }, { "type": "cache" } ] } }
```

`severity` is `"required"`, `"bonus"` or `"warn"`. `text` is what the player sees in the checklist; `hint` is revealed progressively; `weight` applies to bonus rules only (default 1).

- `required` → all must pass to solve (1 star).
- `bonus` → extra stars.
- `warn` → anti-pattern detector; never blocks solving, shows an explanation (e.g. client talks straight to the database, services share one database).

### 6.2 Selectors

```js
{ type: 'cache' }                         // one type
{ type: ['kv-store', 'wide-column-db'] }  // any of
{ category: 'data' }
{ type: 'service', props: { role: 'order' } }
{ type: 'sql-db', props: { locking: ['optimistic', 'pessimistic'] } }  // array = any of
```

### 6.3 Checks

| Check | Meaning |
|---|---|
| `{ count: sel, min?, max? }` | number of matching nodes in range |
| `{ capacity: sel, min }` | sum of `replicas` (default 1) of matching nodes ≥ min — redundancy |
| `{ edge: { from: sel, to: sel, kind? }, min? }` | direct edge(s) exist |
| `{ noEdge: { from: sel, to: sel, kind? } }` | no direct edge |
| `{ path: [sel, sel, …], kind? }` | a directed path visits the selectors in order (any nodes between) |
| `{ noPath: { from: sel, to: sel, avoiding?: sel } }` | no path, or no path that avoids a node (e.g. every client→DB path passes an app tier) |
| `{ prop: sel, key, eq? , in?, gte?, lte?, all? }` | some (or all) matching nodes have the prop value |
| `{ edgeProp: { from: sel, to: sel }, key, eq?, gte? }` | same, on edges |
| `{ inGroups: sel, group: sel, distinct: n }` | matching nodes appear in ≥ n distinct groups of that type |
| `{ notInGroup: sel, group: sel }` | e.g. database must not sit in a public subnet |
| `{ fanOut: sel, min }` | a matching node has ≥ min outgoing edges |
| `{ budget: max }` | total cost ≤ max |
| `{ all: [checks] }`, `{ any: [checks] }`, `{ not: check }` | combinators |

Path search ignores `replication` edges unless `kind: 'replication'` is given. Guard against cycles.

### 6.4 Scoring

```
solved = every required rule passes
stars  = 0 if not solved
       = 1 + (bonusWeightPassed ≥ 50% of bonusWeightTotal ? 1 : 0)
           + (all bonus pass AND budget respected ? 1 : 0)
```
Challenges without bonus rules can still reach 3 stars via the estimate + trade-off questions: each correct → +1 star (cap 3). Keep this formula in one function, `score()`, covered by tests.

As implemented: the quiz path applies **only** when the challenge has no bonus rules (with bonus rules, the formula above decides alone), and on the quiz path the 3rd star also needs the budget respected. Each estimate/trade-off gets one answer; answers are stored in progress (`quiz`) and can raise the stars of a solved challenge later. Stars earned are never lowered.

### 6.5 Rule-writing principles

- **Test the idea, not the drawing.** Accept every reasonable architecture. If a CDN or a reverse-proxy cache both solve it, use `any`.
- One rule = one concept. Checklist text is the requirement in plain words, not the check mechanics.
- Every challenge has **3–8 required rules**, **0–4 bonus**, **0–3 warn**.
- Hints progress: after the 1st failed check show nothing extra, after the 2nd show `hint`, after the 3rd offer "Show a block you're missing" (names one missing block type).

---

## 7. Compact diagrams in the pack and auto-layout

Every diagram inside the pack (`expected.solution`, `expected.alt[]`, `expected.naive[].diagram`, `start`) uses a compact JSON form without coordinates. `Core.expandDiagram(compact, CATALOG)` turns it into the full diagram model of §4.1 (defaults filled in, edge kinds inferred, positions from auto-layout):

```json
{
  "n": [["c", "client-web"],
        ["lb", "lb", { "layer": "L7", "healthChecks": true }],
        ["a", "app-server", { "replicas": 3, "stateless": true }],
        ["k", "cache", { "strategy": "cache-aside" }],
        ["d", "kv-store"],
        ["i", "id-generator"]],
  "e": [["c", "lb"], ["lb", "a"], ["a", "k"], ["a", "d"], ["a", "i"]],
  "g": [["r1", "region", ["c", "lb", "a", "k", "d", "i"]]]
}
```

- `n`: `[id, type, props?, label?]`. `e`: `[from, to, kind?, props?]` (kind inferred as in §4.2 when omitted). `g`: `[id, type, memberIds, label?]`.
- `checkPack` rejects unknown types/props, dangling edge ids, and duplicate ids.

`CORE:LAYOUT` places nodes in columns by longest-path depth from source nodes (left → right), orders within a column to reduce crossings (one barycenter pass is enough), then wraps groups around their members with padding (nested groups wrap inner groups first). Used for "Show solution", for `start` diagrams, and for self-tests (so group geometry is real when validating).

---

## 8. Content plan for the official pack (`challenges.json`)

This is the **authoring plan for the JSON file**, not code. None of it goes into `index.html`. The platform must not assume these numbers: the home grid, unlocks and progress work for any number of chapters and challenges.

Ten chapters of ten, following the learning order: requirements → estimation → APIs → databases → caching → load balancing → queues → scaling → distributed systems → consistency → reliability → microservices → observability → security → cloud → capstones. The 10th challenge of each chapter is a **boss** combining the chapter. Early chapters restrict the palette (`"palette": "chapter"`); from chapter 6 on the palette is `"all"`.

Every block type an official challenge needs must already exist in the engine catalog (§4.3). If a challenge needs something new, add it to the engine first.

### Chapter 1 — Foundations

| # | Title | Scenario | Must include |
|---|---|---|---|
| 1 | Hello, server | Personal portfolio page | client → web/app server |
| 2 | Remember my tasks | To-do app that keeps tasks | client → app → SQL DB; warn on client → DB |
| 3 | What's the address? | Users type a domain name | DNS resolution + app |
| 4 | Lock the door | Login page over HTTPS | reverse proxy with `tls` in front of app |
| 5 | Heavy pictures | Recipe site full of photos | images in object storage, API on app; storage estimate |
| 6 | Two servers, one login | Second app server logs users out | LB, app capacity ≥2 `stateless`, session in cache/KV |
| 7 | Night shift | Nightly sales report email | scheduler → worker → DB, worker → email provider |
| 8 | Money's in | Payment provider notifies paid orders | provider → app webhook, `idempotent` handler |
| 9 | Two clients, two needs | Web and mobile need different payloads | a BFF per client, both → shared service |
| 10 | ★ Pastebin | Share text snippets by link | metadata DB, paste bodies in object storage, ID generator; estimate |

### Chapter 2 — Databases & storage

| # | Title | Scenario | Must include |
|---|---|---|---|
| 11 | Read-heavy blog | 100 reads per write | primary + ≥2 read replicas, replication edges, reads hit replicas |
| 12 | Cart in a flash | Shopping cart lookups by user | key-value store |
| 13 | Shape-shifting catalog | Products with varied attributes | document DB |
| 14 | Friends of friends | "People you may know" | graph DB |
| 15 | Sensor flood | 50k devices every 10 s | ingestion → time-series DB; writes/s estimate |
| 16 | One box isn't enough | User DB outgrows one machine | shard router (consistent hashing) + ≥3 shards, `shardKey` |
| 17 | Too many connections | 40 app servers exhaust the DB | connection pooler between app and DB |
| 18 | Uploads done right | Profile photo uploads | direct upload with `signedUrls`, metadata in DB |
| 19 | Hot, warm, cold | Logs kept 7 years, rarely read | hot + archive object storage with lifecycle |
| 20 | ★ URL shortener | 100M links/month, 100:1 reads | ID generator, KV/wide-column, cache, LB + ≥2 apps; estimate |

### Chapter 3 — Caching & CDN

| # | Title | Scenario | Must include |
|---|---|---|---|
| 21 | Cache-aside | Slow profile reads | cache `cache-aside`, app → cache and app → DB |
| 22 | Static at the edge | Global users load assets slowly | CDN in front of object storage |
| 23 | Live leaderboard | Scores read constantly, must be fresh | cache `write-through` |
| 24 | Counting likes | Millions of likes per minute | cache `write-behind` + batch flush worker |
| 25 | Stampede | Celebrity post key expires, DB melts | `stampedeProtection` |
| 26 | Ghost keys | Bots request IDs that don't exist | bloom filter or negative caching before DB |
| 27 | Avalanche | Thousands of keys expire together | `ttlJitter`, cache capacity ≥2 |
| 28 | Cache the API | Public product GET endpoints | reverse proxy with `cache` |
| 29 | Who gets evicted? | Small memory, some items always hot | eviction `LFU`; trade-off LRU vs LFU |
| 30 | ★ Breaking news | 50× traffic in five minutes | CDN + proxy cache + app cache + replicas within budget |

### Chapter 4 — Load balancing & scaling

| # | Title | Scenario | Must include |
|---|---|---|---|
| 31 | Two is one | Survive an app server crash | LB with `healthChecks`, app capacity ≥2 |
| 32 | Route by path | `/api` and `/static` go to different fleets | L7 LB with two backends |
| 33 | Unstick | Sticky sessions overload one server | `sticky` off + external session store |
| 34 | Flash sale | 10× traffic on Friday | `autoscale` with min/max, stateless apps |
| 35 | Stop the scrapers | Bots hammer the API | gateway + rate limiter with shared counter store |
| 36 | Around the world | Users in two continents | geo DNS → two regions, each with LB + apps |
| 37 | Hot partition | One shard gets all celebrity traffic | hot-key cache or salted shard key |
| 38 | Backpressure | Processing slower than uploads | bounded queue (`maxSize`) + load shedding |
| 39 | A million sockets | 1M concurrent WebSocket users | L4 LB → ≥3 WS gateways → pub/sub between them |
| 40 | ★ Ticket rush | 500k fans, 20k seats | waiting-room queue, rate limit, autoscale, DB locking |

### Chapter 5 — Queues, events & async

| # | Title | Scenario | Must include |
|---|---|---|---|
| 41 | Don't make them wait | Signup blocked by welcome email | queue + worker + email provider; warn on app → provider |
| 42 | Tell everyone | Order placed → email, SMS, analytics | pub/sub topic with ≥3 subscribers |
| 43 | Poison pill | One bad message blocks the queue | `maxRetries` + DLQ |
| 44 | Competing consumers | Backlog keeps growing | worker capacity ≥3 on one queue |
| 45 | Order matters | Account events must stay in order | event stream with `partitionKey`, consumer group |
| 46 | Remind me later | Reminder 24 h before an appointment | delayed queue or scheduler |
| 47 | Replay the past | New service needs last 30 days of events | stream `retentionDays` ≥30, new consumer |
| 48 | Exactly once-ish | Redelivery causes duplicate charges | at-least-once + idempotent consumer + dedup store |
| 49 | Video pipeline | Uploads need four resolutions | storage → queue → transcoders → storage → CDN |
| 50 | ★ Notification system | 10M/day, many channels, priorities | priority queue, per-channel workers, preferences, rate limit, DLQ |

### Chapter 6 — Reliability & fault tolerance

| # | Title | Scenario | Must include |
|---|---|---|---|
| 51 | Find the weak spot | `start` diagram with single LB and DB | redundancy on every tier |
| 52 | Flaky partner | Shipping API times out | edge `timeoutMs`, `retries`, `backoff`, `jitter` |
| 53 | Break the circuit | Slow recommendations slow everything | `circuitBreaker` + fallback |
| 54 | Bulkheads | Report exports starve checkout | separate queues/worker pools |
| 55 | Degrade gracefully | Home page loads without personalization | fallback cache/defaults |
| 56 | Shed the load | Gateway overloaded at peak | `loadShedding` with priority |
| 57 | Backups | RPO of one hour | scheduled backups to archive storage in another region |
| 58 | Standby region | RTO of 30 minutes | active-passive, async replication to region B |
| 59 | Both regions live | Writes accepted in two regions | active-active, conflict resolution |
| 60 | ★ Never charge twice | Payments survive AZ loss | idempotency keys, retries + jitter, breaker, multi-AZ DB, outbox |

### Chapter 7 — Consistency & distributed data

| # | Title | Scenario | Must include |
|---|---|---|---|
| 61 | Read your writes | User edits profile, sees old data | route own reads to primary / session consistency |
| 62 | Quorum | Leaderless KV with three copies | `n`, `r`, `w` with r + w > n |
| 63 | One leader | Only one scheduler may run jobs | coordination service + ≥3 schedulers with leader election |
| 64 | Last item | Two buyers, one item | distributed lock or DB row lock |
| 65 | Two-phase commit | Transfer across two databases | transaction coordinator → both DBs |
| 66 | Saga: choreography | Book flight, hotel, car | services talk via events, compensations |
| 67 | Saga: orchestration | Five-step order process | orchestrator → services |
| 68 | Transactional outbox | Save + publish must not diverge | outbox table + relay → stream |
| 69 | CQRS | Dashboards slow down writes | write DB → stream → projection → read store |
| 70 | ★ Bank ledger | Auditable, strongly consistent | append-only store, serializable isolation, projections, audit log |

### Chapter 8 — Microservices, security & observability

| # | Title | Scenario | Must include |
|---|---|---|---|
| 71 | Strangler fig | Migrate a monolith slowly | gateway routes to new service and legacy |
| 72 | Own your data | Three services share one DB (`start`) | database per service; warn on shared DB |
| 73 | Where's my service? | Instances come and go | service registry |
| 74 | Encrypt the inside | Service-to-service traffic in clear | sidecars + mesh control plane with mTLS |
| 75 | Who are you? | Single sign-on | identity provider, gateway validates JWT |
| 76 | No secrets in code | Passwords in config files | secrets manager + KMS |
| 77 | Private by default | DB reachable from the internet | VPC, LB in public subnet, app + DB in private |
| 78 | See the system | Errors found by customers | logs, metrics, alerting |
| 79 | Trace it | Slow checkout across five services | tracing collector, correlation IDs |
| 80 | ★ Online store | Catalog, cart, order, payment, inventory | gateway, DB per service, events, auth, observability |

### Chapter 9 — Data, search & real-time

| # | Title | Scenario | Must include |
|---|---|---|---|
| 81 | Find it | Product search | search index fed by CDC |
| 82 | Autocomplete | Suggestions under 50 ms | prefix service + cache |
| 83 | Nightly numbers | Business reports | ETL batch → data warehouse |
| 84 | Live dashboard | Orders per minute by region | stream processor with windowing → TSDB → dashboard |
| 85 | Keep everything | Raw clickstream forever | data lake + batch processing |
| 86 | Search by meaning | Semantic document search | embedding worker + vector index, hybrid with search index |
| 87 | Who's online | Presence indicators | WS gateway + KV with TTL heartbeats |
| 88 | Where's the driver? | 100k drivers every 4 s | location stream + geo index |
| 89 | Crawl the web | 1B pages per month | URL frontier queue, fetchers, bloom dedup, DNS cache, storage |
| 90 | ★ Monitoring platform | Metrics from 50k hosts | agents → stream → TSDB with downsampling, alerting, dashboards |

### Chapter 10 — Capstones

| # | Title | Scenario | Must include |
|---|---|---|---|
| 91 | News feed | Social timeline | fan-out on write + pull for celebrities |
| 92 | Photo sharing | Upload, feed, likes | object storage, CDN, feed, metadata |
| 93 | Chat app | 1:1 and groups, offline users | WS gateways, message store, receipts, push |
| 94 | Video streaming | Watch on any network | transcoding, adaptive bitrate, CDN |
| 95 | File sync | Sync folders across devices | chunking, dedup, metadata DB, change notifications |
| 96 | Ride sharing | Match riders and drivers | location, matching, trips, payments |
| 97 | Hotel booking | Search and reserve rooms | search, availability, no double booking |
| 98 | Job scheduler | Millions of scheduled jobs | leader election, at-least-once, retries, DLQ |
| 99 | Rate limiter service | Shared limits for many APIs | distributed token bucket, low latency |
| 100 | ★ Food delivery, worldwide | Everything, in three regions | multi-region, every quality attribute, within budget |

Chapter introduction screens list the concepts the chapter covers, taken from `challenge.concepts`.

---

## 9. Screens and UX

Routes are hash-based: `#/`, `#/load`, `#/c/c020`, `#/glossary`, `#/glossary/cache-aside`, `#/settings`. A route to a challenge id not in the loaded pack shows "This challenge isn't in the loaded pack" with a link home.

Everything the player reads about a challenge (brief, requirements, numbers, rule texts, hints, estimates, trade-off, learn, glossary) comes from the pack. The platform adds only interface words.

**Load** — shown when no pack is available, or from Settings. File picker, drag-and-drop area, pack errors list, and the built-in demo.

**Home** — a grid of all challenges in the pack: one row per chapter, one cell per challenge (10 × 10 for the official pack; rows can differ in length). Each cell shows number, state (locked / open / solved) and stars. This grid is the signature visual of the product. Pack title and progress summary beside it. The first chapter is open; each next chapter opens when the previous chapter's boss is solved (chapters without a boss open when 70% of the previous chapter is solved). Settings has "Open all challenges".

**Workspace** — three areas:
```
┌───────────┬──────────────────────────────┬─────────────┐
│ Brief     │                              │ Properties  │
│ reqs,     │          SVG canvas          │ of selected │
│ numbers,  │                              │ block/edge  │
│ checklist │                              │             │
├───────────┴──────────────────────────────┴─────────────┤
│ Palette (search + categories)   [Undo][Redo][Fit][Check]│
└─────────────────────────────────────────────────────────┘
```
Below 900 px wide, Brief and Properties become slide-over drawers; below 600 px the canvas is replaced by default with **List mode** (see accessibility).

**Results sheet** — after Check: rule checklist with pass/fail and explanations, stars, estimate questions, trade-off question, `learn` text, "Show solution" (read-only overlay using auto-layout, available after solving or after 5 failed checks), "Next challenge".

**Glossary** — the pack's `concepts`: definition, related blocks (with their engine blurbs), and the challenges that practice each concept (computed from `challenge.concepts`).

**Settings** — current pack (title, version, challenge count), "Load another pack", theme (system/light/dark), open all, reset progress for this pack (confirm), export/import progress JSON.

### Editor interactions

- Add block: drag from palette, or tap/click a palette item (places at view center, selected).
- Move: drag. Snap to 16 px grid (hold Alt to disable).
- Connect: drag from a node's port handle to another node; or select node, press `C`, click target.
- Select edge: click near it (wide invisible hit path). Edge route: orthogonal elbows with rounded corners, anchored to the nearest sides.
- Groups: drawn from the palette's Groups section, resizable by corner handles, drawn beneath nodes.
- Pan: drag empty space, middle mouse, or two fingers. Zoom: wheel/pinch, 25 %–200 %. `F` = fit to view.
- `Delete`/`Backspace` remove, `Ctrl/Cmd+Z` undo, `Ctrl/Cmd+Shift+Z` or `Ctrl+Y` redo, `Ctrl/Cmd+D` duplicate, `Esc` clear selection, `Enter` on a selected block opens properties.
- Undo/redo: snapshot stack of the diagram JSON, max 100 entries; a drag is one entry.
- During drag, update only the moved element's `transform` and its edges; full re-render on drop.
- Autosave the draft per challenge (debounced 500 ms).

### Accessibility and List mode

- List mode is a complete alternative editor: blocks as a list (add, rename, edit props, delete), connections as rows with "from / to / kind" selects. Same diagram data, same validator.
- All controls reachable by keyboard with visible focus; nodes are focusable `<g tabindex="0" role="button">` with `aria-label` = label + type.
- Pass/fail is never shown by color alone (icon + text).
- Respect `prefers-reduced-motion`.

---

## 10. Visual design direction

Subject: engineers sketching architecture. Direction: **engineering drafting paper** — calm, precise, legible for hours of work.

- Canvas: pale grey-green drafting paper with a fine 16 px grid and a stronger 80 px grid. Lines in deep ink blue. Dark theme: slate "night shift" paper with chalk-like lines.
- Blocks: category colors, muted like colored pencils, used as a left stripe + glyph tint — never full saturated fills. Edges carry meaning through line style (§4.2), not color.
- Type: system font stacks only (no web fonts). UI: `system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`. Block labels and numbers: a condensed stack such as `"Avenir Next Condensed", "Arial Narrow", "Roboto Condensed", sans-serif`. Clear type scale, sentence case everywhere, no all-caps labels.
- The one bold moment: the home grid, where solved cells fill in like completed squares on a plan sheet.
- Avoid: cream background with terracotta accent, black with a single neon accent, identical rounded cards with soft grey shadows everywhere, decorative gradients, entrance animations on every panel.
- All colors are CSS custom properties in `[CSS:TOKENS]`, defined for light and dark (`:root`, `@media (prefers-color-scheme: dark)`, `[data-theme]` overrides).
- Copy: plain verbs, active voice. Buttons say what happens: "Check design", "Show solution", "Next challenge". Error and empty states say what to do next.

---

## 11. Persistence (`APP:STORE`)

- Keys:
  - `blockprint.v1.settings`
  - `blockprint.v1.pack` — the last valid pack's raw JSON text plus `{ id, version, loadedAt }`, so `file://` players don't re-pick the file every visit. If it's too big for storage, skip caching and say so once.
  - `blockprint.v1.progress.<packId>` — progress is per pack, so loading a different pack never mixes or wipes results.
  - `blockprint.v1.draft.<packId>.<challengeId>`
- Every read/write wrapped in `try/catch`; if storage is unavailable, fall back to memory and show a one-line notice.
- Stored objects carry `schema: 1`. Migrations live in one `migrate()` function; never silently drop user data.
- Progress: `{ schema, packVersion, solved: { c001: { stars, bestAt, checks } }, hintsSeen: {…} }`.
- **Pack updated** (same `id`, new `version`): keep progress for challenge ids that still exist; ignore ids that disappeared (keep them stored, don't show them). Stars already earned are kept even if the new rules are stricter.
- Export/Import: one JSON file with progress + drafts for the current pack. Import validates shape and pack id before replacing anything.
- Diagram export: SVG (serialize the canvas with inlined styles) and PNG (SVG → `Image` → offscreen `<canvas>` → `toBlob`), downloaded via an `<a download>` link.

---

## 12. Testing

`tests/load-core.mjs` reads `index.html`, extracts the text of `<script id="core">`, runs it in a `node:vm` context and returns `Core`. This is why `core` must never touch the DOM. `tests/load-pack.mjs` reads `challenges.json` (or the path passed to `run.mjs`) as text and hands it to `Core.parsePack`, exactly like the browser does.

Engine tests (no dependence on the official pack — use `tests/fixtures/`):

1. **Catalog**: unique types, every prop has a valid default, every glyph and blurb present, every group type exists.
2. **Validator** unit tests for each check type, including cycles, nested groups, replication edges ignored by `path`.
3. **Score** formula edge cases.
4. **Pack loader**: valid fixture loads; each broken fixture (bad JSON, wrong `format`, newer `schema`/`engine`, unknown block type, unknown prop, unknown check type, unknown role, dangling edge, duplicate ids, missing concept, bad chapter reference) is rejected with the expected error path; unknown fields only warn.
5. **Isolation**: `index.html` contains no challenge content — the test asserts `<script id="core">` defines no challenge arrays and the only embedded challenge is the demo.

Pack tests (run against `challenges.json`, or any pack path):

6. **Schema** (`Core.checkPack`) passes with zero errors.
7. **Official pack shape** (only for `challenges.json`): 100 challenges, ids `c001…c100` unique and in order, 10 chapters of 10, one boss per chapter.
8. **Expected results**, per challenge (`Core.runPackSelfTest`):
   - empty diagram → not solved;
   - `expected.solution` → solved with 3 stars (including budget);
   - every `expected.alt` → solved;
   - every `expected.naive` → not solved, and each listed rule id fails;
   - with `start`, the unmodified start diagram → not solved;
   - the palette contains every block type the solution uses.

`?selftest` in the browser runs 6 and 8 on the loaded pack and shows a pass/fail table — the quick way to check a pack after editing it.

A task is done only when `node tests/run.mjs` passes, the page works when served over HTTP, and it works from `file://` after picking `challenges.json`, with no console errors.

---

## 13. Build order

Work in milestones; commit after each. Do not start a milestone before the previous one passes its tests.

1. **Shell + tokens**: layout, themes, routing, empty views.
2. **Core engine**: catalog, pack parser + `checkPack`, compact diagram expansion, graph helpers, validator, score, layout, self-test — plus the Node test harness and `tests/fixtures/` (a 3-challenge valid pack and the broken packs).
3. **Pack loading**: fetch → cache → load screen with picker and drag-and-drop, error list, demo challenge.
4. **Editor**: blocks, edges, groups, pan/zoom, selection, properties, undo/redo, autosave.
5. **Challenge loop**: brief, Check, results sheet, hints, estimates, trade-off, show solution, progress per pack, unlocks, home grid.
6. **Content, chapter by chapter** — only `challenges.json` changes: author 10 challenges at a time, run `node tests/run.mjs` after each chapter. If a chapter needs a block or check type the engine lacks, stop, add it to the engine with tests, then continue the content.
7. **List mode + accessibility pass.**
8. **Glossary, export/import, PNG/SVG export, `?selftest` overlay.**
9. **Polish**: responsive drawers, empty/error states, performance with 60+ nodes.

### Authoring checklist for each challenge (in `challenges.json`)

- Brief reads like a real product request; numbers are realistic.
- Required rules test concepts, not layouts; at least one `expected.alt` when more than one design is reasonable, and at least one `expected.naive` for boss challenges.
- Hints teach without giving the answer away.
- `learn` names the trade-off, not just the pattern.
- Every estimate has its arithmetic in `workings`.
- Solution fits the budget with some slack.
- Every concept id exists in the pack's `concepts`.
- The file stays valid, pretty-printed JSON (2-space indent), one challenge after another in chapter order, so diffs stay readable.

---

## 14. Conventions

- 2-space indent, semicolons, `const` by default, small pure functions in `core`.
- Names: `camelCase` for functions/vars, `UPPER_SNAKE` for data tables, kebab-case for block types and concept ids.
- No global variables except `Core` and `Blockprint`.
- Comments explain *why*; sections use banner comments.
- Keep `index.html` navigable: when a section grows past ~800 lines, split it into sub-banners (`/* ---- [APP:EDITOR:EDGES] ---- */`).
- Challenge content changes go to `challenges.json` only; engine changes go to `index.html` only. A single task that needs both (new block type + challenge using it) lands the engine change and its tests first.
- Never change a challenge `id`, block `type`, prop key or check type once shipped without a migration (engine) or a new pack `version` (content).
