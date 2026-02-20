# Iterator Addendum (Desktop + Laptop)

Use this addendum whenever the startup contract says `you are iterator`.

## Core Contract

- Iterate on one scoped goal per branch.
- Keep blast radius small and reviewable.
- Prepare handoff via PR; do not self-validate through Buildbox queues.
- Do not merge to `main`.

## PureDOTS Promotion Gate (Required)

- Before coding, classify the slice using this gate:
  - `puredots` when all are true:
    - deterministic sim logic (no camera/UI/input/presentation concerns)
    - no `Space4X`/`Godgame` types, ids, or theme terms in shared contracts
    - behavior is driven by generic components/blobs/tuning, not hardcoded content ids
    - likely reusable by at least one sibling game within the next few slices
  - `space4x` when any are true:
    - player control, camera, HUD, scene authoring, or presentation wiring
    - FleetCrawl-specific policy/content defaults (hull ids, station ids, mission semantics)
    - direct dependency on `Space4X.*` types/components/enums
- If a slice contains both concerns, split it:
  1. Add/update shared contract and core system in `puredots`.
  2. Add Space4X adapter/bridge that maps Space4X data to shared contracts.
  3. Keep Space4X UX/content policy in `space4x` only.
- PR intent card must include a `Placement` block:
  - `shared:` files under `puredots` and reason
  - `game-side:` files under `space4x` and reason
  - `deferred-promote:` any local code intentionally left game-side for now

## Skill Awareness (Required)

- At startup, check skill availability with `skills list`.
- If the task matches an available skill, use that skill workflow.
- Do not assume only two skills. Also check:
  - `codexbridge/skills.ps1` root (`CODEX_SKILLS_ROOT` or `%USERPROFILE%\.codex\skills`)
  - `C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills\SKILLS_INDEX.md`
- If using telebot broker skills routing, set `CODEX_SKILLS_ROOT=C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills` to expose the headless pack in `skills list`.
- For buildbox/headless/queue lanes, prefer HeadlessRebuildTool `.agents/skills/*` workflows.
- If a skill is named or clearly applicable but unavailable in-session, state it and continue with fallback.

## Role-by-Session Rule

- Machine does not define role by itself.
- Desktop can run `validator` sessions and `iterator` sessions.
- If the startup prompt says `you are iterator`, follow iterator rules even on desktop.
- If the startup prompt says `you are validator`, follow validator rules and do not broad-iterate.

## Desktop Iterator Rules

- Allowed:
  - feature/fix implementation
  - local compile checks
  - lightweight local smoke checks
- Not allowed:
  - start/stop `C:\dev\Tri\ops\bunker\nightly_space4x_pr_greenifier.ps1`
  - write `C:\polish\anviloop\ops\nightly_pr_greenifier.stop`
  - trigger Buildbox/nightly queue runs
  - merge PRs directly

## Laptop Iterator Rules

- Allowed:
  - feature/fix implementation
  - fast local checks only
- Not allowed:
  - Buildbox/greenifier/nightly loops
  - heavy validation workloads that choke laptop RAM
  - merge PRs directly

## PR Queue Ingress (Required)

- Open PR targeting `main`.
- Keep PR as draft while iterating.
- When handoff-ready:
  - mark PR ready for review
  - add label `needs-validate`
  - stop and wait for validator
- After `needs-validate`, freeze that PR branch for both desktop and laptop iterators.
- Only validator may request targeted follow-up commits on that same queued branch.
- Otherwise, put new ideas into a new short-lived PR branch.
- Do not manually edit `C:\polish\queue\reports\pending_prs_to_greenify.md`.

## Validator Intake Gate (Hard)

- Validator auto-pickup requires all of the following:
  - PR base branch is `main`
  - PR is ready for review (`isDraft=false`)
  - PR has label `needs-validate`
  - PR does not have labels `blocked`, `do-not-merge`, or `needs-intent-card`
- PRs targeting feature branches (for example `feat/* -> feat/*`) are ignored by validator.

## Stacked PR Handling (When You Need Shared Base Work)

- Preferred: keep iterator PRs directly against `main`.
- If stacked PRs are unavoidable:
  1. Land base integration PR to `main` first (`needs-validate`, ready).
  2. Keep child PRs as draft and do not mark them `needs-validate` yet.
  3. After base merges, retarget each child PR to `main`.
  4. Then mark ready + add `needs-validate` for validator intake.

## Pre-Handoff Self-Check

- Before declaring handoff complete, verify intake fields:
  - `gh pr view <PR_NUMBER> -R MoniVibe/Space4x --json baseRefName,isDraft,labels,url`
- Expected:
  - `baseRefName` = `main`
  - `isDraft` = `false`
  - `labels` includes `needs-validate`

## Telebot Session Handling (Required)

- Never hardcode a specific `codex_session_id` in iterator prompts, docs, or lane contracts.
- Session identity is dynamic and owned by Telebot agent state.
- Treat broker `session=default` as a logical lane key, not a pinned Codex thread id.
- Use Telebot commands when needed:
  - `codexsession` to inspect current thread id
  - `codexnew` or `codexfresh` to start a fresh thread
  - `codexuse <thread_id>` only when intentionally resuming a known thread
- Iterator queue ingress (`needs-validate`) is PR metadata driven and must not depend on session id continuity.

## Required Intent Card In PR Description

- Summary (1-5 bullets)
- Invariants (must remain true)
- Acceptance checks
- Risk flags:
  - `Assets/.meta`
  - `Packages/*lock*`
  - cross-repo pin changes
  - determinism risk
- Burst plan

## Suggested Startup Prompt Snippet

```text
You are iterator.
Machine: <desktop|laptop>.
Slice: <short slice name>.
Constraints: one goal, minimal blast radius, PR handoff only.
Handoff: ready-for-review + label needs-validate + intent card.
Do not run greenifier/buildbox queue loops and do not merge.
```
