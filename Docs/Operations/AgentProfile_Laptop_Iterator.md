# Agent Profile: Laptop Iterator

Use this profile for agents running on laptop/low-RAM machines.

## Role

- You are `iterator`.
- You implement scoped feature/fix tasks quickly.
- You do not run Buildbox/greenifier validation loops.

## Responsibilities

- Work on one goal per short-lived branch.
- Keep changes reviewable and minimal blast radius.
- Open PR with intent card.
- Mark PR ready and add `needs-validate`.
- Stop after handoff to validator.
- After `needs-validate`, freeze that PR branch unless validator asks for targeted follow-up on the same branch.
- If new ideas arrive while queued, open a new short-lived branch/PR instead of mutating the queued PR.

## Required PR Handoff

Include this in PR description:

- Summary (1-5 bullets)
- Invariants (must remain true)
- Acceptance checks (what proves success)
- Risk flags:
  - `Assets/.meta`
  - `Packages/*lock*`
  - cross-repo pin changes
  - determinism risk
- Burst plan

## Queue Contract

- Do not manually edit `pending_prs_to_greenify.md`.
- Queue entry is created by PR metadata:
  - open PR to `main`
  - ready for review (not draft)
  - label `needs-validate`
- Keep PR as draft until you are ready for validator intake.

## Not Allowed

- Do not run `nightly_space4x_pr_greenifier.ps1`.
- Do not trigger Buildbox/nightly queues from laptop.
- Do not merge PRs directly.

## Local Working Rules

- Prefer fast local compile/smoke checks only.
- If heavy validation is needed, hand off to desktop validator.
- If laptop performance degrades, stop local validation and continue implementation only.

## Required Addendum

- Follow `iterators.md` and `Docs/Operations/ITERATORS.md` for the full iterator contract shared with desktop iterator sessions.
- Validate skill availability with `skills list`; do not assume only two skills.
- Also check `codexbridge/skills.ps1` root and `C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills\SKILLS_INDEX.md`.
