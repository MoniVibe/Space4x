# Agents – Role-First Guidelines

## Role Initialization Card

**Before doing anything:**

1. **Identify your ROLE**: Validator | Builder | Ops | Perf | Sherpa | Docs
2. **Read canonical docs** (source of truth):
   - `Docs/NORTH_STAR.md`, `Docs/DEMO_SLICE.md`, `Docs/ARCHITECTURE.md`, `Docs/PERF_GATES.md`, `Docs/NETPLAY_NOT_NOW.md`, `Docs/PROGRESS_HUB.md`
   - **Fallback** (if above are missing in this clone): `TRI_PROJECT_BRIEFING.md` (at space4x/godgame root), `Docs/Headless/headless_runbook.md`, `Docs/Headless/headlesstasks.md`, `Docs/Headless/recurring.md`, `Docs/Headless/recurringerrors.md`, and `directive.md` (if at workspace root)
3. **Claim a SLOT** before touching files: see `Docs/Agents/SLOTS.md`
4. **Output expectations**: Each role has a Definition of Done; report accordingly.

---

## Project Context

- **PureDOTS**: shared deterministic ECS runtime and contracts.
- **Space4X**: flagship game — carriers as **villages in space** (hull, society, chain of command). Command ladder: `God → Admiral → Captain → Officer → Pilot`.
- **Godgame**: sibling project proving runtime patterns in fantasy/society domain.
- **Demo slice**: carrier-as-village, command ladder in action, headless-proven truth. See directive / ops binder for full demo vibe.

---

## Roles

### Validator (Bunker only)

**The only role allowed to run canonical validations and declare "super green."**

| Allowed | Forbidden |
|---------|-----------|
| Run Buildbox, headless, nightly validations | Any implementation of gameplay features |
| Apply fix-up commits and merge validated work | Running validations outside Bunker/canonical flow |
| Claim VALIDATION slot | |
| Report run id, exit_reason, top errors, next step | |

**Definition of Done:** Report run id, exit_reason, verdict (pass/fail), and top errors or suggested next step. Evidence order: `out/run_summary_min.json` → `out/run_summary.json` → `meta.json` → `out/watchdog.json` + logs.

---

### Builder / Author

**Implements features. May run local smoke. May NOT declare validated.**

| Allowed | Forbidden |
|---------|-----------|
| Implement features in PureDOTS, Godgame, Space4x | Running canonical validations |
| Run local smoke and compile checks | Declaring "validated" or "super green" |
| Claim DEMO_SLICE (or feature slot) | Triggering Buildbox/nightlies/queues directly |
| Push branch, PR, intent card, label `needs-validate` | |

**Definition of Done:** Code compiles, local smoke passes (if applicable), PR has intent card and `needs-validate` label. Do not claim validated until Validator has run.

---

### Ops

**Owns task wiring and headless tooling. Does NOT run validations.**

| Allowed | Forbidden |
|---------|-----------|
| Task wiring, headless runbook, queue config | Running validations |
| Claim TASK_WIRING slot | Implementing large gameplay features |
| Maintain tools, scripts, CI wiring | Declaring green |
| Fix infra and path issues | |

**Definition of Done:** Wiring works, runbook is accurate, queues/tools behave as documented.

---

### Perf / Harness

**Owns perf gates, scale harness, and report schemas. Does NOT implement large gameplay features.**

| Allowed | Forbidden |
|---------|-----------|
| Perf gates, scale harness, telemetry schemas | Implementing large gameplay features |
| Claim SCALE_HARNESS slot | Running validations (Validator only) |
| Budget definitions, regression checks | Declaring green |
| Report schema updates | |

**Definition of Done:** Gates/schemas updated with scenario id, before/after evidence, and documented rationale.

---

### Sherpa / Bridge

**Coordination only. Forbidden from validations.**

| Allowed | Forbidden |
|---------|-----------|
| Coordinate between agents, route tasks | Running any validations |
| Handoffs, status summaries | Implementing features |
| Bridge prompts and intent cards | Declaring green |
| | CodexBridge must NOT "helpfully validate" |

**Definition of Done:** Handoff updated, tasks routed correctly. No validation artifacts produced.

---

### Docs

**Updates documentation. Never touches CI, harness, or runtime.**

| Allowed | Forbidden |
|---------|-----------|
| Update AGENTS.md, SLOTS.md, canonical docs | Touching CI, harness, or runtime code |
| Claim DOCS slot | Running validations |
| Stub or redirect stale references | |

**Definition of Done:** Docs accurate, links fixed, no runtime/CI/harness changes.

---

## Slot Locking

See `Docs/Agents/SLOTS.md` for slot table and claiming rules.

- One slot owner at a time.
- Only Validator may claim VALIDATION.
- Claim before editing; release when done.

---

## Shared Rules (apply to all roles)

### Project Layout

- `PureDOTS/` → shared engine-level DOTS package (`Packages/com.moni.puredots`)
- `Godgame/` → game project using PureDOTS
- `Space4x/` → game project using PureDOTS
- `Foundation1/` and `Project1/` → read-only references unless user says otherwise
- Engine logic: `PureDOTS/Packages/com.moni.puredots/Runtime/<Module>/`
- Game logic: `Godgame/Assets/Scripts/Godgame/...`, `Space4x/Assets/Scripts/Space4x/...`

