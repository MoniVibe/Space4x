# Validator Workflow (Iterators + Validator)

This repo uses a hub-and-spoke model:
- Many iterator agents push changes.
- One validator agent runs Buildbox and merges.

Goal: keep the expensive truth (Buildbox headless verdict) serialized and consistent, while iterators move quickly.

## Definitions

Super green:
- **space4x**: Buildbox headless smoke `SUCCESS` on `Assets/Scenarios/space4x_collision_micro.json`
- **godgame**: Buildbox headless smoke `SUCCESS` on `Assets/Scenarios/Godgame/godgame_smoke.json`

Validator priority:
1. Restore super green (both titles) if broken.
2. Then greenify PR backlog.

## Iterator Agent Rules (No Validation)

Iterators propose, they do not prove:
- Create a branch (one goal).
- Implement changes.
- Push branch and open a PR.
- Add label `needs-validate`.
- Stop. Do not trigger Buildbox / nightlies / queues.

PR intent card (required in PR description):
- Summary (1-5 bullets)
- Invariants (what must remain true)
- Acceptance checks (how to tell it worked)
- Risk flags: `Assets/.meta`, `Packages/*lock*`, cross-repo pin changes, determinism risk
- Burst plan (see below)

Burst plan for iterators:
- Default fast loop: validate logic with **Burst-off** first when you do local headless runs.
- Set one of:
  - `PUREDOTS_DISABLE_BURST=1`
  - `TRI_DISABLE_BURST=1`
- Still avoid introducing Burst hazards (static `FixedString*Bytes` initializers, managed APIs in Burst jobs).

## Cross-Machine Parity Sync (Required Before Manual Editor Validation)

When an iterator pushes a branch for validation, sync both desktop and laptop validation checkouts to the same ref:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -PushBranch <branch-name> `
  -LocalParityBranch validator/ultimate-checkout `
  -LocalParityUpstreamRef origin/<branch-name> `
  -LaptopRepoPath C:\dev\unity_clean_fleetcrawl `
  -LaptopParityBranch validator/ultimate-checkout `
  -LaptopParityUpstreamRef origin/<branch-name>
```

Policy:
- Do not validate from floating `main` while iterating feature branches.
- Validate from `validator/ultimate-checkout` only.
- Keep parity branch fast-forward only (`merge --ff-only`); no direct feature commits on parity branch.
- If either machine has a dirty tree, stop and resolve before sync.

Validator post-greenify sync (both machines -> `origin/main`):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -Mode validator `
  -PushBranch main `
  -LocalParityBranch validator/ultimate-checkout `
  -LaptopRepoPath C:\dev\unity_clean_fleetcrawl `
  -LaptopParityBranch validator/ultimate-checkout
```

Dirty criteria guidance:
- Prefer clean trees (`-DirtyPolicy fail`).
- Use `-DirtyPolicy stash-allowed` only for known transient diffs.
- Do not blanket-ignore `.meta` by default; only opt in (`-AllowMetaDirty`) when intentionally stashing temporary presentation churn.

## Validator Agent Responsibilities (Buildbox Only)

Validator is the only actor allowed to:
- Trigger Buildbox runs.
- Apply fix-up commits to make PR branches green.
- Merge PRs (or stage then merge).

Evidence order (do not freestyle):
1. `out/run_summary_min.json`
2. `out/run_summary.json`
3. `meta.json`
4. `out/watchdog.json` + `out/player.log`/`out/stdout.log`/`out/stderr.log`

Stop conditions:
- 5 iterations without improvement
- same failure twice
- infra failure twice

Local intake ledger:
- `C:\polish\queue\reports\pending_prs_to_greenify.md`

Validator exec runner (buildbox machine):
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\polish\queue\reports\validator_exec.ps1
```

## Optional Burst Gate (Before Merge)

Recommended merge gate:
1. Burst-off smoke (fast sanity)
2. Burst-on smoke (real performance path)
3. Longer Burst-on matrix in nightlies

Note: `PUREDOTS_DISABLE_BURST` disables Burst at runtime; it may not remove all build-time Burst cost.
