# Iterator Addendum (Desktop + Laptop)

Use this addendum whenever the startup contract says `you are iterator`.

## Core Contract

- Iterate on one scoped goal per branch.
- Keep blast radius small and reviewable.
- Prepare handoff via PR; do not self-validate through Buildbox queues.
- Do not merge to `main`.

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
- Do not manually edit `C:\polish\queue\reports\pending_prs_to_greenify.md`.

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
