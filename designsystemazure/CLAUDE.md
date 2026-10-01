# CLAUDE.md — Cirrus

> Working name: **Cirrus** (rename freely, but keep "Azure" out of the product name and logo, see §1.8). A browser platform for **Azure architecture challenges**. Each challenge describes a product with requirements. The player solves it by building an **Azure architecture diagram** on a canvas (Front Door, App Service, Container Apps, Service Bus, Cosmos DB, Key Vault…), placed inside regions, availability zones, virtual networks and subnets. The diagram is checked automatically against the challenge's expected result. It gets 1–3 stars plus a scorecard for the five Well-Architected pillars.
>
> **Platform and content are separate.**
> - `index.html` is the **platform**: it loads a challenge pack, shows the challenge text, renders and edits the diagram, runs the diagram logic, checks Azure constraints, and validates the answer. It contains **no challenge content**.
> - `challenges.json` is the **content**: all challenges (text, numbers, rules, expected solutions), chapters and glossary. The official pack holds 100 challenges, but the platform must work with any valid pack.

Read this whole file before writing code. When a decision here conflicts with a later request from the user, the user wins. Then update this file.

> **Current status (user decisions, 2026-10-01)**
> - Platform milestones 1–5 and 7–9 are implemented in `index.html`, adapted from the Blockprint platform (`../designsystem`): Azure catalog (77 blocks, 7 group types), the §4.7 constraints, cost, pack parser/schema, validator with `inGroup` and `zoneResilient`, pillar scorecard, AZ-band layout, editor with live constraint badges and a cost meter, list mode, glossary tabs, export/import, `?selftest`.
> - **Only one challenge for now**: `challenges.json` (pack `version` 0.1.0) holds `c001` "Hello, Azure" and chapter `ch01` only. It passes `?selftest` with 0 errors, 0 warnings. It uses an explicit `palette` array (with decoys) instead of `"chapter"`. Add the rest of the plan in §8 chapter by chapter.
> - **No unit tests for now**: the `tests/` folder (§2, §12) is deferred. Until it exists, check a pack with `index.html?selftest` and check the page by hand over HTTP and from `file://`. The "`node tests/run.mjs` must pass" rules apply once the tests are written.
> - Deviations from the text below: clients and external actors have `cost: 0` and `scope: 'external'` (they are not Azure resources); block flags `count`, `cheapWhen`, `zoneRedundantWhen` and `globalWhen` drive count props, the scale-to-zero floor, configuration-based zone redundancy (e.g. Blob ZRS, PostgreSQL zone-redundant HA) and the cross-region Load Balancer being global; group compact rows are `[id, type, members, props?, label?]`; concepts require `kind`.
> - Constraint `verified` dates (2026-10-01) were set from model knowledge, **not** re-checked live on Microsoft Learn. Re-verify each one (§4.7, §8.1) before relying on them.

---

## 0. What makes this an Azure platform

The architecture follows a proven pattern (two runtime files, declarative rules, compact diagrams, auto-layout). These parts are specific to Azure and are the point of the project:

1. **Every block is an Azure service** or an external actor (client, internet, on-premises, third-party API). There are no generic "cache" or "queue" blocks: the player picks Azure Managed Redis, Service Bus, Event Hubs, Storage queues…
2. **SKUs and tiers matter.** A `tier`/`plan`/`sku` prop changes the block's cost and unlocks features, as in Azure.
3. **Azure constraints.** The engine knows combinations Azure itself refuses (e.g. Cosmos DB strong consistency with multi-region writes). The editor flags them; a diagram with one can't be solved (§4.5).
4. **Zone redundancy is configuration or placement.** Some services become zone-redundant through a setting, others through instances spread across zones. The `zoneResilient` check handles both (§6.3).
5. **Identity travels on edges.** Edge `auth` (`key`, `connection-string`, `sas`, `managed-identity`…) teaches passwordless design.
6. **Network exposure is explicit.** `publicAccess` and `privateEndpoint` props, VNets, subnets with a purpose, and an AKS cluster group.
7. **Every rule names a Well-Architected pillar.** The results sheet shows a pillar scorecard.
8. **The glossary includes the cloud design patterns** from the Azure Architecture Center (Valet Key, Sequential Convoy, Deployment Stamps, Claim-Check…).
9. **Accuracy discipline.** Azure renames and retires services often. §8.1 says how to keep content correct.

If you start from the code of an earlier diagram-challenge platform: copy its `index.html`, keep the editor, validator, layout, store and loader, replace the catalog with §4, add the features above, and rename storage keys and the pack `format`. Packs from other platforms are not compatible.

---

## 1. Hard constraints (never break these)

1. **Two runtime files, with strict roles.**
   - `index.html` = platform. All HTML, CSS and JS live inside it. It must never contain challenge text, rules or solutions (a tiny built-in demo challenge for the "no pack loaded" screen is the only exception).
   - `challenges.json` = content. Pure JSON data: no code, no functions, no HTML, no expressions to evaluate.
2. **Zero external packages.** No npm dependencies, no CDNs, no web fonts, no remote images, no frameworks, no bundler, no transpiler. Only what the browser ships.
3. **Works when hosted and when opened from `file://`.** Browsers block `fetch()` of local files on `file://`, so the platform loads the pack in this order (§5.1): `fetch` next to the page → last pack cached in the browser → the player picks or drops the JSON file. No ES module imports from other files, no service worker requirement.
4. **Vanilla JS (ES2020), classic `<script>` tags**, `'use strict'`. No `eval`, no `new Function`, no `innerHTML` with pack or user text (use `textContent` or the safe text renderer in §5.4). Pack content is untrusted data.
5. **Tests live outside the product files** (`tests/`) and run with plain Node built-ins (`node:test`, `node:assert`, `node:vm`, `node:fs`). No npm install, ever.
6. **The diagram is SVG.** No `<canvas>` for the editor, no `foreignObject` inside the diagram SVG (it breaks PNG export).
7. Target: latest Chrome, Firefox, Safari, Edge; desktop first, fully usable on tablet; usable (list mode) on phone.
8. **Trademarks and icons.** Azure and Microsoft service names describe the subject and may appear in content, labels and the glossary. They never appear in the product name, logo or favicon. The load screen and Settings carry one line: "Independent learning project. Not affiliated with or endorsed by Microsoft." Never copy, trace or embed the official Azure architecture icons. Glyphs are original, simple, drawn by code.
9. **No real prices and no real SLA figures in the engine.** Cost is an abstract 1–5 scale (§4.6). SLAs, limits and throughput figures appear only in a challenge's `numbers`, phrased as assumptions ("Assume the app tier offers 99.95%").

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
    ├── catalog.test.mjs
    ├── constraints.test.mjs
    ├── validator.test.mjs
    ├── cost.test.mjs
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
    /* ==== [CSS:EDITOR] ==== */      canvas, nodes, edges, groups, constraint badges
    /* ==== [CSS:COMPONENTS] ==== */  buttons, forms, dialogs, toasts, pillar bars
  </style>
</head>
<body>
  <!-- ==== [HTML:SHELL] ==== -->     static skeleton only; views are rendered by JS

  <script id="core">
    // Pure logic. NO DOM, NO window, NO localStorage. Must run in node:vm.
    // No challenge content here — it all comes from the pack.
    /* ==== [CORE:VERSION] ==== */     ENGINE_VERSION (integer), supported pack schema versions
    /* ==== [CORE:CATALOG] ==== */     Azure blocks, group types, edge kinds, edge props, PILLARS
    /* ==== [CORE:CONSTRAINTS] ==== */ AZURE_CONSTRAINTS table + checkConstraints(diagram, CATALOG)
    /* ==== [CORE:COST] ==== */        cost(diagram, CATALOG): base × tier multiplier × instances
    /* ==== [CORE:PACK] ==== */        parsePack(text) → { pack, errors, warnings }; normalize defaults; expand compact diagrams
    /* ==== [CORE:SCHEMA] ==== */      checkPack(pack): structure, references, versions
    /* ==== [CORE:GRAPH] ==== */       selectors, adjacency, paths, group membership
    /* ==== [CORE:VALIDATOR] ==== */   rule engine, scoring, pillar scorecard
    /* ==== [CORE:LAYOUT] ==== */      auto-layout for expected solutions (zones as bands, §7)
    /* ==== [CORE:SELFTEST] ==== */    runPackSelfTest(pack): expected-result checks per challenge
    /* ==== [CORE:EXPORT] ==== */      exports to globalThis.Core
  </script>

  <script id="app">
    // Browser-only code. Reads Core, never duplicates its logic.
    /* ==== [APP:STORE] ==== */        localStorage wrapper, schema version, migrations
    /* ==== [APP:LOADER] ==== */       ?pack / fetch challenges.json → cache → file picker / drag-drop; demo challenge
    /* ==== [APP:STATE] ==== */        app state, undo/redo stack
    /* ==== [APP:ROUTER] ==== */       hash routes
    /* ==== [APP:EDITOR] ==== */       SVG canvas, pointer interactions, constraint badges, cost meter
    /* ==== [APP:PANELS] ==== */       palette, properties, brief, results, pillar scorecard
    /* ==== [APP:VIEWS] ==== */        home grid, workspace, glossary, settings
    /* ==== [APP:EXPORT] ==== */       JSON import/export, PNG/SVG export
    /* ==== [APP:SELFTEST] ==== */     ?selftest overlay
    /* ==== [APP:BOOT] ==== */
  </script>
