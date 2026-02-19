# Validator Workflow (Iterators + Validator)

This repo uses a hub-and-spoke model:
- Many iterator agents push changes.
- One validator agent runs Buildbox and merges.

Goal: keep the expensive truth (Buildbox headless verdict) serialized and consistent, while iterators move quickly.

Machine role profiles:
- Desktop validator: `Docs/Operations/AgentProfile_Desktop_Validator.md`
- Desktop iterator: `Docs/Operations/AgentProfile_Desktop_Iterator.md`
- Laptop iterator: `Docs/Operations/AgentProfile_Laptop_Iterator.md`
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

Laptop policy:
- Laptop is iterator-only.
- Do not run greenifier/Buildbox validation on laptop hardware.

## Optional Burst Gate (Before Merge)

Recommended merge gate:
1. Burst-off smoke (fast sanity)
2. Burst-on smoke (real performance path)
3. Longer Burst-on matrix in nightlies

Note: `PUREDOTS_DISABLE_BURST` disables Burst at runtime; it may not remove all build-time Burst cost.
