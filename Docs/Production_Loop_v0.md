# Production Loop v0 (Space4x adapter notes)

**Goal**: Share a minimal extraction + production loop via PureDOTS, then adapt to Space4x modules/ship parts and shipyards.

- **Extraction**: keep current mining loop; map output to production inputs.
- **Facilities**: module fabs (non-shipyard) + shipyards. Ships are built **only** in shipyards; modules/resources are produced in other facilities.
- **Recipes**: ore → ingot/alloy; parts → module items; shipyard consumes parts/alloy/ingots to produce hull items.
- **Stockpile**: carrier cargo or station storage as `ResourceStockpileRef`; module outputs are items until equipped.
- **Shipyard MVP**: `BusinessType.Builder` stands in for shipyards; module fabs use `BusinessType.Blacksmith` until we split explicit facility classes.
- **Telemetry**: slot utilization + throughput; use for headless validation.
- **Inputs**: allow tag-based “any fuel/any food” inputs so multiple resource types can satisfy recipes.
- **Outputs**: optional tag-based outputs for generic resource variants (e.g., any fuel). Hull outputs use hull IDs (e.g., `lcv-sparrow`).
- **Crew**: production uses abstract crew pools (planets = millions; ships/stations = pooled crew) for throughput modifiers later.
- **Permissions**: default generic (owner/contracted) with a later pass for faction policy nuance.

Suggested next docs: `C:/Dev/unity_clean/puredots/Docs/Production_Loop_v0.md`.
