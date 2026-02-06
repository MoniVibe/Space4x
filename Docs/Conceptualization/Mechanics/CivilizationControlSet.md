# Civilization Control Set (LLM Agent Interface)

**Purpose**
Define a robust, deterministic control surface that LLM agents use to steer a civilization at Galaxy Tamagotchi scale (2 TPS). The control set is designed to be low‑frequency, high‑impact, observable, and safe.

**Principles**
- **Orders are the language**: all actions are expressed as orders or directives.
- **Deterministic & replayable**: orders can be re‑applied in rewind.
- **Low‑frequency, high‑legibility**: avoid twitchy commands; prefer goals and constraints.
- **Explainable outcomes**: every order produces a traceable outcome and reason.
- **Scoped authority**: agents operate within defined jurisdiction and budget.

---

## 1) Control Layers (Top‑Down)

**1. Empire Directives (Strategic)**
- **Intent**: shape civilization posture and priorities.
- **Typical cadence**: minutes to hours of sim time.
- **Examples**: Expand, Secure Resources, Research Focus, Military Posture, Trade Bias.

**2. Faction Goals (Operational)**
- **Intent**: convert directives into goal queues.
- **Examples**: Colonize system, Establish trade route, Defend territory, Exploit resource.

**3. Fleet/Captain Orders (Tactical)**
- **Intent**: concrete action orders for carriers/fleets.
- **Examples**: Patrol sector, Intercept target, Mine region, Escort convoy, Establish outpost.

**4. Mission/Task Queue (Execution)**
- **Intent**: fine‑grained tasks derived from orders.
- **Examples**: Travel → Approach → Mine → Return → Offload.

---

## 2) Canonical Control Set

### A) Strategy Controls
- **SetDirective**: `(directiveType, priority, scope, target?, expiry?)`
- **AdjustDoctrine**: `(doctrineKey, value, scope)`
- **SetBudget**: `(category, cap, scope)` (research, military, logistics)
- **SetRiskTolerance**: `(riskProfile, scope)`

### B) Operational Controls
- **CreateGoal**: `(goalType, priority, target?, expiry?)`
- **RetireGoal**: `(goalId)` or `(goalType, scope)`
- **PrioritizeGoal**: `(goalId, priority)`

### C) Tactical Controls
- **IssueOrder**: `(orderType, target, constraints, priority, expiry)`
- **CancelOrder**: `(orderId)`
- **ModifyOrder**: `(orderId, patch)`

### D) Inquiry/Inspection Controls
- **QueryState**: `(scope, filters)` → returns compact state snapshot.
- **QueryOrders**: `(scope, status)` → orders + outcomes.
- **QueryTelemetry**: `(metricKey, window)` → time series deltas.

---

## 3) Order Envelope (Schema)

**Order**
```
id
issuer (agent/faction)
scope (empire/faction/fleet/captain)
type
priority (0–100)
target (entity or location)
constraints (budget/time/risk/casualties)
issuedTick
expiryTick
status (pending/active/completed/failed/aborted)
result (success/failure + reason + summary)
traceRef (telemetry/log linkage)
```

**Outcome**
```
orderId
finalStatus
reasonCode
metrics (key/value summary)
startTick / endTick
```

---

## 4) Constraints & Safety

- **Budget Caps**: orders cannot exceed resource, personnel, or time caps.
- **Jurisdiction**: agents can only control scoped entities.
- **Risk Guardrails**: max loss thresholds and collateral limits.
- **Conflict Resolution**: if multiple orders compete, priority + doctrine decides.

---

## 5) Observability & Feedback

Every order produces:
- **Outcome summary** (pass/fail/unknown + reason).
- **Tick span** (start → end).
- **Telemetry delta** (resources, casualties, territory change).
- **Narrative note** (short textual summary).

---

## 6) LLM‑Friendly Interaction Contract

**One‑turn policy**
- Agents should emit a compact **Plan** + **Orders** + **Queries** for next tick window.

**Example (pseudocode)**
```
Plan: Secure mining throughput in Sector K
Orders:
  IssueOrder(type=MineRegion, target=SectorK, priority=20, constraints={risk:"low", budget:500})
  IssueOrder(type=EscortConvoy, target=ConvoyA, priority=30, constraints={lossCap:5})
Queries:
  QueryOrders(scope=FactionA, status=active)
  QueryTelemetry(metricKey="resource.inflow", window="last_600_ticks")
```

---

## 7) Roadmap (Mutable)

- **Short‑term**: document the concrete order queue types used by captains/fleets.
- **Mid‑term**: add order outcome telemetry + reason codes.
- **Long‑term**: live “civilization console” for LLM agents with strict budgets.

---

## 8) Current ECS Bindings (Concrete, 2026‑02‑06)

**Strategic directives**
- Faction buffers: `Space4XSimServerComponents.Space4XFactionOrder`
- Resolved directive state: `Space4XSimServerComponents.Space4XFactionDirective`
- Baseline directive state: `Space4XSimServerComponents.Space4XFactionDirectiveBaseline`
- Directive resolver: `Space4XSimServerFactionDirectiveResolverSystem`

**Goal layer**
- Faction goals: `Space4X.Registry.Space4XFactionGoal`
- Goal resolver: `Space4X.Registry.Space4XFactionGoalSystem`
- Directive -> goal mapping: `Space4X.Orders.Space4XEmpireDirectiveGoalSystem`

**Directive seeds**
- Default directives per faction: `Space4X.Orders.Space4XEmpireDirectiveBootstrapSystem`

---

## 9) SimServer HTTP Contract (Current)

**Endpoints**
- `GET /health` or `GET /` -> `{ "ok": true }`
- `GET /status` -> JSON status blob
- `GET /saves` -> save slots list
- `POST /directive` -> enqueue directive JSON
- `POST /save` -> enqueue save request
- `POST /load` -> enqueue load request

**Directive JSON (minimal)**
```json
{
  "factionId": 1,
  "directiveId": "secure_resources",
  "priority": 0.7,
  "duration_seconds": 120,
  "mode": "blend",
  "source": "player",
  "weights": {
    "economy": 0.9,
    "security": 0.3,
    "expansion": 0.6
  }
}
```

**Notes**
- Either `factionId` or `factionName` can target a faction; omit both to apply to all.
- `orderId` overrides `directiveId` if you want a stable identifier.
- `priority` is normalized [0,1]. Missing fields fall back to faction baseline.
- `duration_seconds`, `duration_ticks`, or `expires_at_tick` control expiry.
- `mode` supports `blend` or `override`.
