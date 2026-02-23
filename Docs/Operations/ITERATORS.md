# Iterator Contract (Canonical)

This is the canonical iterator workflow for `space4x`.

Role split:
- Iterator: implement and hand off.
- Validator: run Buildbox/nightlies, greenify, merge.

Hard boundary:
- Iterator agents do not run Buildbox, queue workers, or nightly decks.
- Iterator agents do not merge to `main`.
- Iterator handoff is PR + `needs-validate`.

## Mandatory Loop

1. Start clean and current.
- `git fetch --all --prune`
- `git switch main`
- `git pull --ff-only`
- Branch from current `main` (`feat/*`, `fix/*`, `perf/*`, `infra/*`).

2. Implement one scoped goal.
- Keep branch scope tight.
- Avoid mixing unrelated fixes.

3. Run guarded handoff before every push.
- Mandatory gate command:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorGuardedHandoff.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -PuredotsRepoPath C:\dev\Tri\puredots `
  -PushBranch <branch-name> `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

This command enforces:
- bedrock freshness (`space4x` + `puredots` not behind `origin/main`),
- compile preflight gate (zero compile errors),
- open `needs-validate` queue awareness,
- push + desktop/laptop parity sync.

4. If guarded handoff fails, fix and rerun.
- Do not open/update PR while guard is red.

5. Open PR.
- Add label `needs-validate`.
- Include a PR intent card:
  - Summary (1-5 bullets)
  - Invariants
  - Acceptance checks
  - Risk flags (`Assets/.meta`, `Packages/*lock*`, cross-repo pin changes, determinism risk)
  - Guard report path (`Temp/iterator_bedrock_guard_<timestamp>.json`)
  - Compile log path (`Temp/iterator_compile_preflight.log`)

6. Stop after handoff.
- Validator owns all Buildbox runs and merge decisions.

## Drift Guardrails

- No blind cross-branch merges into active feature branches.
- Prefer scoped cherry-picks over replaying stale branches.
- If integrating `main` causes unexpected wide deletions/churn, stop and triage before continuing.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` aligned with current `main` unless the branch intentionally changes package pins.
- For FleetCrawl/presentation changes, run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/ScenarioReferenceCheck.ps1
```

- Keep scenario authority and runtime gate logs explicit so drift is visible in logs.

## Escalate to Validator Immediately

- Same compile failure repeats twice after targeted fixes.
- Package restore/pin issues block compile.
- Parity script reports dirty-state or fast-forward policy violations.
- Regression appears to come from stale-branch replay or merge deletions.

Related docs:
- `Docs/VALIDATOR_WORKFLOW.md`
- `Docs/Operations/FleetCrawl_Parity_Incident_Lessons_2026-02-21.md`
- `Docs/Operations/FleetCrawl_Bootstrap_Contract.md`
