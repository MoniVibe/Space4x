# Agent Slot Locking

One slot owner at a time. Claim before editing.

## Slots

| Slot | Owner Role | Rule |
|------|------------|------|
| VALIDATION | Validator only | Only Validator may claim. Only Validator may run canonical validations or declare "super green." |
| SCALE_HARNESS | Perf/Harness | Perf owns scale harness + perf gates + report schemas. |
| TASK_WIRING | Ops | Ops owns task wiring + headless tooling. |
| DEMO_SLICE | Builder | Builder implements demo-slice features. |
| DOCS | Docs | Docs updates documentation only. |

## Claiming

1. Before touching files, identify your role and the slot you need.
2. Check this file for any active claim note (if the workflow uses handoff files, check `C:\polish\handoff\` or equivalent).
3. Record your claim (role + slot + brief intent) when you start work.
4. Release the slot when done (remove claim or hand off).

## Policy

- Slot locking is **policy**, not code. Agents must comply; there is no runtime enforcement.
- Overlaps cause duplicate work and confusion. When in doubt, yield to an active claim.
- Validator is the single source of "green" verdicts. No other role may declare validated.
