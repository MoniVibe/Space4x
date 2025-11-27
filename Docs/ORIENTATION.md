# Space4X Project Orientation

**Last Updated**: 2025-11-23  
**Purpose**: Comprehensive overview of project structure, current state, and readiness for game development

---

## Project Overview

**Space4X** is a carrier-first 4X strategy game built on Unity DOTS, consuming the **PureDOTS** framework package. The game focuses on commanding carrier task forces across a living galaxy, with core loops around mining, hauling, exploration, and combat.

### Key Architecture Decisions

1. **PureDOTS Integration**: Space4X consumes `com.moni.puredots` package (file reference to `../../PureDOTS/Packages/com.moni.puredots`)
2. **ECS-First**: All gameplay logic uses Unity Entities/Components/Systems
3. **Time/Rewind**: Uses PureDOTS time pipeline (TickTimeState, RewindState, TimeControlCommand)
4. **Registry System**: Bridges Space4X entities to PureDOTS registries for spatial queries and telemetry
5. **Scenario Runner**: Headless testing via PureDOTS ScenarioRunner with JSON scenarios

---

## Project Structure

```
Assets/
├── Scripts/Space4x/
│   ├── Authoring/          # MonoBehaviour bakers for ECS conversion
│   ├── Registry/           # Core gameplay systems + registry bridge
│   ├── Systems/            # AI and movement systems
│   ├── Presentation/       # Visual assignment and binding
│   ├── Scenario/           # Scenario loader and action processors
│   ├── Runtime/            # Shared runtime components
│   └── Tests/              # EditMode and PlayMode tests
├── Scenes/
│   ├── Demo/               # Active demo scenes (mining, registry)
│   └── Hybrid/             # Archived hybrid showcase scenes
├── Data/
│   └── Catalogs/           # Module/hull/refit catalog assets
├── Settings/               # Render pipeline and config assets
└── Space4X/                # Space4X-specific config assets
```

---

## Core Systems Status

### ✅ Completed Systems

#### Registry & Telemetry
- **Space4XRegistryBridgeSystem**: Bridges colonies, fleets, logistics routes, anomalies to PureDOTS registries
- **Space4XRegistryTelemetrySystem**: Emits telemetry metrics for all registry data
- **Space4XRegistrySnapshot**: Aggregated snapshot of all registry state
- Spatial integration with PureDOTS spatial grid system

#### Mining Loop
- **Space4XMinerMiningSystem**: Core mining execution (FixedStep)
- **Space4XMiningYieldSpawnBridgeSystem**: Converts yield to spawn requests
- **MiningResourceSpawnSystem**: Creates SpawnResource entities
- **CarrierPickupSystem**: Transfers resources to carrier storage
- **Space4XMiningTelemetrySystem**: Mining telemetry metrics
- **Space4XMiningTimeSpine**: Time spine recording for rewind support

#### Module & Degradation System
- **Module/Hull Catalog System**: Blob assets for module/hull definitions (`ModuleCatalogAuthoring`, `HullCatalogAuthoring`, `RefitRepairTuningAuthoring`)
- **FacilityProximitySystem**: Detects refit facilities within radius
- **Space4XCarrierModuleRefitSystem**: Refit queue and execution
- **Space4XFieldRepairSystem**: Field repair (capped at 80%)
- **Space4XStationOverhaulSystem**: Full repair at stations
- **Space4XComponentDegradationSystem**: Degradation over time
- **Space4XModuleRatingAggregationSystem**: Computes offense/defense/utility ratings
- **Space4XModuleTelemetryAggregationSystem**: Module telemetry metrics

#### Alignment & Compliance
- **Space4XAffiliationComplianceSystem**: Compliance breach detection
- **Space4XCrewAggregationSystem**: Aggregates alignment/outlook buffers
- **Space4XComplianceTelemetrySystem**: Compliance telemetry
- **Space4XComplianceTicketQueueSystem**: Mutiny/desertion ticket routing
- **Space4XCompliancePlannerBridgeSystem**: Bridges to AI planner

#### Crew & Skills
- **Space4XCrewExperienceSystem**: XP and skill progression
- **Space4XCrewGrowthSystems**: Crew growth mechanics
- **Space4XCrewSkillUtility**: Skill calculations and modifiers

#### Tech Diffusion
- **Space4XTechDiffusionSystem**: Tech diffusion across factions
- **Space4XTechDiffusionTelemetrySystem**: Tech telemetry

#### Fleet & Intercept
- **Space4XFleetInterceptSystems**: Fleet interception mechanics
- **Space4XFleetCoordinationAISystem**: Fleet coordination AI

