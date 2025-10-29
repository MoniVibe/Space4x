# Space4X DOTS Alignment Request

This note captures the DOTS-side work needed so crews, fleets, and factions can react to alignment and doctrine conflicts in a deterministic way. Treat the items below as the minimum viable slice for gameplay to consume.

## Components & Data Shapes
- **Wire up alignment data**: Ensure every crew member prefab gains `AlignmentTriplet`, `RaceId`, `CultureId`, `DynamicBuffer<EthicAxisValue>`, and `DynamicBuffer<OutlookEntry>`. Aggregated crews should maintain `DynamicBuffer<TopOutlook>`, `RacePresence`, and `CulturePresence`.
- **Affiliations**: Populate `DynamicBuffer<AffiliationTag>` for crews, fleets, colonies, and factions. Loyalty should be derived from morale/contract state on spawn.
- **DoctrineAuthoring**: Expose a monobehaviour/baker that maps to `DoctrineProfile`, `DynamicBuffer<DoctrineAxisExpectation>`, and `DynamicBuffer<DoctrineOutlookExpectation>` so designers can define expectations per empire/faction/fleet template.

## Systems
- **Aggregation pass**: Implement a `CrewAggregationSystem` that recalculates weighted alignments/outlooks when membership changes and writes filtered results into the buffers consumed by compliance.
- **Compliance**: Integrate `Space4XAffiliationComplianceSystem` into the Simulation group ordering, making sure it runs after aggregation and before command systems. Hook breaches to AI/planning pipelines (e.g., convert `ComplianceBreach` into mutiny/desertion command buffers).
- **Suspicion decay routing**: Feed `SuspicionScore` deltas into intel/alert systems so UI/telemetry can surface looming spy exposure.

## Authoring & Tooling
- **Enum registry**: Generate/maintain shared enums for `EthicAxisId`, `OutlookId`, and `AffiliationType` to keep authoring, narrative, and DOTS code aligned.
- **Inspector helpers**: Add custom inspectors or baker validation to guard against doctrine ranges that conflict (e.g., min > max) or crews with more than two fanatic convictions.
- **Sample scenes**: Create a micro test scene with a captain, crew, and faction doctrine to validate mutiny/desertion flows.

## Testing
- **Edit mode**: Add NUnit coverage that feeds synthetic alignments/ethics into `Space4XAffiliationComplianceSystem` and asserts breach type, severity scaling with loyalty, and spy suspicion behavior.
- **Runtime assertions**: Instrument aggregation/compliance with Unity assertions (enabled in dev builds) to catch missing doctrine data or empty affiliation buffers at runtime.

## Integration Hooks
- **AI planner**: Consume `ComplianceBreach` output to spawn behavioral tickets (mutiny state machine, desertion escape paths, independence colony creation).
- **Telemetry**: Extend the registry bridge snapshot to include counts of current breaches and mean suspicion so ops dashboards reflect moral stability.
- **Narrative triggers**: Forward breach events into narrative/quest systems for bark generation and incident scripts.
