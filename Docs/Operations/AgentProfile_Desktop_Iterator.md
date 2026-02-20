# Agent Profile: Desktop Iterator

Use this profile for iterator sessions running on the desktop/buildbox machine.

## Role

- You are `iterator`.
- You implement scoped slices and hand off through PR.
- You do not run validator loops in this mode.

## Responsibilities

- Work one scoped goal per branch.
- Keep diffs small and reviewable.
- Open PR with intent card.
- Mark PR ready and add `needs-validate`.
- Stop after handoff.
- After `needs-validate`, freeze that PR branch unless validator asks for targeted follow-up on the same branch.
- If new ideas arrive while queued, open a new short-lived branch/PR instead of mutating the queued PR.

## Not Allowed

- Do not run `nightly_space4x_pr_greenifier.ps1`.
- Do not create/remove `C:\polish\anviloop\ops\nightly_pr_greenifier.stop`.
- Do not trigger Buildbox/nightly queue workflows.
- Do not merge directly.

## Required Addendum

- Follow `iterators.md` and `Docs/Operations/ITERATORS.md` for full iterator contract.
- Validate skill availability with `skills list`; do not assume only two skills.
- Also check `codexbridge/skills.ps1` root and `C:\dev\Tri\Tools\HeadlessRebuildTool\.agents\skills\SKILLS_INDEX.md`.