#### AI Systems
- **VesselAISystem**: Vessel target assignment
- **VesselMovementSystem**: Vessel movement
- **VesselTargetingSystem**: Target resolution
- **Space4XAIMaintenanceSystem**: Maintenance AI
- **Space4XAIMissionBoardSystem**: Mission board AI
- **Space4XAIDiplomacySystem**: Diplomacy AI
- **Space4XAIGovernanceSystem**: Governance AI

### 🚧 In Progress / Partial

#### Movement Systems
- **Issue**: Vessels with `MiningOrder` are excluded from movement systems (`VesselMovementSystem`, `VesselTargetingSystem`, `VesselAISystem` all have `[WithNone(typeof(MiningOrder))]`)
- **Status**: Mining loop works but movement needs integration decision

#### Scenario Runner
- **Space4XRefitScenarioSystem**: Loads JSON scenarios
- **Space4XRefitScenarioActionProcessor**: Executes timed actions
- **Status**: Functional but needs broader scenario support

#### Prefab Maker
- **Status**: Planned but not implemented (see `space.plan.md` section 7)
- **Goal**: Data-driven prefab generation from catalogs

### 📋 Planned / TODO

#### Agent A (Alignment/Compliance)
- Mutiny/desertion demo scene
- Scene-wide affiliation pass
- Narrative trigger integration

#### Agent B (Modules/Degradation)
- Extend skill XP into combat/haul/hazard flows
- Maintenance authoring/registry hooks
- Prefab Maker implementation

#### Agent C (Mobility/Economy/Tech)
- Mobility graph maintenance
- Economy pricing/queue handling
- Tech diffusion TimeState scaling audit

#### Phase 2 (Rewind/Time)
- PlayMode rewind determinism tests
- Registry continuity validation

---

## PureDOTS Integration

### What PureDOTS Provides

1. **Time System**: `TickTimeState`, `RewindState`, `TimeControlCommand`
2. **Registry Framework**: `RegistryDirectory`, `RegistryMetadata`, spatial registry support
3. **Spatial Grid**: `SpatialGridConfig`, `SpatialGridState`, `SpatialIndexedTag`
4. **Resource System**: `ResourceSourceState`, `ResourceSourceConfig`, `ResourceTypeId`
5. **Villager System**: `VillagerAlignment`, `VillagerBehavior`, `VillagerInitiativeState` (used for crew/pops)
6. **Miracle System**: `MiracleRegistry`, ability system
7. **Telemetry**: `TelemetryStream`, `TelemetryMetric`
8. **Scenario Runner**: Headless scenario execution

### Integration Points

- **Time**: All systems use `TimeState` from PureDOTS, no local time systems
- **Spatial**: Entities tagged with `SpatialIndexedTag`, use `SpatialGridResidency` for queries
- **Registry**: `Space4XRegistryBridgeSystem` mirrors Space4X entities to PureDOTS registries
- **Resources**: Mining uses PureDOTS `ResourceSourceState`/`ResourceSourceConfig`
- **Crew**: Uses PureDOTS `Villager*` components (pops/crew are "villagers" in PureDOTS terms)

---

## Key Components & Data Structures

### Carrier Components
- `Carrier`: Carrier identity
- `ResourceStorage` (buffer): Storage slots for resources
- `MiningOrder`: Active mining order
- `MiningState`: Mining state machine
- `MiningVessel`: Vessel mining data
- `MiningYield`: Pending yield amount

### Module Components
- `ModuleSlot`: Physical slot on carrier
- `ModuleInstance`: Installed module with health
- `ModuleCatalogSingleton`: Blob reference to module catalog
- `HullCatalogSingleton`: Blob reference to hull catalog
- `RefitRepairTuningSingleton`: Blob reference to tuning data
- `InRefitFacilityTag`: Proximity tag for refit facilities

### Registry Components
- `Space4XColony`: Colony data
- `Space4XFleet`: Fleet data
- `Space4XLogisticsRoute`: Logistics route data
- `Space4XAnomaly`: Anomaly data
- `Space4XRegistrySnapshot`: Aggregated snapshot

### Alignment Components
- `AlignmentTriplet`: Alignment data
- `DynamicBuffer<OutlookEntry>`: Outlook buffer
- `DynamicBuffer<AffiliationTag>`: Affiliation tags
- `DoctrineProfile`: Doctrine expectations
- `ComplianceBreach`: Breach events

---

## Testing Infrastructure

