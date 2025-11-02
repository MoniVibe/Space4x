# Pure DOTS Template Checklist

## When Starting a New Project
- [ ] Duplicate `Assets/Scenes/PureDotsTemplate.unity` or create a new scene referencing `PureDotsRuntimeConfig`.
- [ ] Review `Packages/manifest.json` and remove optional packages noted in `Docs/DependencyAudit.md` if not required.
- [ ] Configure CI scripts (`CI/run_playmode_tests.sh`) for automated validation.

## When Adding New Systems
- [ ] Decide which system group the new system belongs to (see `Docs/SystemOrdering/SystemSchedule.md`).
- [ ] Add `UpdateInGroup`, `UpdateAfter`, and `UpdateBefore` attributes to maintain deterministic ordering.
- [ ] Provide headless tests in `Assets/Tests/` covering the new behaviour.
- [ ] Document any new configuration assets or workflows in `Docs/EnvironmentSetup.md`.

## Before Shipping Template Updates
- [ ] Ensure debugging utilities (HUD/gizmos) are optional and disabled in production builds.
- [ ] Verify playmode tests and CI scripts pass locally.
- [ ] Update `Docs/Progress.md` with a summary of changes.
