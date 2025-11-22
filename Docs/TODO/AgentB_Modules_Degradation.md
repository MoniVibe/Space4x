# Agent B – Modules, Degradation, Skills Follow-Through

Scope: module slot/refit pipeline, degradation/repair flows, and extending skills into refit/repair/combat/haul/hazard paths.

## Module System
- Physical slot system on carriers; modules as entities for per-instance health/repair.
- Refit mechanics: removing → empty → installing → active; archetype transitions supported.
- Stat aggregation from active modules; refit gating via nearby `RefitFacility`/tech.
- Authoring/bakers for module definitions and slot configs; registry-friendly data.
- Progress: slots + module authoring + refit queue/aggregation landed; maintenance log/telemetry now recorded during refit/repair/degradation.

## Degradation & Repair
- Track per-module/component health with degradation rates and repair priority.
- Field repair capped (e.g., 80%); station overhaul for full repair; failure disables module.
- Repair queues processed by priority; hazard/combat hooks drive degradation.
- Progress: degradation + field repair systems emit maintenance log/telemetry + repair XP. Still need station overhaul path and tech/facility gating for refits/repairs.

## Skills Integration
- Broaden XP sources; apply modifiers to refit/repair/combat/hauling; integrate hazard resistance.
- Telemetry/command-log entries for refit/repair outcomes.
- Progress: repair/refit award XP and log maintenance events. Next: hook combat/hauling/hazard XP and replay-safe command-log streams.

## Testing
- Module swap sequences (remove → install → activate) and archetype transition determinism (rewind-safe).
- Stat aggregation with multiple modules; repair priority ordering.
- Skills affecting refit/repair/combat/haul/hazard paths with coverage.
