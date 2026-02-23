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
  -RepoPath C:\dev\Tri\space4x `
  -PuredotsRepoPath C:\dev\Tri\puredots `
  -PushBranch <branch-name> `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

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