</body>
</html>
```

`Core` is the single shared global: `globalThis.Core = { ENGINE_VERSION, CATALOG, PILLARS, AZURE_CONSTRAINTS, parsePack, checkPack, expandDiagram, checkConstraints, cost, validate, score, pillarScorecard, layout, runPackSelfTest }`. Core never holds a pack in global state; every function receives the pack or challenge it works on. For debugging also expose `window.Cirrus = { Core, state }` (the loaded pack lives in `state.pack`).

**Who owns what:** the engine owns the *vocabulary* (Azure block types, props, tiers, edge kinds, group types, constraints, check types, pillars). The pack owns the *content* (chapters, challenges, concepts, rules, expected solutions) and may only use that vocabulary. A pack that references an unknown block type, prop, option or check type is rejected with a clear error.

---

## 4. Data model

### 4.1 Diagram (what the player builds, what gets saved)

```js
{
  v: 1,
  nodes:  [{ id: 'n1', type: 'container-apps', x: 320, y: 160, label: 'Links API',
             props: { minReplicas: 2, maxReplicas: 20, scaleRule: 'http', identity: 'system', role: 'links' } }],
  edges:  [{ id: 'e1', from: 'n1', to: 'n2', kind: 'sync',
             props: { protocol: 'HTTPS', auth: 'managed-identity', private: true,
                      timeoutMs: 2000, retries: 2, backoff: true, jitter: true, circuitBreaker: false } }],
  groups: [{ id: 'g1', type: 'region', x: 40, y: 40, w: 900, h: 500, label: 'West Europe', props: {} },
           { id: 'g2', type: 'subnet', x: 80, y: 120, w: 400, h: 200, label: 'snet-app',
             props: { purpose: 'app', nsg: true } }]
}
```

- Edges are **directed**: caller → callee, producer → consumer, primary → replica.
- **Group membership is geometric**: a node belongs to every group whose rectangle contains the node's center. Groups can nest and can overlap (see §4.4: subnets cross availability zones).
- Groups have `props` (unlike nodes, they have no cost).
- Node `label` is cosmetic. Rules never depend on labels.
- Ids are generated (`n`/`e`/`g` + counter) and never reused inside one diagram.

### 4.2 Edge kinds

| kind | Meaning | Style |
|---|---|---|
| `sync` | Request/response call (HTTPS, gRPC, SQL, Redis) | solid line, filled arrow |
| `async` | Message/event (send, publish, trigger, consume) | dashed line, open arrow |
| `replication` | Data copied from source to replica, standby or secondary region | dotted line, double arrow head |
| `data` | Bulk/batch data flow (ETL, backup, capture, upload, copy) | thick line, filled arrow |

When the player connects two blocks, preselect the most likely kind: anything → `service-bus-queue`, `service-bus-topic`, `event-hubs`, `event-grid`, `storage-queue` is `async`; messaging → consumer is `async`; database → `read-replica` is `replication`; `data-factory`, `azure-backup`, `site-recovery` outgoing is `data`. The player can change it.

### 4.3 Edge props

| key | on kinds | values | teaches |
|---|---|---|---|
| `protocol` | sync | HTTPS, gRPC, AMQP, SQL, Redis, TCP, UDP, SMB | L4 vs L7 choices |
| `auth` | all | none, key, connection-string, sas, entra-token, managed-identity | passwordless access, Valet Key |
| `private` | all | bool: traffic goes over Private Link / a private endpoint | network isolation |
| `timeoutMs`, `retries`, `backoff`, `jitter`, `circuitBreaker` | sync | numbers/bools | transient fault handling |
| `source` | async, from Service Bus | main, deadletter | processing the dead-letter subqueue |

### 4.4 Group types

| type | props | notes |
|---|---|---|
| `management-group` | — | governance; outermost |
| `subscription` | `role`: platform/connectivity/identity/management/landing-zone | governance |
| `region` | — | label is the region name; regional blocks live inside one |
| `availability-zone` | `n`: 1/2/3 | **overlaps** VNets and subnets (in Azure, VNets and subnets are regional and span zones) |
| `vnet` | `role`: hub/spoke/standalone | inside a region |
| `subnet` | `purpose`: app/data/integration/aks/gateway/firewall/bastion/private-endpoints; `nsg` bool; `routeToFirewall` bool | inside a vnet |
| `aks` | `tier`: Free/Standard; `nodePools` int; `clusterAutoscaler`, `workloadIdentity`, `networkPolicy` bools; `zones` | a cluster; contains `k8s-deployment` blocks; sits in a subnet |

Resource groups are logical, not spatial, so they are not modelled.

### 4.5 Block catalog (`CORE:CATALOG`)

Each entry: `{ type, label, aka, category, scope, cost, costBy, glyph, blurb, props }`.

- `label` = current Azure name. `aka` = former names and common aliases (searchable in the palette, listed in the glossary). Example: `entra-id` → label "Microsoft Entra ID", aka ["Azure Active Directory", "Azure AD"].
- `scope`: `global` (never inside a `region` group: Front Door, Traffic Manager, Azure DNS, Entra ID, cross-region Load Balancer) or `regional`. A global block inside a region gets a constraint warning.
- `cost` 1–5, `costBy` tier multipliers (§4.6).
- `glyph` = tiny original inline SVG path drawn by code. `blurb` = one plain sentence on what the service is for.
- `props` = schema `{ key: { kind: 'bool'|'int'|'enum'|'text', options?, default, min?, max?, label } }`.

Common props, added only where Azure supports them:
- `zones`: `none` | `zonal` | `zone-redundant`.
- `publicAccess` (bool, default `true` as in Azure) and `privateEndpoint` (bool) on PaaS services.
- `identity`: `none` | `system` | `user-assigned` on anything that calls other services.
- `role`: enum from the challenge's `roles` (compute blocks and `k8s-deployment`), shown as a dropdown so rules never depend on typed names.
- `tracing` (bool) on compute: emits OpenTelemetry traces with correlation ids.

| Category | Types (and key props) |
|---|---|
| Clients & external | `client-web`, `client-mobile`, `iot-device`, `internet`, `on-premises`, `third-party-api`, `payment-provider`, `saas-app`, `push-platform` (APNs/FCM) |
| Global edge & DNS | `azure-dns` (zone: public/private), `traffic-manager` (routing: priority/weighted/performance/geographic, endpointMonitor), `front-door` (tier: Standard/Premium, caching, routing: latency/priority/weighted, waf: none/custom/managed, privateLinkOrigin), `ddos-protection` |
| Networking | `load-balancer` (sku: Standard/Gateway, scope: public/internal, crossRegion, healthProbe, zones), `application-gateway` (waf, pathRouting, autoscale, zones), `azure-firewall` (tier: Basic/Standard/Premium, zones), `nat-gateway`, `vpn-gateway` (activeActive), `expressroute` (redundantCircuits), `bastion`, `private-dns-zone`, `virtual-wan` |
| Compute | `virtual-machine` (zones), `vmss` (instances, autoscale, minInstances, maxInstances, zones), `app-service` (tier: Basic/Standard/PremiumV3/IsolatedV2, instances, autoscale, zones, arrAffinity, deploymentSlots, vnetIntegration, tls, managedCert, easyAuth), `static-web-apps`, `functions` (plan: FlexConsumption/Premium/Dedicated/Consumption, trigger: http/timer/queue/blob/event-grid/event-hubs/cosmos-change-feed, idempotent), `durable-functions` (pattern: chaining/fan-out-fan-in/async-http/monitor/human-interaction, compensation), `container-apps` (minReplicas, maxReplicas, scaleRule: http/service-bus/event-hubs/cron/cpu, dapr, ingress: external/internal/none, zones), `container-apps-job` (trigger: manual/schedule/event, parallelism), `k8s-deployment` (replicas, hpa, sidecar), `container-instances`, `batch` (pool nodes, lowPriority), `container-registry` (tier: Basic/Standard/Premium, geoReplication), `logic-apps` (plan: Consumption/Standard) |
| Data | `sql-database` (tier: GeneralPurpose/BusinessCritical/Hyperscale, elasticPool, zones, readScaleOut, failoverGroup, ltr, cmk, isolation, locking: none/optimistic/pessimistic, appendOnly), `sql-managed-instance` (tier, failoverGroup), `postgresql-flexible` (ha: none/same-zone/zone-redundant), `mysql-flexible` (ha), `read-replica`, `cosmos-db` (api: NoSQL/MongoDB/Cassandra/Gremlin/Table, consistency: strong/bounded-staleness/session/consistent-prefix/eventual, partitionKey text, hierarchicalKey, throughput: provisioned/autoscale/serverless, regions int, multiRegionWrites, conflictResolution: lww/custom, changeFeed, zones), `redis` (label "Azure Managed Redis"; tier, strategy: cache-aside/read-through/write-through/write-behind, eviction: LRU/LFU/noeviction, ttlJitter, stampedeProtection, geoReplication, zones), `blob-storage` (redundancy: LRS/ZRS/GRS/GZRS/RA-GRS/RA-GZRS, accessTier: hot/cool/cold/archive, lifecycle, versioning, immutability, staticWebsite, cmk), `data-lake` (Data Lake Storage, hierarchical namespace; redundancy), `azure-files` (tier: Standard/Premium, redundancy), `table-storage` |
| Analytics & AI | `ai-search` (replicas, partitions, vector, semanticRanker, indexer), `data-explorer`, `data-factory`, `stream-analytics` (windowing: tumbling/hopping/sliding/session), `databricks`, `fabric` (warehouse/lakehouse), `azure-openai` (label tracks the current Foundry name; deployment: standard/provisioned), `document-intelligence` |
| Messaging & integration | `service-bus-queue` (tier: Basic/Standard/Premium, sessions, duplicateDetection, maxDeliveryCount, scheduled, maxSizeGb), `service-bus-topic` (tier, subscriptions, filters), `event-hubs` (tier: Basic/Standard/Premium/Dedicated, partitions, consumerGroups, capture, retentionDays), `event-grid` (source: system/custom, filters, deadLetter), `storage-queue`, `api-management` (tier: Consumption/Developer/BasicV2/StandardV2/Premium, rateLimit, validateJwt, cache, versioning, multiRegion), `web-pubsub` (units), `signalr` (units), `notification-hubs`, `communication-services` (channels: email/sms/both), `iot-hub` (tier, partitions) |
| Identity, security, governance | `entra-id`, `entra-external-id`, `key-vault` (softDelete, purgeProtection, rbac), `defender-for-cloud`, `sentinel`, `azure-policy` (effect: audit/deny/deployIfNotExists) |
| Observability | `app-insights`, `log-analytics` (retentionDays), `monitor-alerts` (actionGroup: email/sms/webhook), `managed-grafana` |
| Backup & DR | `azure-backup` (vault redundancy: LRS/ZRS/GRS), `site-recovery` |

Rules:
- The catalog is engine code, not pack content: packs can't add block types. A challenge that needs a new service requires an engine change and an `ENGINE_VERSION` bump.
- Adding a block type means: catalog entry + glyph + blurb + default props (Azure's own defaults where they exist) + a catalog test.
- `type` and prop keys are stable forever. **When Azure renames a service, change `label` and push the old name into `aka`. Never change `type`.**
- `k8s-deployment` must sit inside an `aks` group (constraint). Kubernetes is modelled as a cluster group with workloads inside, not as one opaque block.

### 4.6 Cost

Teaches SKU trade-offs without real prices.

```
nodeCost = cost × (costBy[prop][value] multiplier, product over props, default 1)
                × (instances | replicas | minReplicas | partitions | regions, whichever the block declares as its count, default 1)
