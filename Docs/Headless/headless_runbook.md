# Headless Runbook - Rebuild Gate + Test Bank (TRI)

Purpose: keep headless agents productive **without rebuild churn** by enforcing:
1) a **prebuild gate** (assume current build is immutable),
2) a **test bank** (canonical loops + pass/fail rules),
3) a **rebuild gate** (batch-only rebuilds).

This runbook applies to **PureDOTS + Godgame + Space4X**.

---

## Non-negotiables (Headless Track Contract)

- Headless is the source of truth for simulation correctness.
- During an overnight shift, **assume "current build" is fixed**.
- **Do not request rebuilds** as a first response to failures.
- Prefer changes that can be validated **without rebuilding**:
  - Scenario JSON (swap/copy into build folder)
  - Environment flags (proof toggles, telemetry level, thresholds)
  - Report/telemetry output paths
- If the test bank goes green: **freeze rebuilds** until a new failure or a material code change.

Telemetry defaults (use unless you are debugging a specific failure):
- `PUREDOTS_TELEMETRY_LEVEL=summary`
- `PUREDOTS_TELEMETRY_MAX_BYTES=52428800` (50MB cap)

(Deep dive only: set level=full and/or raise max bytes.)

---

## Definitions

**Build**: a compiled headless player binary (immutable during the shift).

**Run**: executing the binary with a scenario and env flags.

**Test Bank**: the minimum set of runs that must pass to consider headless "green."

**PASS**: exit code `0` + required proof PASS lines present + telemetry written and within cap.

**FAIL**: any non-zero exit code OR proof FAIL lines OR missing telemetry artifact.

---

## Prebuild Gate (How to decide "no rebuild needed")

Before escalating to rebuild, do this:

1) Re-run the same scenario twice (same seed), confirm failure is repeatable.
2) Confirm proof enable/disable env flags are correct.
3) Confirm scenario spawns required subjects for the proof (villagers/storehouse, mining vessels, strike craft, etc.).
4) Ensure telemetry output is **fresh** (truncate/rotate between runs).
5) Only then decide if a rebuild is required.

Note: authoring/baker/SubScene changes typically require rebuild; scenario/env changes do not.

---

## When a rebuild is allowed (Rebuild Gate)

A rebuild request is allowed ONLY if **all** are true:
- The fix requires compiled code or baking (cannot be validated by swapping JSON/env).
- You have **batched** multiple fixes/instrumentation into one request.
- You can cite which tests in the bank will be re-run post-build.
- You attach a concise "why rebuild" diff summary.

Rebuild requests must be bundled and aligned to scheduled windows.

---

## Test Bank (Minimum)

### Global / Shared (PureDOTS proofs)
These are required because time/rewind are the spine.

Proof expectations (see `Docs/QA/HeadlessProofs.md`):
- **Time control proof**: `[HeadlessTimeControlProof] PASS ...`
- **Rewind core proof**: `[HeadlessRewindProof] PASS ...` (requires at least one subject registered by game proofs)

Recommended flags:
- `PUREDOTS_HEADLESS_TIME_PROOF=1`
- `PUREDOTS_HEADLESS_REWIND_PROOF=1` (or unset; enabled by default in headless)

### Space4X (canonical smoke)
Scenario artifact:
- `Assets/Scenarios/space4x_smoke.json` (shared with `TRI_Space4X_Smoke`)

Run must show:
- logs mentioning `space4x_smoke.json`
- mining loop produces positive ore delta (proof emits PASS)
- (when applicable) behavior proof loops emit PASS/FAIL at scenario end

### Godgame (canonical smoke)
Scenario artifact:
- `Assets/Scenarios/Godgame/godgame_smoke.json` (shared with `TRI_Godgame_Smoke`)

Run must show:
- logs mentioning `godgame_smoke.json`
- villager gather->deliver proof PASS
- needs proof PASS
- combat proof PASS (if enabled for the scenario)

---

## Reference commands (built headless binaries)

