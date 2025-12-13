# Scene Lockdown Checklist (Godgame)

GodgameBootstraps SubScene requirements:

- `PureDotsConfigAuthoring` must reference the Godgame runtime config asset (verify asset path; Space4X configs cannot sneak in).
- `SpatialPartitionAuthoring` must target the Godgame spatial profile. Its baker already warns/early-exits when `SpatialPartitionProfile` is null; keep that behavior to prevent silent defaults.

Ban list:

- No Space4X editor tooling or utility scripts can compile into Godgame assemblies. If shared editors are required, isolate them via asmdefs.

Baking invariants:

- Each singleton component type is authored exactly once. If multiple bakers need the same singleton (example: KnowledgeLessonEffectCatalog), consolidate ownership or merge data before emitting the component.

CI gate:

- Hook the scene sweep into the existing CLI validator: `PureDOTS.Editor.PureDotsAssetValidator.RunValidationFromCommandLine` (see `PureDOTS_AuthoringConventions`). Add a `-executeMethod <SweepEntryPoint>` to fail the build when missing scripts or invalid component attachments are found.

Runtime singleton guard (optional but recommended):

- Add a startup invariant check similar to Space4X’s `SingletonCleanupSystem`. Prefer failing loudly in dev builds if duplicate singleton entities appear, so bootstrap issues are caught immediately.