total    = Σ nodeCost
```

Example: `app-service` `{ cost: 2, costBy: { tier: { Basic: 0.5, Standard: 1, PremiumV3: 2, IsolatedV2: 4 } } }` with 3 instances on PremiumV3 = 12. `minReplicas: 0` on Container Apps or `throughput: serverless` on Cosmos DB costs a fixed 0.5 (scale-to-zero is cheap, not free). The editor toolbar shows a live cost meter against the challenge `budget`. One function, `Core.cost`, covered by tests.

### 4.7 Azure constraints (`CORE:CONSTRAINTS`)

Combinations that Azure itself refuses or that make no sense. Table entries: `{ id, types, when, require, message, ref, verified }`. `ref` names the Microsoft Learn page title (text only); `verified` is the date the rule was last checked against it.

Starting set (true as of mid-2026, **verify each on Microsoft Learn before implementing and record the date**):

| id | block | rule | message |
|---|---|---|---|
| `cosmos-strong-multiwrite` | cosmos-db | consistency `strong` ⇒ `multiRegionWrites` false | Strong consistency isn't available with multiple write regions. |
| `cosmos-multiwrite-regions` | cosmos-db | `multiRegionWrites` ⇒ `regions` ≥ 2 | Multi-region writes need at least two regions. |
| `afd-private-origin` | front-door | `privateLinkOrigin` ⇒ tier Premium | Private Link origins need Front Door Premium. |
| `afd-managed-waf` | front-door | waf `managed` ⇒ tier Premium | Managed WAF rule sets need Front Door Premium. |
| `sb-basic-topic` | service-bus-topic | tier ≠ Basic | Service Bus Basic has queues only. |
| `sb-basic-features` | service-bus-queue | `sessions` or `duplicateDetection` ⇒ tier ≠ Basic | Sessions and duplicate detection need Standard or Premium. |
| `sb-private` | service-bus-* | `privateEndpoint` ⇒ tier Premium | Private endpoints for Service Bus need Premium. |
| `eh-basic-capture` | event-hubs | `capture` ⇒ tier ≠ Basic | Event Hubs Capture isn't in the Basic tier. |
| `blob-archive-redundancy` | blob-storage, data-lake | accessTier `archive` ⇒ redundancy ∈ LRS/GRS/RA-GRS | The archive tier isn't supported with zone-redundant storage. |
| `app-basic-autoscale` | app-service | `autoscale` ⇒ tier ≠ Basic | Basic plans scale manually only. |
| `app-slots` | app-service | `deploymentSlots` ⇒ tier ≠ Basic | Deployment slots start at Standard. |
| `app-zones` | app-service | zones `zone-redundant` ⇒ tier ∈ PremiumV3/IsolatedV2 | Zone redundancy needs a Premium or Isolated plan. |
| `apim-multiregion` | api-management | `multiRegion` ⇒ tier Premium | Multi-region gateways need API Management Premium. |
| `k8s-in-cluster` | k8s-deployment | must be inside an `aks` group | Kubernetes workloads run inside an AKS cluster. |
| `global-in-region` | any `scope: global` | must not be inside a `region` group | This service is global, not regional. (warning only) |

Behaviour:
- `checkConstraints(diagram, CATALOG) → [{ nodeId, id, severity: 'error'|'warning', message }]`.
- The editor shows a small badge on the node and a line in the properties panel: "Azure would reject this: …".
- `validate()` reports constraint errors first. **A diagram with a constraint error is not solved**, whatever the rules say ("Azure won't deploy this design").
- `checkPack` rejects any `expected.solution` or `expected.alt` with constraint errors. `naive` diagrams may contain them (teaching material).

---

## 5. The challenge pack (`challenges.json`)

### 5.1 Loading (`APP:BOOT` + `CORE:PACK`)

On start, the platform tries these sources in order and uses the first that works:

1. `?pack=<url>` query parameter (same-origin URL or relative path), else `challenges.json` next to `index.html`, via `fetch` with `cache: 'no-cache'`. This is the normal path when hosted.
2. The last successfully loaded pack cached in the browser (`cirrus.v1.pack`). If step 1 also succeeded and the fetched pack's `id` + `version` differ from the cache, the fetched one wins and replaces the cache.
3. **Load screen**: shown when nothing above worked (typical on `file://`). It explains why in one sentence and offers "Choose challenges file" (`<input type="file" accept=".json,application/json">`) plus drag-and-drop anywhere on the page. A small built-in demo challenge lets people try the editor without a pack.

