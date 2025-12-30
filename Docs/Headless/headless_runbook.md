# Headless Runbook (Stub)

Canonical runbook lives at `puredots/Docs/Headless/headless_runbook.md`.

## Mandatory addenda
- Unity version alignment (ProjectVersion.txt -> UNITY_WIN) is required for rebuilds; do not fall back to Linux Unity.
- Space4X proof toggles are scenario-specific; see the canonical runbook for exact envs per test.
- S5 behavior loops may currently FAIL with reason=missing_loops; treat as a Tier 2 advisory and track in headlesstasks/backlog.
- If a bank failure is fixed or proof/env toggles change, update the canonical runbook and prompt in the same cycle.
- Before ending a cycle, run the staleness check and update runbook/prompt if bank expectations or toggles changed.
- Assets blocker protocol: if a fix needs `Assets/` or `.meta`, switch to a Windows/presentation context or log an ASSET_HANDOFF in headlesstasks/cycle log.
- Asset import failures are rebuild-blocking, not run-blocking; continue the cycle using the current build and mark it stale.

## Productivity requirement (non-negotiable)
- Each cycle must attempt at least one headlesstask from `headlesstasks.md`.
- If telemetry already exposes the metric, compute it and update `headlesstasks.md` (status, baseline/threshold, notes).
- If the metric is missing, add minimal telemetry in logic repos (PureDOTS) and rebuild; if it requires `Assets/` or `.meta` edits, log the requirement and switch to another task.
- Do not end a cycle with only bank runs; the bank is gating, not sufficient.

## Compile-error remediation (non-negotiable)
- If a rebuild fails with compiler errors, attempt a minimal, logic-only fix, rebuild scratch, then rerun Tier 0.
- If the compiler errors point to `Assets/` or `.meta` and the agent is running in WSL, log the blocker and switch tasks; do not edit those files from WSL.
- If the agent is running in a Windows/presentation context, it may fix `Assets/` or `.meta` compiler errors before retrying the rebuild.
- Record compile-fix attempts in the cycle log and note any blockers in `headlesstasks.md`.
