# Space4X Runtime Ordering Guardrails

Status: active contract for Space4X runtime wiring and fleet-crawl slice stability.

If this document conflicts with older docs, treat these as authoritative in this order:
1. `../../../puredots/Docs/TruthSources/RuntimeLifecycle_TruthSource.md`
2. `Docs/Simulation/Space4X_Runtime_Ordering_Guardrails.md` (this file)

## Why This Exists

Space4X can appear to "work" while running with invalid system ordering. Unity logs warnings, then silently falls back to a different schedule. That creates hidden stutter, drift, and non-deterministic behavior.

This file codifies hard runtime ordering rules so agents and contributors do not reintroduce invalid ordering.

## Non-Negotiable Rules

1. `UpdateAfter` and `UpdateBefore` only count when both systems are in the same parent group.
2. Use `CreateAfter` and `CreateBefore` for creation-time dependencies (`OnCreate` ordering), not update ordering.
3. Do not create ad-hoc worlds for gameplay slices unless explicitly required. Prefer one profile-selected world with explicit system inclusion.
4. Use `DisableAutoCreation` for non-slice systems and add only required systems for the slice profile.
5. Keep bootstrap lightweight. Bootstrap must configure and register; heavy simulation work belongs in scheduled groups.
6. Keep one transform authority per phase. Do not mix simulation writes from multiple systems without explicit ordering and ownership.
7. Do not treat `LocalToWorld` as simulation authority when deterministic gameplay state comes from `LocalTransform`.
8. Structural changes in hot loops must be batched via ECB playback points, not scattered direct writes.
9. Use `RequireForUpdate` / `RequireAnyForUpdate` / `RequireMatchingQueriesForUpdate` so idle systems do not run.
10. Any ignored ordering warning in console is a runtime bug, not cosmetic noise.

## Fleet Crawl Minimal Runtime Contract

1. Keep only slice-required simulation systems in active profile.
2. Disable or gate RTS-heavy systems by default for Fleet Crawl runs.
3. Keep camera/follow and flagship control in a deterministic phase with a single target authority.
4. Cap background presentation workloads that can trigger catch-up cascades.
5. Ensure fixed-step settings are bounded so frame spikes do not produce runaway multi-step catch-up.

## Automated Ordering Audit (Required)

Use the cross-repo audit script before merge:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/runtime_ordering_audit.ps1
```

Output:
- Writes a report to `space4x/reports/runtime_ordering_audit_*.md`.
- Scans both `space4x/Assets/Scripts` and `puredots/Packages/com.moni.puredots/Runtime`.

Merge gate:
1. `Potential mismatches` must be `0`.
2. Any unresolved relation touching files changed in the PR must be manually triaged.
3. If unresolved relations are intentionally accepted, record the reason in the PR description.

## Failure Signatures And Canonical Fixes

### Signature: `Ignoring invalid [Unity.Entities.UpdateAfterAttribute] ...`

- Meaning: ordering relation does not apply because systems are not siblings under the same `ComponentSystemGroup`.
- Fix:
1. Move both systems under the same group if ordering is required.
2. Or replace with group-level ordering (`UpdateInGroup` on proper parent groups).
3. For init-only dependency, use `CreateAfter` / `CreateBefore`.

### Signature: high frame spikes in background systems (for example chunk mesh build over budget)

- Meaning: heavy work is exceeding frame budget and forcing perceived jitter/catch-up.
- Fix:
1. Throttle, budget, or amortize heavy builders.
2. Move non-critical work to later phases or lower frequency.
3. Validate fixed-step and max timestep settings after throttling.

### Signature: camera drift / ship leaves frame despite follow mode

- Meaning: camera target and vessel pose are being sampled from different authorities or phases.
- Fix:
1. Use one pose authority for controlled flagship in follow path.
2. Keep follow sampling and camera application in deterministic late frame order.
3. Verify no competing camera drivers are active in non-RTS modes.

## Agent Checklist Before Merge

1. Zero `Ignoring invalid [Unity.Entities.UpdateAfterAttribute]` warnings in startup logs.
2. Systems window confirms expected parent groups and sibling ordering constraints.
3. No multi-writer transform ownership for flagship/camera path.
4. Presentation/background jobs stay within budget under normal speed and elevated speed.
5. Fleet Crawl slice profile loads only intended systems.
6. Updated docs if ordering contracts changed.

## Canonical External References

- Unity Entities system update order:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/systems-update-order.html
- Unity Entities custom bootstrap:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/systems-icustombootstrap.html
- Unity `DisableAutoCreationAttribute`:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.DisableAutoCreationAttribute.html
- Unity fixed-step simulation group:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Entities.FixedStepSimulationSystemGroup.html
- Unity transform authority guidance (`LocalToWorld`):
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/api/Unity.Transforms.LocalToWorld.html
- Unity transform helpers (`ComputeWorldTransformMatrix`):
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/transforms-helpers.html
- Unity systems window:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/editor-systems-window.html
- Unity structural changes profiling:
  - https://docs.unity.cn/Packages/com.unity.entities%401.3/manual/profiler-module-structural-changes.html