Settings has "Load another pack" and shows the current pack's title, version and challenge count.

Every source goes through the same path: `text → Core.parsePack(text) → { pack, errors, warnings }`.
- `JSON.parse` errors are reported with line/column when the browser provides a position.
- Any error → the pack is rejected, the previous pack (if any) stays active, and the load screen lists the errors (path + message, e.g. `challenges[19].expected.rules[2].check.path[1]: unknown block type "cosmosdb"`).
- Warnings (e.g. a concept never used) are shown in `?selftest` and the console, not to players.
- Loading never executes anything from the file.

### 5.2 Top-level structure

```json
{
  "format": "cirrus-pack",
  "schema": 1,
  "engine": 1,
  "id": "azure-100",
  "title": "Azure Architecture: 100 Challenges",
  "version": "1.0.0",
  "language": "en",
  "verified": "2026-10",
  "chapters": [
    { "id": "ch01", "n": 1, "title": "Azure foundations", "intro": "Regions, App Service, Azure SQL Database and Blob Storage." }
  ],
  "concepts": [
    { "id": "valet-key", "kind": "pattern", "term": "Valet Key",
      "def": "Give clients a short-lived, scoped token (a SAS) so they upload or download directly from storage instead of through your app.",
      "blocks": ["blob-storage"], "ref": "Valet Key pattern — Azure Architecture Center" }
  ],
  "challenges": [ ]
}
```

- `format` must equal `"cirrus-pack"`; `schema` is the pack format version; `engine` is the minimum `ENGINE_VERSION` needed. A newer `schema` or `engine` than the platform supports → "This pack needs a newer version of the platform."
- `verified` is the month the content was last checked against Azure documentation; shown in Settings.
- `id` + `version` identify the pack for caching and progress (§11).
- `chapters` order is display order. Challenge order inside a chapter is array order in `challenges`.
- `concepts` is the glossary. `kind`: `service` | `pattern` | `principle` | `pillar`. `blocks` must be catalog types. `ref` is an optional plain-text title of the Microsoft Learn / Architecture Center page (no URLs).

### 5.3 Challenge object

```json
{
  "id": "c020",
  "chapter": "ch02",
  "title": "Short links, long life",
  "difficulty": 3,
  "boss": true,
  "concepts": ["estimation", "request-units", "cosmos-partition-key", "cache-aside", "front-door"],
  "brief": "A marketing team wants short links like go.example/Ab3x that redirect instantly and never break.",
  "functional": ["Create a short link", "Redirect to the original URL"],
  "nonFunctional": ["Redirect p99 under 50 ms", "100:1 reads to writes", "Survive the loss of one availability zone"],
  "numbers": [
    { "label": "New links per month", "value": "100M" },
    { "label": "Read/write ratio", "value": "100:1" },
    { "label": "Assume a 1 KB point read costs", "value": "1 RU" }
  ],
  "palette": "chapter",
  "roles": ["links"],
  "start": null,
  "locked": [],
  "budget": 30,
  "estimate": [
    { "q": "Reads per second (average)?", "answer": 3858, "unit": "reads/s", "tolerance": 0.5,
      "workings": "100M × 100 / (30 × 86,400 s) ≈ 3,858" },
    { "q": "RU/s for those reads with no cache?", "answer": 3858, "unit": "RU/s", "tolerance": 0.5,
      "workings": "3,858 point reads/s × 1 RU ≈ 3,858 RU/s" }
  ],
  "tradeoff": {
    "q": "Why is /code a good partition key for the links container?",
    "choices": ["…", "…", "…"], "correct": 0, "why": "One sentence."
  },
  "learn": "2–4 sentences: the takeaway and its trade-off, shown on the results sheet.",
  "expected": {
    "rules": [ ],
    "solution": { },
    "alt": [ ],
    "naive": [ { "diagram": { }, "fails": ["r-cache"] } ]
  }
}
```

Field notes:
- `id`: unique, never changes once published (progress is keyed by it). Official pack: `c001`…`c100`.
- `difficulty`: 1–5. `boss`: optional, at most one per chapter; it unlocks the next chapter.
- `palette`: `"chapter"` (every block used by expected solutions of this and earlier chapters), `"all"`, or an explicit array of block types.
- `start` / `locked`: optional pre-filled diagram (compact form, §7) for "fix this architecture" challenges; `locked` lists node ids that cannot be deleted.
- `estimate[].tolerance` is relative (0.5 = ±50%). `workings` is shown after answering. Every input to an estimate is in `numbers` as an assumption.
- **`expected` is the answer key**:
  - `rules`: what the player's diagram is judged against (§6). Never compare shapes with `solution`; many designs are valid.
  - `solution`: the reference design. Shown by "Show solution"; self-tests prove the rules are satisfiable with it.
  - `alt`: other designs that must also pass (e.g. Container Apps instead of App Service).
  - `naive`: common wrong answers and the rule ids that must fail for each.
- Unknown fields are a **warning**, not an error. `_`-prefixed fields (authoring notes) don't warn.

### 5.4 Text in the pack

Text fields (`brief`, `learn`, `def`, `intro`, `hint`, `why`, rule `text`) are plain text with a tiny safe subset rendered by `renderText()` in `APP:PANELS`: `**bold**`, `` `code` ``, and blank-line paragraphs. No HTML, no links, no images. The renderer builds DOM nodes with `textContent`; anything else is shown literally.

Briefs describe products generically ("like a popular photo app"); they never name real companies other than Microsoft/Azure services.

---

## 6. Rule engine (`CORE:VALIDATOR`)

Rules come from the pack as **declarative JSON data**; the engine owns the meaning of every check type. The validator is a pure function:
`validate(diagram, challenge, CATALOG) → { constraints: [...], rules: [{ id, pass, severity, pillar, text, hint, detail }] }`.

`checkPack` validates every rule on load: known check type, required fields, selectors reference catalog types/props/valid options, roles exist in `challenge.roles`, rule ids unique, `pillar` valid.

### 6.1 Rule shape

```json
{ "id": "r-cache",
  "severity": "required",
  "pillar": "performance",
  "text": "Hot links are served from a cache",
  "hint": "Redirects repeat the same few keys all day.",
  "weight": 1,
  "check": { "path": [ { "type": ["app-service", "container-apps"] }, { "type": "redis" } ] } }
```

- `severity`: `required` (all must pass to solve), `bonus` (extra stars), `warn` (anti-pattern detector; never blocks solving; shows an explanation, e.g. a connection string where a managed identity fits).
- `pillar` (required): `reliability` | `security` | `cost` | `operations` | `performance` — the Well-Architected pillars, defined in `Core.PILLARS` with display names.
- `weight` applies to bonus rules only (default 1).

### 6.2 Selectors

```js
{ type: 'redis' }
{ type: ['app-service', 'container-apps', 'functions'] }       // any of
{ category: 'data' }
{ type: 'k8s-deployment', props: { role: 'order' } }
{ type: 'cosmos-db', props: { consistency: ['session', 'bounded-staleness'] } }  // array = any of
```

### 6.3 Checks

