# Agent Profile: Desktop Validator

Use this profile when the session role is `validator` on desktop/buildbox hardware.

Role:
- Validator is the only actor that runs Buildbox/nightlies/queue workflows.
- Validator is the only actor that greenifies and merges iterator PRs.

Primary workflow:
- `Docs/VALIDATOR_WORKFLOW.md`

Priority order:
1. Restore super green baseline.
2. Greenify queued PRs labeled `needs-validate`.
3. Merge only after successful validation evidence.

Hard boundaries:
- Do not ask iterators to run Buildbox.
- Keep validation evidence ordered by `run_summary_min -> run_summary -> meta/watchdog/logs`.
- Re-anchor desktop/laptop parity checkouts after green merge:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -Mode validator `
  -PushBranch main `
  -LocalParityBranch validator/ultimate-checkout `
  -LaptopRepoPath C:\dev\unity_clean `
  -LaptopParityBranch validator/ultimate-checkout
```
