# Agent Profile: Desktop Iterator

Use this profile when the session role is `iterator` on desktop/buildbox hardware.

Role:
- Implement scoped changes.
- Hand off through PR + `needs-validate`.
- Do not run Buildbox/nightly/queue jobs.
- Do not merge to `main`.

## Startup Checklist

1. Read:
- `Docs/Operations/ITERATORS.md`
- `Docs/VALIDATOR_WORKFLOW.md`

2. Ensure branch hygiene:
- update local `main` with fast-forward only,
- create one scoped feature/fix branch.

3. Confirm package parity assumptions:
- do not drift `Packages/manifest.json` and `Packages/packages-lock.json` unintentionally.

## Required Iterator Loop

1. Implement.
2. Run guarded handoff (mandatory):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorGuardedHandoff.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PuredotsRepoPath C:\dev\Tri\puredots_ultimate `
  -PushBranch <branch-name> `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

`-UnityExe` is optional and only needed for explicit override.

## Parallel Editor + CLI Lane Pattern

- Keep your interactive editor open in `C:\dev\Tri\space4x_ultimate`.
- Run agent CLI compile/tests in `C:\dev\Tri\space4x`.
- Use:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunUnityCliLane.ps1 `
  -EditorRepoPath C:\dev\Tri\space4x_ultimate `
  -CliRepoPath C:\dev\Tri\space4x `
  -TestPlatform PlayMode `
  -TestFilter Space4XUiUxKernelTests
```

This allows simultaneous manual work + agent validation while enforcing editor/package parity by default.

PlayMode lane stability defaults:
- The CLI test runner automatically applies a headless-safe env (`PUREDOTS_EXIT_POLICY=nevernonzero` and related exit toggles) so test XML is emitted even when scenario invariants fire.
- Use `-AllowHeadlessProcessExit` only when you intentionally want legacy headless auto-exit behavior.

Lock behavior:
- Active lock: if `Temp/UnityLockfile` exists and Unity is actually running against that exact repo path, the lane run blocks unless `-IgnoreProjectLock`.
- Stale lock: if no matching Unity process is using that path, the runner warns and continues.

3. If guard fails, fix blockers (freshness/compile/awareness) and rerun.
4. Open PR to `main`.
5. Add label `needs-validate`.
6. Stop and hand off to validator.

## Handoff Requirements (PR Description)

- Summary (1-5 bullets)
- Invariants
- Acceptance checks
- Risk flags (`Assets/.meta`, `Packages/*lock*`, cross-repo pin changes, determinism risk)
- Guard report path (`Temp/iterator_bedrock_guard_<timestamp>.json`)
- Compile log path (`Temp/iterator_compile_preflight.log`)

If guarded handoff fails, do not hand off yet.