| Check | Meaning |
|---|---|
| `{ count: sel, min?, max? }` | number of matching nodes in range |
| `{ capacity: sel, min }` | sum of the block's count prop (instances/replicas/minReplicas, default 1) ≥ min |
| `{ edge: { from: sel, to: sel, kind? }, min? }` | direct edge(s) exist |
| `{ noEdge: { from: sel, to: sel, kind? } }` | no direct edge |
| `{ path: [sel, sel, …], kind? }` | a directed path visits the selectors in order |
| `{ noPath: { from: sel, to: sel, avoiding?: sel } }` | no path, or no path that avoids a node (e.g. every internet → app path passes the WAF) |
| `{ prop: sel, key, eq?, in?, gte?, lte?, all? }` | some (or all) matching nodes have the prop value |
| `{ edgeProp: { from: sel, to: sel }, key, eq?, in?, gte?, all? }` | same on edges; `all: true` = every matching edge (e.g. every edge into data uses `managed-identity`) |
| `{ inGroup: sel, group: sel }` | every matching node sits in a group matching `group` (selectors may filter group props, e.g. `{ type: 'subnet', props: { purpose: 'private-endpoints' } }`) |
| `{ notInGroup: sel, group: sel }` | no matching node sits in such a group |
| `{ inGroups: sel, group: sel, distinct: n }` | matching nodes appear in ≥ n distinct groups of that type (regions, zones) |
| `{ zoneResilient: sel, all? }` | a node passes if its `zones` prop is `zone-redundant`, or if nodes of the same selector sit in ≥ 2 distinct `availability-zone` groups; `aks` group zones count for its workloads |
| `{ fanOut: sel, min }` | a matching node has ≥ min outgoing edges |
| `{ budget: max }` | `Core.cost` ≤ max |
| `{ all: [checks] }`, `{ any: [checks] }`, `{ not: check }` | combinators |

Path search ignores `replication` edges unless `kind: 'replication'` is given. Guard against cycles.

### 6.4 Scoring and pillar scorecard

```
solved = no constraint errors AND every required rule passes
stars  = 0 if not solved
       = 1 + (bonusWeightPassed ≥ 50% of bonusWeightTotal ? 1 : 0)
           + (all bonus pass AND budget respected ? 1 : 0)
```
Quiz path: only when a challenge has **no** bonus rules, each correct estimate/trade-off answer adds +1 star (cap 3), and the 3rd star also needs the budget respected. Each question gets one answer; answers are stored in progress and can raise a solved challenge's stars later. Stars earned are never lowered. One function, `score()`, covered by tests.

`pillarScorecard(results)` → for each pillar: required + bonus rules passed / total tagged with it, plus the count of failed `warn` rules for that pillar. Shown on the results sheet as five labelled bars with numbers. It never changes stars; it shows where the design is strong or thin.

### 6.5 Rule-writing principles

- **Test the idea, not the drawing.** Accept every reasonable Azure design. If Front Door caching or an API Management cache both solve it, use `any`. If App Service, Container Apps and AKS all fit, accept all three.
- One rule = one concept. Checklist text states the requirement in plain words, not the check mechanics.
- Every challenge has **3–8 required rules**, **0–4 bonus**, **0–3 warn**, and touches at least two pillars.
- Hints progress: after the 1st failed check show nothing extra, after the 2nd show `hint`, after the 3rd offer "Show a service you're missing" (names one missing block type).

---

## 7. Compact diagrams in the pack and auto-layout

Every diagram inside the pack (`expected.solution`, `expected.alt[]`, `expected.naive[].diagram`, `start`) uses a compact JSON form without coordinates. `Core.expandDiagram(compact, CATALOG)` turns it into the full model of §4.1 (defaults filled, edge kinds inferred, positions from auto-layout):

```json
{
  "n": [["c",  "client-web"],
        ["fd", "front-door", { "tier": "Standard", "caching": true }],
        ["a",  "container-apps", { "minReplicas": 2, "zones": "zone-redundant", "identity": "system", "role": "links" }],
        ["r",  "redis", { "strategy": "cache-aside", "zones": "zone-redundant" }],
        ["db", "cosmos-db", { "api": "NoSQL", "partitionKey": "/code", "consistency": "session", "zones": "zone-redundant" }]],
  "e": [["c", "fd"], ["fd", "a"],
        ["a", "r",  null, { "auth": "managed-identity" }],
        ["a", "db", null, { "auth": "managed-identity" }]],
  "g": [["r1", "region", ["a", "r", "db"], null, "West Europe"]]
}
```

- `n`: `[id, type, props?, label?]`. `e`: `[from, to, kind?, props?]`. `g`: `[id, type, memberIds, props?, label?]`.
- `checkPack` rejects unknown types/props/options, dangling ids, duplicate ids, and constraint errors in solutions/alts.

`CORE:LAYOUT`:
1. Columns by longest-path depth from source nodes (left → right); one barycenter pass to reduce crossings.
2. Global blocks (`scope: global`) and clients stay outside region groups, on the left.
3. Groups wrap their members with padding, inner groups first, in this nesting order: management-group ⊃ subscription ⊃ region ⊃ vnet ⊃ subnet ⊃ aks.
4. **Availability zones are vertical bands**: when a region has AZ groups, split the region's width into equal bands, move each AZ's members into its band, and draw the bands across the VNet/subnet boxes (subnets stay horizontal bands spanning all zones). This matches how Azure reference architectures are drawn and keeps geometric membership correct.

---

## 8. Content plan for the official pack (`challenges.json`)

This is the **authoring plan for the JSON file**, not code. None of it goes into `index.html`. The platform must not assume these numbers.

Ten chapters of ten. The 10th challenge of each chapter is a **boss** combining the chapter. Chapters 1–5 use `"palette": "chapter"`; from chapter 6 on, `"all"`. The order loosely follows the AZ-305 design areas (identity/governance/monitoring, data storage, business continuity, infrastructure), but the pack makes no claim of exam coverage.

### 8.1 Accuracy discipline (read before authoring any challenge)

- Azure changes constantly. **Before writing a chapter, check every service, tier and feature it uses against current Microsoft Learn documentation** (use web search when available) and update the pack's `verified` month.
- No prices, no SLA percentages, no service limits stated as facts. Put them in `numbers` as "Assume…".
- Prefer the current recommended service when Microsoft has one (e.g. Flex Consumption for new Functions apps, Front Door for edge caching, Entra External ID for customer identity).
- When a challenge depends on a tier rule, encode it as an engine constraint (§4.7), not only as challenge text.
- Known renames and retirements to respect (mid-2026; re-verify):
  - Azure Active Directory → **Microsoft Entra ID**.
  - Azure AD B2C → **Microsoft Entra External ID** for new customer-identity designs.
  - Azure Cache for Redis → **Azure Managed Redis** (older tiers are being retired).
  - **Azure Media Services is retired**: transcode with containers (Container Apps jobs or Batch running ffmpeg), store renditions in Blob, deliver through Front Door.
  - Azure CDN classic tiers → **Front Door Standard/Premium**; there is no separate CDN block.
  - **Basic** Load Balancer and Basic public IPs are retired → Standard only (Basic is not an option in the catalog).
  - Azure OpenAI is offered through Azure AI Foundry; the label follows the current name, the type stays `azure-openai`.
  - New analytics designs point to **Microsoft Fabric**; Synapse is not in the catalog.

### Chapter 1 — Azure foundations

| # | Title | Scenario | Must include |
|---|---|---|---|
| 1 | Hello, Azure | Personal portfolio site | Static Web Apps (or App Service) serving the client |
| 2 | Remember my tasks | To-do app that keeps tasks | client → App Service → Azure SQL Database; warn on client → DB |
| 3 | Your own name | Custom domain over HTTPS | Azure DNS zone, App Service with `managedCert` and `tls` |
| 4 | Pictures go in Blob | Recipe site full of photos | images in Blob Storage, API on App Service; storage estimate |
| 5 | Valet key | Users upload profile photos | client → Blob with `auth: sas`, metadata in SQL; app never proxies the bytes |
| 6 | Two instances, one login | Second instance logs users out | App Service capacity ≥2, `arrAffinity` off, session in Redis |
| 7 | Night shift | Nightly sales report email | Functions `timer` trigger → SQL, Communication Services email |
| 8 | Money's in | Payment provider calls a webhook | provider → Functions `http`, `idempotent` |
| 9 | Pick the compute | Containerized API, idle at night, bursts by day | Container Apps `minReplicas` 0 + HTTP scale rule; trade-off VM vs App Service vs Container Apps vs AKS |
| 10 | ★ Pastebin | Share text snippets by link | metadata in SQL or Cosmos DB, bodies in Blob, compute ≥2, Front Door; estimate |