### DOTS 1.4 & Burst Style

- Unity Entities 1.4+, C# 9, Burst default
- Use `ISystem` and `SystemAPI`; no `ComponentSystem`, `SystemBase`, `IJobForEach`
- Use `IComponentData` / `IBufferElementData` with Burst-safe fields only (no managed refs, no `string`, no `class` in components)
- Use `RefRO<T>` / `RefRW<T>`, `DynamicBuffer<T>` with `[InternalBufferCapacity]`
- Avoid: Linq, reflection, exceptions in hot paths, allocations in `OnUpdate`, `foreach` on native containers (use index loops)
- Systems under `PureDOTS.Runtime.<ModuleName>.Systems`; use `[BurstCompile]`
- Respect determinism/rewind when mentioned

### Game vs Engine Separation

- Engine (PureDOTS): environment, motivation, goals, miracles core, construction, stellar/solar systems
- Game-specific: building/ship lists, miracles, VFX, UI, input, scene wiring
- When in doubt: if reusable, put in PureDOTS

### Hard Rules (6.1)

- Do not add new asmdefs
- Do not modify asmdefs except to match AsmdefLayout.md
- Do not introduce runtime Monos to force ECS/singletons unless explicitly requested
- Do not touch `com.moni.puredots.demo` or import its systems into game asmdefs
- Use canonical render pipeline: `<Game>RenderCatalogDefinition` → Baker → `RenderPresentationCatalog` + `RenderMeshArray` → `ResolveRenderVariantSystem` / `ApplyRenderVariantSystem` + presenter systems
- Respect unified RenderKey per game in Spawners, Rendering, Sanity systems

### Error Patterns (6.2)

- CS0118/CS0119 related to Camera → namespace hygiene; use aliases per NamespaceHygiene doc
- `SystemAPI.*` in static methods → move into `OnUpdate` or use `SystemState` APIs
- `GetSingleton<T>()` requires exactly one entity → ensure baked/created by canonical pipeline
- Deviations are design bugs, not tolerances

### Cross-OS Workflow (WSL + Windows) — TRI multi-track

- Preferred WSL root: `/home/oni/Tri` (ext4). Avoid `/mnt/c` for active work (drvfs I/O errors)
- Dual clones: WSL edits PureDOTS + logic; Windows edits `Assets/` + `.meta` + scenes/prefabs
- Keep `Packages/manifest.json` and `packages-lock.json` in sync; drift causes slice-only compile errors
- Do not share `Library` between OSes
- WSL is case-sensitive; fix casing mismatches

### Headless Run Discipline

- Canonical paths, ops-bus protocol (`Docs/Headless/OPS_BUS_PROTOCOL.md`), no Unity license assumption
- Entrypoint expectations: see `Docs/Headless/headless_runbook.md`, `Docs/Headless/headlesstasks.md`

### Known Pitfalls (recurring signatures)

- Branch pin/pathspec mismatches
- Telemetry bloat / oracle missing
- See `Docs/Headless/recurring.md`, `Docs/Headless/recurringerrors.md`

### Simulation Behavior Contract (Headless)

- Headless must be fully organic: no proof-only hacks, shortcuts, bypasses, or hard-coded behavior
- Decisions are weight-driven against dynamic individual + collective profiles
- Profiles are mutable; behavior reflects live profile state
- Global exceptions limited to shared survival/needs logic
- Performance first: design for millions; avoid per-tick global scans

### Buildbox Iteration Contract (Validator / Builder reference)

- Work branch-only; never push to main or rewrite history
- No autoloop: implement between runs. Queue → diagnose → fix → requeue
- Evidence order: `out/run_summary_min.json` → `out/run_summary.json` → `meta.json` → `out/watchdog.json` + logs
- Stop: 5 iterations without improvement, same failure twice, infra failure twice
- Artifacts: `run_summary_min.json`, `run_summary.json`, `meta.json`, `watchdog.json`, `player.log`, `stdout.log`, `stderr.log`

**Only Validator runs Buildbox and consumes this contract authoritatively.** Builder may use it as informational only.

### Git Practices

- Long-lived: `main` only. Short-lived: `feat/*`, `fix/*`, `perf/*`, `infra/*`
- One goal per branch; delete after merge
- Iterate on same branch until green; avoid spawning nightly/run branches

### Multi-Agent Workflow (Roles + Slots)

- **Builder** pushes branch + PR + intent card, adds `needs-validate`. Does not trigger Buildbox.
- **Validator** is the only actor that runs Buildbox, applies fix-up commits, and merges.
- **Sherpa/Bridge** coordinates; never validates.
- Workflow details: `TRI_PROJECT_BRIEFING`, `Docs/Headless/headless_runbook.md`, `Docs/INDEX.md`; if `PROGRESS_HUB`/`ARCHITECTURE` exist, use those.

### Answer Style

- Concise, high-signal. Start with solution.
- Small focused snippets; minimal diffs.
- No restating repo structure unless asked.
- Verbose only when user asks for deep explanations.

### When Unsure

- Non-critical: reasonable assumption + state briefly
- Critical (API, breaking change): ask once