These are examples. Always prefer pointing `--scenario` at a file **inside the build's Scenarios folder** so you can swap JSON without rebuilding.

### Space4X build/run shape
Build output and scenario folder are typically:
- `Builds/Space4X_headless/Linux/Space4X_Headless.x86_64`
- `Builds/Space4X_headless/Linux/Space4X_Headless_Data/Scenarios/space4x/`

Example run:
~~~bash
PUREDOTS_TELEMETRY_LEVEL=summary \
PUREDOTS_TELEMETRY_MAX_BYTES=52428800 \
SPACE4X_HEADLESS_MINING_PROOF=1 \
Builds/Space4X_headless/Linux/Space4X_Headless.x86_64 \
  --scenario Builds/Space4X_headless/Linux/Space4X_Headless_Data/Scenarios/space4x/space4x_smoke.json \
  --report reports/space4x_smoke.json
~~~

### Godgame build/run shape
Build output and scenario folder are typically:
- `Builds/Godgame_headless/Linux/Godgame_Headless.x86_64`
- `Builds/Godgame_headless/Linux/Godgame_Headless_Data/Scenarios/godgame/`

Example run:
~~~bash
PUREDOTS_TELEMETRY_LEVEL=summary \
PUREDOTS_TELEMETRY_MAX_BYTES=52428800 \
Builds/Godgame_headless/Linux/Godgame_Headless.x86_64 \
  --scenario Builds/Godgame_headless/Linux/Godgame_Headless_Data/Scenarios/godgame/godgame_smoke.json \
  --report reports/godgame_smoke.json
~~~

Notes:
- Always pass `--report ...` so the run derives a default telemetry output path.
- Interpret exit code: `0` PASS, non-zero FAIL.

---

## Pass/Fail Rules (Machine-checkable)

A run is **PASS** only if:
- Process exit code is `0`
- No `FAIL` proof lines in logs
- Required `PASS` proof lines exist for enabled proofs
- Telemetry output exists AND is fresh AND size <= `PUREDOTS_TELEMETRY_MAX_BYTES`

A run is **FAIL** if:
- Exit code != 0
- Any proof logs a `FAIL`
- Telemetry missing or stale
- Telemetry exceeds cap (unless explicitly allowed for a deep-dive run)

---

## Overnight Work: What headless agents should fix without rebuild

Allowed overnight fix types (no rebuild):
1) Scenario JSON tweaks (counts, spawn distances, durations, scripted actions)
2) Env-flag tuning (enable/disable specific proofs, thresholds, debug modes)
3) Telemetry instrumentation changes ONLY if already in the current build (toggle levels)
4) Report hygiene (ensure fresh report/telemetry per run; cap respected)

Not allowed without rebuild:
- Any C# code edits, bakers, SubScene changes, package changes

If a code fix is needed:
- Implement the change in a PR **but do not request a rebuild**.
- Add a short reproduction command + which test bank entry it unblocks.
- Tag it `REBUILD_BATCH_CANDIDATE`.

---

## Nightly Backlog Format (copy/paste)

Create one item per failing bank entry:

- **Test**: (Space4X smoke / Godgame smoke / PureDOTS time / PureDOTS rewind / etc.)
- **Build ID**: (commit SHA or build stamp)
- **Repro command**: (exact CLI + env)
- **Observed**: (exit code, FAIL line, missing telemetry, perf budget fail)
- **Expected**: (PASS line + required telemetry loop proof)
- **Likely slice**: Body / Mind / Aggregate + suspected system(s)
- **Fix type**: Scenario | Env | Code(PR)
- **Status**: Investigating | Mitigated(no rebuild) | PR opened | Needs rebuild batch
- **Artifacts**: logs + telemetry ndjson path

---

## Rebuild Request Template (batch-only)

- **Why rebuild**: (must be code/bake-only)
- **PRs included**: (list)
- **Expected impact**: (which tests flip from red->green)
- **Prebuild evidence**: (bank failing on current build)
- **Postbuild plan**: (run full bank once; if green, freeze rebuilds)