### Chapter 2 — Data & storage

| # | Title | Scenario | Must include |
|---|---|---|---|
| 11 | Read-heavy blog | 100 reads per write | PostgreSQL flexible server + ≥2 read replicas, replication edges, reads hit replicas |
| 12 | Cart in a flash | Cart lookups by user | Cosmos DB for NoSQL, `partitionKey` /userId |
| 13 | Hot partition | One huge tenant gets all writes | high-cardinality or `hierarchicalKey`; RU estimate |
| 14 | Pick a consistency | Editors must see their own edits | Cosmos `consistency` session; trade-off across the five levels |
| 15 | Many small tenants | 300 spiky, mostly idle tenant databases | SQL Database `elasticPool` |
| 16 | Outgrow the box | 40 TB transactional database | SQL Database Hyperscale with read replicas |
| 17 | Lift the file share | Legacy app needs an SMB share | Azure Files (`redundancy` ZRS), `privateEndpoint` |
| 18 | Which letters? | Data must survive a region outage and stay readable | Blob `redundancy` RA-GZRS; trade-off LRS/ZRS/GRS/GZRS |
| 19 | Hot, cool, archive | Logs kept 7 years, rarely read | Blob `lifecycle` to archive with a redundancy that allows it (constraint) |
| 20 | ★ URL shortener | 100M links/month, 100:1 reads | Front Door, compute ≥2, Cosmos DB, Redis, zone-resilient data; RU estimate |

### Chapter 3 — Caching, edge & global entry

| # | Title | Scenario | Must include |
|---|---|---|---|
| 21 | Cache-aside | Slow profile reads | Redis `cache-aside`, app → Redis and app → DB |
| 22 | Static at the edge | Global users load assets slowly | Front Door `caching` in front of Blob |
| 23 | Live leaderboard | Scores read constantly, must be fresh | Redis `write-through` |
| 24 | Stampede | Celebrity key expires, DB melts | `stampedeProtection` + `ttlJitter` |
| 25 | Who gets evicted? | Small memory, some items always hot | Redis eviction `LFU`; trade-off LRU vs LFU |
| 26 | Cache the API | Public product GET endpoints | API Management `cache` |
| 27 | Two regions, one URL | Users in Europe and Asia | Front Door latency routing → two regions; trade-off Front Door vs Traffic Manager |
| 28 | Shield the origin | Attackers bypass the edge and hit App Service | Front Door Premium `privateLinkOrigin`, App Service `publicAccess` off |
| 29 | Block the bad | SQL injection attempts in the logs | Front Door WAF `managed` (needs Premium) |
| 30 | ★ Breaking news | 50× traffic in five minutes | Front Door caching, Redis, autoscale, within budget |

### Chapter 4 — Networking & load balancing

| # | Title | Scenario | Must include |
|---|---|---|---|
| 31 | Two is one | VMs must survive one zone failing | Standard Load Balancer `healthProbe`, VMSS zone-resilient |
| 32 | Route by path | `/api` and `/images` go to different pools | Application Gateway `pathRouting` |
| 33 | Not HTTP | Game server over UDP | Load Balancer (L4), not Application Gateway; trade-off |
| 34 | Private by default | Database reachable from the internet | private endpoints on SQL and Blob, `publicAccess` off, App Service `vnetIntegration` |
| 35 | No public IPs | Admins RDP over the internet | Bastion in a `bastion` subnet, VMs unreachable from `internet` |
| 36 | One outbound IP | Partner allowlists one IP; SNAT exhaustion | NAT Gateway on the app subnet |
| 37 | Hub and spoke | Three apps share one firewall | hub VNet with Azure Firewall, spokes `routeToFirewall`, path spoke → firewall → internet |
| 38 | Office link | Branch office reaches private apps | VPN Gateway (or ExpressRoute) to `on-premises`; trade-off |
| 39 | Global, not HTTP | TCP service in two regions | Traffic Manager or cross-region Load Balancer; decision trade-off |
| 40 | ★ Secure web tier | Public app, private data, inspected egress | Front Door + WAF, app in spoke, firewall hub, private endpoints, Bastion |

### Chapter 5 — Messaging & integration

| # | Title | Scenario | Must include |
|---|---|---|---|
| 41 | Don't make them wait | Signup blocked by welcome email | Service Bus queue + Functions + Communication Services; warn on app → email directly |
| 42 | Tell everyone | Order placed → email, SMS, analytics | Service Bus topic with ≥3 subscribers |
| 43 | Poison pill | One bad message retried forever | `maxDeliveryCount` + consumer on `source: deadletter` |
| 44 | React to uploads | Thumbnails when a photo lands | Event Grid (Blob created) → Functions → Blob |
| 45 | Order matters | Per-account events stay in order | Service Bus `sessions` (Sequential Convoy) |
| 46 | Telemetry flood | Millions of events per minute | Event Hubs `partitions`, `consumerGroups`, `capture` to Data Lake; throughput estimate |
| 47 | No double invoices | Redelivery creates duplicates | `duplicateDetection` + idempotent consumer |
| 48 | Glue the SaaS | New CRM lead → ERP + chat notice | Logic Apps between `saas-app` blocks |
| 49 | Fan out, fan in | Process 10k files, then summarize | Durable Functions `fan-out-fan-in` |
| 50 | ★ Notification system | 10M/day, many channels, priorities | API Management, separate queues per priority, Functions per channel, Notification Hubs, Communication Services, Cosmos preferences, dead-letter handling |

### Chapter 6 — Reliability & business continuity

| # | Title | Scenario | Must include |
|---|---|---|---|
| 51 | Find the weak spot | `start`: one VM, one database, no zones | redundancy on every tier |
| 52 | Zone by zone | Survive a datacenter loss in one region | zone-resilient App Service, SQL, Redis, Storage (ZRS) |
| 53 | Do the math | Chain of four services | composite SLA estimate from assumed figures; a redundant path raises it |
| 54 | Flaky partner | Shipping API times out | edge `timeoutMs`, `retries`, `backoff`, `jitter` |
| 55 | Break the circuit | Slow recommendations slow everything | `circuitBreaker` + fallback (Redis or defaults) |
| 56 | Bulkheads | Report exports starve checkout | separate queues and separate compute for exports |
| 57 | Back it up | RPO of one hour | Azure Backup vault (GRS) for VMs, SQL `ltr` |
| 58 | Standby region | RTO of 30 minutes | SQL `failoverGroup`, Front Door priority routing, second region (alt: Site Recovery for VMs) |
| 59 | Both regions live | Writes accepted in two regions | Cosmos `multiRegionWrites` + `conflictResolution`, Front Door latency routing |
| 60 | ★ Never charge twice | Payments survive a zone loss | idempotency, retries + jitter, breaker, zone-redundant SQL, outbox → Service Bus |

### Chapter 7 — Containers & microservices

| # | Title | Scenario | Must include |
|---|---|---|---|
| 61 | Scale on the backlog | Workers idle or overwhelmed | Container Apps `scaleRule: service-bus`, `minReplicas` 0 |
| 62 | First cluster | Team moves to Kubernetes | `aks` group with `nodePools` ≥2, `clusterAutoscaler`, zones |
| 63 | Pull safely | Images come from a public registry | Container Registry, pull with `auth: managed-identity` |
| 64 | Broker-agnostic | Services publish without broker SDKs | Container Apps with `dapr` + Service Bus |
| 65 | One door for APIs | Five services, one public API | API Management routes and `versioning` |
| 66 | Own your data | `start`: three services share one DB | database per service; warn on shared DB |
| 67 | Saga, orchestrated | Five-step order process | Durable Functions orchestrator with `compensation` |
| 68 | Transactional outbox | Save + publish must agree | Cosmos `changeFeed` → Functions → Service Bus |
| 69 | CQRS | Dashboards slow down writes | change feed → materialized view (second store or AI Search) |
| 70 | ★ Online store | Catalog, cart, order, payment, inventory | API Management, AKS or Container Apps, DB per service, Service Bus, Entra ID, App Insights |

### Chapter 8 — Identity, security & governance