### Test Structure
- **EditMode Tests**: NUnit tests in `Assets/Scripts/Space4x/Tests/`
- **PlayMode Tests**: PlayMode tests in `Assets/Scripts/Space4x/Tests/PlayMode/`
- **Scenario Tests**: JSON scenarios in `Assets/Scenarios/`

### Key Test Files
- `Space4XRegistryBridgeSystemTests.cs`: Registry bridge tests
- `Space4XMinerMiningSystemTests.cs`: Mining system tests
- `Space4XModuleSystemsTests.cs`: Module system tests
- `Space4XRefitScenarioSystemTests.cs`: Scenario loader tests
- `Space4XTechDiffusionSystemTests.cs`: Tech diffusion tests

### Running Tests
```bash
# EditMode tests
Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode

# Scenario runner
Unity -batchmode -projectPath . -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario Assets/Scenarios/space4x_demo_refit.json
```

---

## Game Design Vision

### Core Pillars
- **Carrier-First**: Player commands through carriers, not avatars
- **Starting Modes**: Configurable starting conditions (trade, mining, combat, exploration)
- **Interdependent Loops**: Mining, hauling, exploration, combat interlock
- **Scale Target**: ~1 million active entities with responsiveness

### Core Loops
1. **Short-term** (minutes): Direct carriers to mine, scout, haul, trade, engage
2. **Mid-term** (session): Allocate resources, construct stations, unlock carriers
3. **Long-term** (campaign): Expand through colonization, logistics networks, conflicts

### Key Mechanics
- **Mining Loop**: Carriers extract resources from deposits
- **Haul Loop**: Transport resources between carriers and stations
- **Combat Loop**: Fleet engagements and interception
- **Module System**: Modular carrier customization with refit/repair
- **Tech Diffusion**: Research spreads unevenly across empire
- **Alignment/Compliance**: Crew alignment affects mutiny/desertion

---

## Current Development Status

### Milestone: Phase 2 Demo + Phase 3 Agents

**Agent A (Alignment/Compliance)**: In progress
- Doctrine baker + OutlookId enum added
- Compliance emits telemetry (suspicion max/alerts)
- Planner tickets + inbox implemented
- Next: Mutiny/desertion demo + scene-wide affiliation pass

**Agent B (Modules/Degradation)**: In progress
- ✅ Module/hull catalog system complete
- ✅ Facility proximity detection complete
- ✅ Refit/repair/health hooks emit events + XP
- ✅ Station-only refit gating + overhaul repairs
- ✅ Maintenance playback rebuilds telemetry
- Next: Extend skill XP into combat/haul/hazard flows

**Agent C (Mobility/Economy/Tech)**: In progress
- ✅ Tech diffusion baseline + telemetry/logging
- Next: Mobility graph maintenance + economy pricing/queue handling

**Phase 2 (Rewind/Time)**: In progress
- Next: PlayMode rewind determinism tests for mining → haul

---

## Known Issues & Gaps

### Critical Issues
1. **Movement System Gap**: Vessels with `MiningOrder` excluded from movement systems
   - Options: Remove `MiningOrder`, fix movement systems, or hybrid approach
   - See: `Docs/CarrierMiningDemo_MiningLoopDependencies.md`

2. **Resource Registry Population**: No system automatically populates `ResourceRegistryEntry` for asteroids
   - Mining works but registry integration incomplete

### Design Gaps
1. **Core Pillars**: `Docs/Conceptualization/CorePillars.md` is template (needs filling)
2. **Design Principles**: `Docs/Conceptualization/DesignPrinciples.md` partially filled
3. **Prefab Maker**: Not implemented (planned in `space.plan.md`)

### Integration Gaps
1. **Scenario Runner**: Needs broader scenario support beyond refit demo
2. **Presentation Binding**: `Space4XPresentationBinding` system exists but needs prefab generation
3. **HUD/Debug**: Should reuse PureDOTS `DebugDisplayReader` + `RewindTimelineDebug`

---

## Next Steps for Game Development

### Immediate Priorities
1. **Fix Movement Integration**: Resolve `MiningOrder` exclusion from movement systems
2. **Complete Core Pillars**: Fill out design pillars document
3. **Extend Scenario Support**: Broaden scenario runner beyond refit demo
4. **Prefab Maker**: Implement data-driven prefab generation

### Short-term Goals
1. **Agent A**: Mutiny/desertion demo scene
2. **Agent B**: Extend skill XP into combat/haul/hazard flows
3. **Agent C**: Mobility graph + economy pricing systems
4. **Phase 2**: Rewind determinism tests

