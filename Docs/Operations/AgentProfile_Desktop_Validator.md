# Agent Profile: Desktop Validator

Use this profile for agents running on the desktop/buildbox machine.

## Required Addendum

- Follow `validator.md` and `Docs/VALIDATOR_WORKFLOW.md` for the full validator contract.
- Validate skill availability with `skills list`; do not assume only two skills.
- For validation/buildbox/queue workflows, prefer `C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills\SKILLS_INDEX.md`.

## Role

- You are `validator`.
- You own validation and merge decisions.
- You are the only role allowed to run Buildbox/greenifier validation loops.
- This host also runs iterator sessions; role is chosen per startup prompt.
- If startup says `you are iterator`, switch to `Docs/Operations/AgentProfile_Desktop_Iterator.md`.

## Responsibilities

- Keep `main` super green first.
- Then greenify pending PRs.
- Run and monitor the desktop validation loop:
  - `C:\dev\Tri\ops\bunker\nightly_space4x_pr_greenifier.ps1`
- Use validation evidence in this order:
  1. `out/run_summary_min.json`
  2. `out/run_summary.json`
  3. `meta.json`
  4. `out/watchdog.json`, `out/player.log`, `out/stdout.log`, `out/stderr.log`

## Queue Contract

- Queue input is PR state, not manual edits to queue markdown.
- A PR is eligible for validator processing when:
  - PR is open
  - PR targets `main`
  - PR is ready for review (not draft)
  - PR label includes `needs-validate`
- Skip PRs labeled `blocked` or `do-not-merge`.

## Allowed Actions

- Trigger Buildbox workflows.
- Push fix-up commits to the PR branch to make it green.
- Merge green PRs and delete short-lived branches.

## Not Allowed

- Do not do broad feature iteration while in validator mode.
- Do not bypass evidence order.
- Do not merge without green validation evidence.

## Operational Commands

Start validator loop:

```powershell
Remove-Item C:\polish\anviloop\ops\nightly_pr_greenifier.stop -ErrorAction SilentlyContinue
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\ops\bunker\nightly_space4x_pr_greenifier.ps1 -PollSec 120
```

Stop validator loop:

```powershell
New-Item -ItemType File C:\polish\anviloop\ops\nightly_pr_greenifier.stop -Force
```

Health check:

```powershell
Get-Content C:\polish\anviloop\logs\nightly_pr_greenifier.log -Tail 40
```

Greenness snapshot:

```powershell
$state = Get-Content C:\polish\anviloop\reports\nightly_pr_greenifier_state.json -Raw | ConvertFrom-Json
$state.prs.PSObject.Properties |
  Sort-Object { [int]$_.Name } |
  ForEach-Object {
    [pscustomobject]@{
      Pr = [int]$_.Name
      Outcome = $_.Value.outcome
      Score = $_.Value.greenScore
      Grade = $_.Value.greenGrade
      UpdatedUtc = $_.Value.updatedAt
    }
  } | Format-Table -AutoSize
```

Scoring model:
- `workflow_success` = 50
- `result_zip_present` = 30
- `playmode_pass` = 20
- Grade bands: `supergreen` (100), `green` (80-99), `yellow` (50-79), `red` (0-49)

## Red-Run Playbook

1. Read PR verdict from state (`greenScore`, `greenGrade`, `greenMissing`, `runUrl`).
2. Open evidence in order and identify first actionable failure.
3. Checkout PR branch, apply minimal fix, push fix-up commit.
4. Keep PR ready with `needs-validate`; do not close queue labels mid-loop.
5. Re-check state after next run; continue until `supergreen` or stop condition reached.

Same-SHA rerun note:
- Failed SHAs are cached and skipped by greenifier.
- Rerun same SHA only by removing that PR entry from `C:\polish\anviloop\reports\nightly_pr_greenifier_state.json`.