| # | Title | Scenario | Must include |
|---|---|---|---|
| 71 | Work sign-in | Internal app needs SSO | Entra ID, App Service `easyAuth` |
| 72 | Customers sign in | Consumer app with social logins | Entra External ID |
| 73 | No secrets in code | Connection strings in config | Key Vault, `identity` on compute, `auth: managed-identity` |
| 74 | Passwordless data | Remove SQL, Storage and Service Bus keys | `edgeProp` all edges into data use `managed-identity` |
| 75 | Check the token | APIs trust any caller | API Management `validateJwt` against Entra ID |
| 76 | Bring your own key | Regulator wants customer-managed keys | Key Vault `purgeProtection`, `cmk` on Storage and SQL |
| 77 | Layers of defence | Public API under attack | WAF, DDoS Protection, NSGs on subnets, private endpoints |
| 78 | Guardrails | Teams keep creating public storage | management group + subscriptions, Azure Policy `deny` |
| 79 | Watch for threats | Nobody notices attacks | Defender for Cloud + Sentinel on Log Analytics |
| 80 | ★ Landing zone | 20 app teams onboard | management groups, connectivity hub, firewall, policy, Entra ID, Key Vault, central Log Analytics |

### Chapter 9 — Data, observability & AI

| # | Title | Scenario | Must include |
|---|---|---|---|
| 81 | See the system | Customers find errors first | App Insights + Log Analytics + `monitor-alerts` with action group |
| 82 | Trace it | Slow checkout across five services | `tracing` on every service → App Insights |
| 83 | Find it | Product search | AI Search with `indexer` from Cosmos DB or SQL |
| 84 | Live numbers | Orders per minute by region | Event Hubs → Stream Analytics `windowing` → Data Explorer → Managed Grafana |
| 85 | Nightly numbers | Business reports | Data Factory → Data Lake → Fabric |
| 86 | Lakehouse | Raw clickstream forever, ML-ready | Data Lake + Databricks (bronze/silver/gold in `learn`) |
| 87 | Ask your docs | Chatbot over company PDFs | Blob → AI Search (`vector`, hybrid) → Azure OpenAI, app orchestrates; Content filtering in `learn` |
| 88 | Read the forms | 50k invoices a day | Blob → Event Grid → queue → Functions → Document Intelligence → Cosmos DB |
| 89 | Live together | Collaborative whiteboard | Web PubSub (or SignalR), state in Redis or Cosmos DB |
| 90 | ★ Fleet monitoring | 50k devices every 10 s | IoT Hub → Stream Analytics → Data Explorer, alerts, Grafana; estimate |

### Chapter 10 — Capstones

| # | Title | Scenario | Must include |
|---|---|---|---|
| 91 | News feed | Social timeline | fan-out on write via Service Bus + Cosmos feed containers, pull for celebrities |
| 92 | Photo sharing | Upload, feed, likes | Valet Key uploads, Event Grid thumbnails, Front Door, Cosmos DB |
| 93 | Chat app | 1:1 and groups, offline users | Web PubSub, Cosmos message store, Notification Hubs |
| 94 | Video streaming | Watch on any network | Container Apps jobs/Batch transcoding, renditions in Blob, Front Door |
| 95 | File sync | Folders across devices | chunks in Blob, metadata DB, change notifications |
| 96 | Ride sharing | Match riders and drivers | location stream on Event Hubs, geospatial queries, matching service, payments |
| 97 | Hotel booking | Search and reserve rooms | AI Search, SQL `locking` for availability, Service Bus |
| 98 | SaaS for many tenants | Thousands of business customers | Deployment Stamps, tenant catalog, Front Door routing per stamp |
| 99 | Modernize the monolith | Old VMs, one big database | Strangler Fig via Application Gateway, App Service, SQL Managed Instance |
| 100 | ★ Food delivery, worldwide | Everything, in three regions | multi-region, zone-resilient, private, passwordless, observable, within budget |

Chapter introduction screens list the concepts the chapter covers, taken from `challenge.concepts`. Target glossary: ~120 concepts (Azure services, cloud design patterns, Well-Architected principles and pillars).

---

## 9. Screens and UX

Routes are hash-based: `#/`, `#/load`, `#/c/c020`, `#/glossary`, `#/glossary/valet-key`, `#/settings`. A route to a challenge id not in the loaded pack shows "This challenge isn't in the loaded pack" with a link home.

Everything the player reads about a challenge comes from the pack. The platform adds only interface words, block labels/blurbs and constraint messages.

**Load** — shown when no pack is available, or from Settings. File picker, drag-and-drop area, pack errors list, built-in demo, the not-affiliated line.

**Home** — a grid of all challenges: one row per chapter, one cell per challenge. Each cell shows number, state (locked / open / solved) and stars. Pack title, progress summary and a pack-wide pillar summary beside it. The first chapter is open; each next chapter opens when the previous boss is solved (chapters without a boss open at 70% solved). Settings has "Open all challenges".

**Workspace**
```
┌───────────┬──────────────────────────────┬─────────────┐
│ Brief     │                              │ Properties  │
│ reqs,     │          SVG canvas          │ of selected │
│ numbers,  │                              │ block/edge/ │
│ checklist │                              │ group       │
├───────────┴──────────────────────────────┴─────────────┤
│ Palette (search incl. aka)  Cost 18/30  [Undo][Redo][Fit][Check] │
└─────────────────────────────────────────────────────────┘
```
Below 900 px, Brief and Properties become slide-over drawers; below 600 px the canvas is replaced by default with **List mode**.

- The properties panel groups props as "Settings", "Networking" (publicAccess, privateEndpoint, vnetIntegration), "Identity" and "Resilience" (zones, instances), and shows tier-dependent options with a short note when a constraint applies.
- Constraint badges appear live while editing, not only on Check.

**Results sheet** — constraint errors first (if any), rule checklist with pass/fail and explanations, stars, pillar scorecard, estimate questions, trade-off question, `learn` text, "Show solution" (read-only overlay with auto-layout, after solving or after 5 failed checks), "Next challenge".

**Glossary** — tabs: Services, Patterns, Principles. Each concept: definition, `aka` names, related blocks with blurbs, `ref` title, challenges that practice it.

**Settings** — current pack (title, version, verified month, challenge count), "Load another pack", theme (system/light/dark), open all, reset progress for this pack (confirm), export/import progress JSON, the not-affiliated line.

### Editor interactions

- Add block: drag from palette, or tap/click a palette item (placed at view center, selected).
- Move: drag. Snap to 16 px grid (hold Alt to disable).
- Connect: drag from a node's port handle to another node; or select node, press `C`, click target.
- Select edge: click near it (wide invisible hit path). Orthogonal elbows with rounded corners, anchored to nearest sides.
- Groups: drawn from the palette's Groups section, resizable by corner handles, drawn beneath nodes. Availability-zone groups render as translucent hatched bands so they read as overlapping subnets.
- Pan: drag empty space, middle mouse, or two fingers. Zoom: wheel/pinch, 25 %–200 %. `F` = fit to view.
- `Delete`/`Backspace` remove, `Ctrl/Cmd+Z` undo, `Ctrl/Cmd+Shift+Z` or `Ctrl+Y` redo, `Ctrl/Cmd+D` duplicate, `Esc` clear selection, `Enter` opens properties.
- Undo/redo: snapshot stack of the diagram JSON, max 100 entries; a drag is one entry.
- During drag, update only the moved element's `transform` and its edges; full re-render on drop.
- Autosave the draft per challenge (debounced 500 ms).

### Accessibility and List mode

- List mode is a complete alternative editor: blocks as a list (add, rename, edit props, choose groups by select), connections as rows with from / to / kind selects. Same diagram data, same validator. Group membership in list mode is chosen explicitly and stored by moving the node into the group rectangle.
- All controls reachable by keyboard with visible focus; nodes are focusable `<g tabindex="0" role="button">` with `aria-label` = label + type + constraint state.
- Pass/fail and constraint errors are never shown by color alone (icon + text).
- Respect `prefers-reduced-motion`.

---

## 10. Visual design direction

Subject: cloud architecture mapped across regions and zones. Direction: **aeronautical chart** — the canvas reads like a flight sectional: calm, precise, layered boundaries.

