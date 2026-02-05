# Agent A – Alignment, Compliance, Doctrine

Scope: alignment/affiliation data, aggregation, compliance, doctrine authoring, and related tests/telemetry.

## Components & Data Shapes
- Add `AlignmentTriplet`, `RaceId`, `CultureId`, `DynamicBuffer<EthicAxisValue>`, `DynamicBuffer<StanceEntry>` to crew prefabs.
- Maintain aggregated buffers on crews/fleets/colonies/factions: `DynamicBuffer<TopStance>`, `RacePresence`, `CulturePresence`.
- Populate `DynamicBuffer<AffiliationTag>` for crews, fleets, colonies, factions; derive loyalty from morale/contract at spawn.
- Expose `DoctrineAuthoring` baker → `DoctrineProfile`, `DynamicBuffer<DoctrineAxisExpectation>`, `DynamicBuffer<DoctrineOutlookExpectation>`.

## Systems
- `CrewAggregationSystem`: recompute weighted alignments/outlooks on membership changes; write buffers consumed by compliance.
- `Space4XAffiliationComplianceSystem`: place after aggregation, before command systems; convert `ComplianceBreach` into mutiny/desertion tickets.
- Route `SuspicionScore` deltas into intel/alert surfaces for UI/telemetry.

## Authoring & Tooling
- Enum registry for `EthicAxisId`, `StanceId`, `AffiliationType` shared across authoring/narrative/DOTS code.
- Inspector/baker validation: clamp doctrine ranges, cap fanatic convictions, guard invalid combos.
- Sample micro-scene for mutiny/desertion validation.

## Testing
- EditMode/NUnit: feed synthetic alignments/ethics into compliance; assert breach type, loyalty scaling, spy suspicion behavior.
- Runtime assertions in aggregation/compliance for missing doctrine data or empty affiliation buffers.

## Integration Hooks
- AI planner: consume `ComplianceBreach` to spawn mutiny/desertion behaviors (ComplianceTicket + ComplianceTicketQueue provided for deterministic routing).
- Telemetry: extend registry snapshot with breach counts/mean suspicion (`space4x.compliance.*` including suspicion max/alerts).
- Narrative triggers: forward breach events to bark/incident systems.

