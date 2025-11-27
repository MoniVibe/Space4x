# Agent B – Modules, Degradation, Skills Follow-Through

Scope: module slot/refit pipeline, degradation/repair flows, and extending skills into refit/repair/combat/haul/hazard paths.

## Module System
- Physical slot system on carriers; modules as entities for per-instance health/repair.
- Refit mechanics: removing → empty → installing → active; archetype transitions supported.
- Stat aggregation from active modules; refit gating via nearby `RefitFacility`/tech.
- Authoring/bakers for module definitions and slot configs; registry-friendly data.
- Progress: slots + module authoring + refit queue/aggregation landed; maintenance log/telemetry now recorded during refit/repair/degradation.
- **NEW**: Module/hull catalog system with blob assets (`ModuleCatalogAuthoring`, `HullCatalogAuthoring`, `RefitRepairTuningAuthoring`). Default catalogs bootstrap automatically. Refit time calculated via formula: `BaseRefit + MassSecPerTon * mass * SizeMult * LocationMult + RewirePenalty`. Facility proximity system (`FacilityProximitySystem`) adds/removes `InRefitFacilityTag` based on radius. Refit gating: `(InRefitFacilityTag || (HullSpec.FieldRefitAllowed && tuning.GlobalFieldRefitEnabled))`.

## Degradation & Repair
- Track per-module/component health with degradation rates and repair priority.
- Field repair capped (e.g., 80%); station overhaul for full repair; failure disables module.
- Repair queues processed by priority; hazard/combat hooks drive degradation.
- Progress: degradation + field repair systems emit maintenance log/telemetry + repair XP. Still need station overhaul path and tech/facility gating for refits/repairs.
- **NEW**: Repair rates now use `RefitRepairTuning` values (`RepairRateEffPerSecStation` vs `RepairRateEffPerSecField`). Facility proximity affects repair speed. Module rating aggregation system (`Space4XModuleRatingAggregationSystem`) computes offense/defense/utility ratings and power balance from installed modules × efficiency.

## Skills Integration
- Broaden XP sources; apply modifiers to refit/repair/combat/hauling; integrate hazard resistance.
- Telemetry/command-log entries for refit/repair outcomes.
- Progress: repair/refit award XP and log maintenance events. Next: hook combat/hauling/hazard XP and replay-safe command-log streams.

### Specific Tasks for Skills Integration
- **Combat XP**: Add XP awards to combat systems when modules are used in combat (weapon modules fire, shields absorb damage, point defense intercepts)
  - Hook into combat damage/defense systems to award `CrewSkills.CombatSkill` XP
  - Log combat events to command log for replay support
- **Hauling XP**: Add XP awards when vessels successfully haul resources between carriers/stations
  - Hook into `CarrierPickupSystem` and `VesselDepositSystem` to award `CrewSkills.LogisticsSkill` XP
  - Track haul distance/efficiency for XP scaling
- **Hazard XP**: Add XP awards when vessels mitigate hazards (radiation, debris, anomalies)
  - Hook into `Space4XHazardMitigationSystem` to award hazard-specific skill XP
  - Different hazards may award different skill types (Engineering for system failures, Tactics for combat hazards)
- **Skill Modifiers**: Apply skill bonuses to refit/repair/combat/haul operations
  - Engineering skill → faster refit/repair times, reduced failure chance
  - Logistics skill → faster haul speeds, increased cargo capacity
  - Combat skill → improved weapon accuracy, faster targeting
  - Mining skill → already implemented (+50% output at max skill)

## Testing
- Module swap sequences (remove → install → activate) and archetype transition determinism (rewind-safe).
- Stat aggregation with multiple modules; repair priority ordering.
- Skills affecting refit/repair/combat/haul/hazard paths with coverage.
- **COMPLETE**: Scenario demo (`Assets/Scenarios/space4x_demo_refit.json`) exercises degradation → field repair → facility refit. Telemetry expectations include refit count, repair count, module restoration, and offense rating improvements. Tests added (`Space4XRefitScenarioSystemTests`) covering catalog lookups, refit time calculation, and facility proximity.

## Demo Readiness
- **COMPLETE**: Burst compatibility verified (action processor uses managed code intentionally, other systems Burst-compiled). Catalog assets can be generated via `Tools/Space4X/Create Refit Catalog Assets`. Telemetry metrics wired: `space4x.modules.ratings.*`, `space4x.modules.power.balanceMW`, `space4x.modules.refit.*`, `space4x.modules.repair.*`. Scenario loader reads JSON and spawns entities; action processor executes timed actions. Run scenario via ScenarioRunner: `Unity -batchmode -projectPath . -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario Assets/Scenarios/space4x_demo_refit.json`.