- Canvas: pale cool-grey chart paper with a fine 16 px grid and a stronger 80 px grid, ink in deep charcoal. Dark theme: night chart, slate paper with soft chalk lines.
- Groups as airspace boundaries: regions in a solid muted outline, VNets dashed, subnets thin, availability zones as light diagonal hatching, governance groups (management group, subscription) as wide pale frames.
- Blocks: muted category colors (colored-pencil tones) used as a left stripe + glyph tint, never saturated fills. Edges carry meaning through line style (§4.2), not color.
- Type: system font stacks only. UI: `system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`. Labels and numbers: `"Avenir Next Condensed", "Arial Narrow", "Roboto Condensed", sans-serif`. Sentence case, no all-caps labels.
- The one bold moment: the home grid, where solved cells fill in like charted squares.
- Avoid: looking like the Azure portal or Fluent UI (no Azure-blue chrome, no portal blades), cream background with terracotta accent, black with a single neon accent, identical rounded cards with soft grey shadows, decorative gradients, entrance animations on every panel.
- All colors are CSS custom properties in `[CSS:TOKENS]`, defined for light and dark (`:root`, `@media (prefers-color-scheme: dark)`, `[data-theme]` overrides).
- Copy: plain verbs, active voice. Buttons say what happens: "Check design", "Show solution", "Next challenge". Error and empty states say what to do next.

---

## 11. Persistence (`APP:STORE`)

- Keys:
  - `cirrus.v1.settings`
  - `cirrus.v1.pack` — the last valid pack's raw JSON text plus `{ id, version, loadedAt }`. If too big for storage, skip caching and say so once.
  - `cirrus.v1.progress.<packId>` — progress is per pack.
  - `cirrus.v1.draft.<packId>.<challengeId>`
- Every read/write wrapped in `try/catch`; if storage is unavailable, fall back to memory and show a one-line notice.
- Stored objects carry `schema: 1`. Migrations live in one `migrate()` function; never silently drop user data.
- Progress: `{ schema, packVersion, solved: { c001: { stars, bestAt, checks, pillars } }, quiz: {…}, hintsSeen: {…} }`.
- **Pack updated** (same `id`, new `version`): keep progress for challenge ids that still exist; keep but hide ids that disappeared. Stars already earned are kept even if new rules are stricter.
- Export/Import: one JSON file with progress + drafts for the current pack. Import validates shape and pack id before replacing anything.
- Diagram export: SVG (serialize the canvas with inlined styles) and PNG (SVG → `Image` → offscreen `<canvas>` → `toBlob`), downloaded via an `<a download>` link.

---

## 12. Testing

`tests/load-core.mjs` reads `index.html`, extracts `<script id="core">`, runs it in a `node:vm` context and returns `Core`. This is why `core` must never touch the DOM. `tests/load-pack.mjs` reads the pack as text and hands it to `Core.parsePack`, exactly like the browser.

Engine tests (fixtures only, never the official pack):

1. **Catalog**: unique types, unique `aka` across blocks, every prop has a valid default, every option list non-empty, every glyph and blurb present, every `costBy` key is a real prop with real options, `scope` set, no `Basic` load balancer option.
2. **Constraints**: each entry has a passing and a failing fixture diagram; `global-in-region` is a warning, the rest errors.
3. **Cost**: tier multipliers, count props, scale-to-zero floor, budget check.
4. **Validator** unit tests for each check type, including cycles, nested and overlapping groups, replication edges ignored by `path`, `zoneResilient` by prop and by placement, `edgeProp` with `all`.
5. **Score** formula and quiz path; **pillar scorecard** counts.
6. **Layout**: AZ bands cross subnets and every node keeps its intended group membership after layout.
7. **Pack loader**: valid fixture loads; each broken fixture (bad JSON, wrong `format`, newer `schema`/`engine`, unknown block/prop/option/check, unknown role, missing pillar, dangling edge, duplicate ids, missing concept, bad chapter, solution with a constraint error) is rejected with the expected error path; unknown fields only warn.
8. **Isolation**: `index.html` contains no challenge content; the only embedded challenge is the demo.

Pack tests (official or any pack path):

9. **Schema** (`Core.checkPack`) passes with zero errors.
10. **Official pack shape** (only `challenges.json`): 100 challenges, ids `c001…c100` in order, 10 chapters of 10, one boss per chapter, every pillar tagged on ≥ 15 required/bonus rules across the pack.
11. **Expected results**, per challenge (`Core.runPackSelfTest`):
    - empty diagram → not solved;
    - `expected.solution` → zero constraint errors, solved with 3 stars (including budget);
    - every `expected.alt` → zero constraint errors, solved;
    - every `expected.naive` → not solved, and each listed rule id fails;
    - with `start`, the unmodified start diagram → not solved;
    - the palette contains every block type the solution uses.

`?selftest` in the browser runs 9 and 11 on the loaded pack and shows a pass/fail table.

A task is done only when `node tests/run.mjs` passes, the page works over HTTP, and it works from `file://` after picking `challenges.json`, with no console errors.

---

## 13. Build order

Work in milestones; commit after each. Do not start a milestone before the previous one passes its tests.

1. **Shell + tokens**: layout, themes, routing, empty views, not-affiliated line.
2. **Core engine**: Azure catalog, constraints, cost, pack parser + `checkPack`, compact diagram expansion, graph helpers, validator, score, pillar scorecard, layout with AZ bands, self-test — plus the Node test harness and `tests/fixtures/` (a 3-challenge valid pack and the broken packs). Verify each constraint against Microsoft Learn as you add it.
3. **Pack loading**: fetch → cache → load screen with picker and drag-and-drop, error list, demo challenge.
4. **Editor**: blocks, edges, groups (incl. overlapping AZ bands and the AKS group), pan/zoom, selection, grouped properties, live constraint badges, cost meter, undo/redo, autosave.
5. **Challenge loop**: brief, Check, results sheet with pillar scorecard, hints, estimates, trade-off, show solution, progress per pack, unlocks, home grid.
6. **Content, chapter by chapter** — only `challenges.json` changes: verify the chapter's services (§8.1), author 10 challenges, run `node tests/run.mjs`. If a chapter needs a block, prop, constraint or check type the engine lacks, stop, add it to the engine with tests, then continue.
7. **List mode + accessibility pass.**
8. **Glossary (tabs, aka, refs), export/import, PNG/SVG export, `?selftest` overlay.**
9. **Polish**: responsive drawers, empty/error states, performance with 60+ nodes and nested groups.

### Authoring checklist for each challenge

- Brief reads like a real product request; numbers are realistic and SLAs/limits are phrased as assumptions.
- Every service, tier and feature used was checked against current Microsoft Learn docs; the pack's `verified` month is current.
- Required rules test concepts, not layouts; at least one `expected.alt` when more than one Azure design is reasonable (App Service vs Container Apps vs AKS, Front Door vs Traffic Manager…), and at least one `expected.naive` for boss challenges.
- Every rule has a `pillar`; the challenge touches at least two pillars.
- Solutions are passwordless where Azure supports it (`auth: managed-identity`) unless the challenge is about something else and says so in `_note`.
- Hints teach without giving the answer away.
- `learn` names the trade-off (cost, complexity, consistency, latency), not just the pattern.
- Every estimate has its arithmetic in `workings`.
- Solution fits the budget with some slack and has zero constraint errors.
- Every concept id exists in `concepts`.
- The file stays valid, pretty-printed JSON (2-space indent), challenges in chapter order.

---

## 14. Conventions

- 2-space indent, semicolons, `const` by default, small pure functions in `core`.
- Names: `camelCase` for functions/vars, `UPPER_SNAKE` for data tables, kebab-case for block types and concept ids.
- No global variables except `Core` and `Cirrus`.
- Comments explain *why*; sections use banner comments. Each constraint carries its `ref` and `verified` date in data, not only in a comment.
- Keep `index.html` navigable: when a section grows past ~800 lines, split it into sub-banners (`/* ---- [CORE:CATALOG:DATA] ---- */`).
- Challenge content changes go to `challenges.json` only; engine changes go to `index.html` only. A task that needs both lands the engine change and its tests first.
- When Azure renames a service: update `label`, add the old name to `aka`, keep `type`. When Azure retires a service: keep the type so old drafts load, mark it `retired: true` (hidden from the palette, constraint warning if used), and remove it from pack solutions in a new pack `version`.
- Never change a challenge `id`, block `type`, prop key or check type once shipped without a migration (engine) or a new pack `version` (content).
