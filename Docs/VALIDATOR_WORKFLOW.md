# Validator Workflow (Iterators + Validator)

This repo uses a hub-and-spoke model:
- Many iterator agents push changes.
- One validator agent runs Buildbox and merges.

Goal: keep the expensive truth (Buildbox headless verdict) serialized and consistent, while iterators move quickly.

Machine role profiles:
- Desktop validator: `Docs/Operations/AgentProfile_Desktop_Validator.md`
- Desktop iterator: `Docs/Operations/AgentProfile_Desktop_Iterator.md`
- Laptop iterator: `Docs/Operations/AgentProfile_Laptop_Iterator.md`
- Validator addendum: `validator.md`
- Iterator addendum: `iterators.md` and `Docs/Operations/ITERATORS.md`

Role-by-session on desktop host:
- Desktop can run validator sessions and iterator sessions.
- If startup prompt says `you are iterator`, follow iterator contract and do not run greenifier/Buildbox queue loops.
- If startup prompt says `you are validator`, follow validator contract.
- Telebot/Codex thread ids are dynamic; queue intake and merge policy must not depend on fixed `codex_session_id`.

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
- Once `needs-validate` is set, freeze that PR branch unless validator requests targeted follow-up on the same branch.
- New iteration should go to a new short-lived PR branch by default.

Validator intake gate (auto-pickup):
- PR base must be `main`.
- PR must be ready for review (not draft).
- PR must include `needs-validate`.
- PR is skipped when labeled `blocked`, `do-not-merge`, or `needs-intent-card`.
- Stacked PRs targeting non-`main` branches are intentionally ignored until retargeted.

Stacked branch policy:
- If iterators need shared base work, validate and merge the base PR to `main` first.
- Child PRs stay draft/off-queue until retargeted to `main`.
- After retarget, add `needs-validate` and hand off.

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

Validator loop runner (desktop/buildbox machine only):
```powershell
Remove-Item C:\polish\anviloop\ops\nightly_pr_greenifier.stop -ErrorAction SilentlyContinue
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\ops\bunker\nightly_space4x_pr_greenifier.ps1 -PollSec 120
```

Stop file:
```powershell
New-Item -ItemType File C:\polish\anviloop\ops\nightly_pr_greenifier.stop -Force
```

Greenness annotation:
- Added by validator after workflow completion and evidence collection.
- Score model:
  - `workflow_success` = 50 points
  - `result_zip_present` = 30 points
  - `playmode_pass` = 20 points
- Grade bands:
  - `supergreen` = 100
  - `green` = 80-99
  - `yellow` = 50-79
  - `red` = 0-49
- Stored in `C:\polish\anviloop\reports\nightly_pr_greenifier_state.json` as:
  - `greenScore`
  - `greenGrade`
  - `greenChecks`
  - `greenMissing`

Quick view:
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
      RunUrl = $_.Value.runUrl
      UpdatedUtc = $_.Value.updatedAt
    }
  } | Format-Table -AutoSize
```

## Red-Run Playbook (Validator)

Use this exact loop when a queued PR gets `yellow` or `red`:

1. Confirm latest validator verdict for that PR.
```powershell
$pr = 88
$state = Get-Content C:\polish\anviloop\reports\nightly_pr_greenifier_state.json -Raw | ConvertFrom-Json
$state.prs.$pr | Select-Object outcome,reason,greenScore,greenGrade,greenMissing,runUrl,summary,playmode,resultZip,updatedAt
```
2. Open evidence in priority order and extract first actionable error.
```powershell
$summary = ($state.prs.$pr).summary
if ($summary) { Get-Content $summary -Tail 120 }
```
3. Apply minimal fix on PR branch, push fix-up commit, keep label `needs-validate`.
4. Let validator loop pick up new SHA automatically.
5. Repeat until `greenScore=100` (`greenGrade=supergreen`) and outcome is mergeable.

Same-SHA rerun rule:
- Greenifier skips already-failed SHAs to avoid reprocessing loops.
- To rerun without code changes, clear only that PR state entry:
```powershell
$pr = "88"
$path = "C:\polish\anviloop\reports\nightly_pr_greenifier_state.json"
$json = Get-Content $path -Raw | ConvertFrom-Json
$h = @{}
foreach ($p in $json.prs.PSObject.Properties) { $h[$p.Name] = $p.Value }
$h.Remove($pr) | Out-Null
[pscustomobject]@{ prs = $h } | ConvertTo-Json -Depth 8 | Set-Content $path -Encoding utf8
```

Classification guide:
- `red` (0-49): workflow failed or no lane artifacts. Fix compile/runtime blocker first.
- `yellow` (50-79): workflow reached success path but missing zip/playmode proof.
- `green` (80-99): almost there; usually playmode artifact/probe issue.
- `supergreen` (100): full pass, merge candidate.

Laptop policy:
- Laptop is iterator-only.
- Do not run greenifier/Buildbox validation on laptop hardware.

## Optional Burst Gate (Before Merge)

Recommended merge gate:
1. Burst-off smoke (fast sanity)
2. Burst-on smoke (real performance path)
3. Longer Burst-on matrix in nightlies

Note: `PUREDOTS_DISABLE_BURST` disables Burst at runtime; it may not remove all build-time Burst cost.
