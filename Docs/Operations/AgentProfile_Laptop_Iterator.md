# Agent Profile: Laptop Iterator

Use this profile when iterating on low-RAM laptop hardware.

Role:
- Iterator only (implement + handoff).
- No Buildbox/nightly/queue operations.
- PR + `needs-validate` is the handoff point.

## Workflow

Follow canonical iterator contract:
- `Docs/Operations/ITERATORS.md`

Laptop-specific emphasis:
- Keep branch scope small to reduce compile churn.
- Run bedrock guard before push:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorBedrockGuard.ps1 `
  -RepoPath C:\dev\unity_clean\space4x_ultimate `
  -PuredotsRepoPath C:\dev\puredots `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

- If laptop repo paths differ, pass your local paths explicitly.
- Do not bypass freshness/compile/awareness gates to "save time"; that shifts noise to validator.

After push:
- label PR `needs-validate`,
- stop and hand off.
