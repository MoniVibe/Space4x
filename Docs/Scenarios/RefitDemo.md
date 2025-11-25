# Space4X Refit Demo Scenario

## Overview
The refit demo (`Assets/Scenarios/space4x_demo_refit.json`) exercises the complete module degradation → repair → refit workflow with telemetry validation.

## Scenario Flow
1. **Spawn** (t=0s): Creates "lcv-sparrow" hull with 7-module loadout and a refit station at (200, 0).
2. **Degrade** (t=10s, 12s, 14s): Reduces engine, shield, and hangar efficiency to 60%, 55%, and 80% respectively.
3. **Field Repair** (t=45s): Triggers field repair on engine and shield modules (slower rate).
4. **Move to Station** (t=80s): Carrier moves to refit facility.
5. **Facility Refit** (t=95s): Swaps laser-s-1 → missile-s-1 (faster at facility, increases offense rating).

## Running the Scenario

### Via Unity Editor
1. Ensure catalog assets exist: `Tools/Space4X/Create Refit Catalog Assets`
2. Load `SampleScene.unity` or a scene with PureDOTS bootstrap
3. ScenarioRunner will automatically load `space4x_demo_refit.json` if ScenarioInfo is present

### Via Command Line (Batchmode)
```bash
Unity -batchmode -projectPath . -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario Assets/Scenarios/space4x_demo_refit.json --report Reports/refit_demo_report.json
```

## Telemetry Expectations

The scenario validates:
- `expectRefitCount: 1` - One refit completed (laser → missile)
- `expectFieldRepairCount: 1` - Field repairs applied
- `expectModulesRestoredTo>=0.95` - Engine and shield restored above 95% efficiency
- `expectNonNegativePowerBalance: true` - Power balance remains >= 0

### Assertions
- `OffenseImprovedAfterRefit`: Offense rating at t=110s > t=90s
- `DowntimeTracked`: Repair/refit events logged

## Telemetry Metrics

The following metrics are emitted to `TelemetryStream`:
- `space4x.modules.ratings.offense` - Total offense rating across all carriers
- `space4x.modules.ratings.defense` - Total defense rating
- `space4x.modules.ratings.utility` - Total utility rating
- `space4x.modules.power.balanceMW` - Net power balance (negative = generating, positive = consuming)
- `space4x.modules.degraded` - Count of modules below 95% efficiency
- `space4x.modules.repairing` - Count of modules currently being repaired
- `space4x.modules.refitting` - Count of modules currently being refit
- `space4x.modules.refit.count` - Total refits completed
- `space4x.modules.refit.duration.avg_s` - Average refit duration
- `space4x.modules.repair.count` - Total repairs applied
- `space4x.modules.repair.duration.avg_s` - Average repair duration

## Catalog Assets

Default catalogs are auto-created by `ModuleCatalogBootstrapSystem` if ScriptableObject assets don't exist. To customize:

1. Run `Tools/Space4X/Create Refit Catalog Assets` to generate assets in `Assets/Data/Catalogs/`
2. Modify the assets in Unity Inspector
3. The bakers will rebuild blob assets on domain reload

## Troubleshooting

- **Scenario not loading**: Ensure `ScenarioInfo` singleton exists (created by ScenarioRunner)
- **Modules not spawning**: Check that `ModuleCatalogBootstrapSystem` ran and catalogs exist
- **Actions not executing**: Verify `ScenarioActionScheduler` entity exists and `TimeState` is advancing
- **Telemetry empty**: Ensure `Space4XModuleTelemetryAggregationSystem` runs after rating aggregation