### Long-term Vision
1. **Starting Modes**: Implement configurable starting conditions
2. **Combat Loop**: Full combat system with module integration
3. **Colonization**: Station building and colony management
4. **Tech Tree**: Full tech progression with diffusion
5. **Threat Ecosystem**: Space fauna, pirates, rival empires

---

## Documentation Index

### Core Documentation
- `Docs/INDEX.md`: Quick pointers to active documentation
- `Docs/PROJECT_SETUP.md`: Project structure and setup
- `Docs/Progress.md`: Rolling status (single source of truth)
- `Docs/ORIENTATION.md`: This document

### Design Documentation
- `Docs/Conceptualization/GameVision.md`: High-level game vision
- `Docs/Conceptualization/CorePillars.md`: Design pillars (template)
- `Docs/Conceptualization/DesignPrinciples.md`: Design principles
- `Docs/Conceptualization/Mechanics/`: Individual mechanic docs

### Integration Guides
- `Docs/PureDOTS_TimeIntegration.md`: Time/rewind integration
- `Docs/PureDOTS_ScenarioRunner_Wiring.md`: Scenario runner wiring
- `Docs/Guides/Space4X/SpatialAndMiracleIntegration.md`: Spatial/miracle integration

### TODO Lists
- `Docs/TODO/AgentA_Alignment.md`: Agent A tasks
- `Docs/TODO/AgentB_Modules_Degradation.md`: Agent B tasks
- `Docs/TODO/AgentC_Mobility_Economy.md`: Agent C tasks
- `Docs/TODO/Phase2_Demo_TODO.md`: Phase 2 tasks
- `Docs/TODO/4xdotsrequest.md`: PureDOTS feature requests

### Demo Documentation
- `Docs/CarrierMiningDemo_*.md`: Mining demo documentation
- `space.plan.md`: Demo readiness closure plan

---

## Development Workflow

### Opening Project
```bash
# Open Unity project
Unity -projectPath .
# Load SampleScene.unity for play-mode checks
```

### Building
```bash
# Windows build
Unity -batchmode -projectPath . -quit -buildWindows64Player Build/Space4x.exe
```

### Testing
```bash
# EditMode tests
Unity -batchmode -projectPath . -runTests -testResults Logs/EditModeResults.xml -testPlatform editmode

# Scenario runner
Unity -batchmode -projectPath . -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario Assets/Scenarios/space4x_demo_refit.json
```

### Code Style
- Four-space indentation
- PascalCase for public types/methods
- camelCase for locals/private fields (prefix `_` when needed)
- Group related code in assembly definitions (`Space4x.Gameplay.asmdef`)

---

## Key Files Reference

### Core Systems
- `Assets/Scripts/Space4x/Registry/Space4XRegistryBridgeSystem.cs`: Registry bridge
- `Assets/Scripts/Space4x/Registry/Space4XMinerMiningSystem.cs`: Mining execution
- `Assets/Scripts/Space4x/Registry/Space4XCarrierModuleRefitSystem.cs`: Module refit
- `Assets/Scripts/Space4x/Registry/Space4XFieldRepairSystem.cs`: Field repair
- `Assets/Scripts/Space4x/Registry/Space4XAffiliationComplianceSystem.cs`: Compliance

### Components
- `Assets/Scripts/Space4x/Registry/Space4XDemoComponents.cs`: Demo components
- `Assets/Scripts/Space4x/Registry/Space4XModuleComponents.cs`: Module components
- `Assets/Scripts/Space4x/Registry/Space4XAlignmentComponents.cs`: Alignment components
- `Assets/Scripts/Space4x/Runtime/VesselComponents.cs`: Vessel components

### Configuration
- `Packages/manifest.json`: Package dependencies (PureDOTS reference)
- `Assets/Space4X/Config/`: Space4X config assets
- `Assets/Data/Catalogs/`: Module/hull catalog assets

---

## Summary

Space4X is a well-structured DOTS-based 4X game with:
- ✅ Solid foundation: Registry, mining, modules, compliance systems
- ✅ PureDOTS integration: Time, spatial, registry, resources
- ✅ Testing infrastructure: EditMode, PlayMode, scenario tests
- 🚧 In progress: Movement integration, scenario support, prefab generation
- 📋 Planned: Combat loop, colonization, tech tree, threat ecosystem

**Ready for**: Fleshing out core loops, implementing missing mechanics, expanding scenario support, and building out the game vision.

**Next focus areas**: Movement system integration, completing design pillars, extending scenario support, and implementing prefab maker.

