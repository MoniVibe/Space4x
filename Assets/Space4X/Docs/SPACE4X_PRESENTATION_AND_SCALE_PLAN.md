# Space4X Presentation, Input & Scale Plan

**Status**: Implementation Document  
**Target**: Space4X Demo_01 + Scale Architecture  
**Dependencies**: PureDOTS Performance Plan, DOTS 1.4.3, Entities Graphics, New Input System  
**Last Updated**: 2025-12-01

---

## Executive Summary

This document defines how Space4X renders, interacts with, and scales to millions of entities using Unity DOTS 1.4.3 and Entities Graphics. Space4X consumes PureDOTS as the deterministic simulation foundation and adds presentation, input, UX, and game-specific visual systems on top.

**Key Principles:**
- Zero GameObject-based rendering for hot entities (Entities Graphics only)
- PureDOTS owns simulation data; Space4X owns presentation data
- Frame-time for presentation, tick-time for simulation
- Progressive LOD from full detail → impostors → hidden
- Input System bridges MonoBehaviour → ECS singleton components

---

## 1. Vertical Slice Definition: Space4X Demo_01

### 1.1 Scope

**Demo_01** is the first visual demonstration that proves Space4X can render and interact with carrier-based fleets at scale. It focuses on the mining loop as the core gameplay demonstration.

**Minimum Viable Demo:**
- **2-4 Carriers** (bands) with distinct faction colors
- **8-16 Mining Crafts** (2-4 per carrier) that mine and return resources
- **10-20 Asteroids** distributed across the play area
- **Basic Resource Gathering Loop**: Crafts mine asteroids → return to carriers → carriers accumulate resources
- **Simple Fleet Behavior**: Carriers patrol in formation, crafts maintain relative positions
- **Visual State Communication**: Mining state (crafts at asteroids), returning state (crafts moving to carriers), idle state (crafts docked)

**Entity Count Target:**
- **Demo_01**: ~50-100 entities (carriers + crafts + asteroids + resources)
- **Scale Test 1**: 10k entities
- **Scale Test 2**: 100k entities
- **Scale Test 3**: 1M entities (with LOD/impostors)

### 1.2 Entity Types to Render

#### Carriers
- **Sim-side Data**: `Carrier`, `LocalTransform`, `CarrierHullId`, `ResourceStorage`, `Space4XFleet`, `AffiliationTag`
- **View-side Data**: `CarrierPresentationTag`, `PresentationLOD`, `FactionColor`, `CarrierVisualState`, `MaterialPropertyOverride`

#### Mining Crafts
- **Sim-side Data**: `MiningVessel`, `LocalTransform`, `VesselAIState`, `MiningOrder`
- **View-side Data**: `CraftPresentationTag`, `PresentationLOD`, `FactionColor`, `CraftVisualState`, `MaterialPropertyOverride`

#### Asteroids
- **Sim-side Data**: `Asteroid`, `LocalTransform`, `ResourceSourceState`, `ResourceSourceConfig`, `ResourceTypeId`
- **View-side Data**: `AsteroidPresentationTag`, `PresentationLOD`, `ResourceTypeColor`, `AsteroidVisualState`, `MaterialPropertyOverride`

#### Resource Pickups
- **Sim-side Data**: `SpawnResource`, `LocalTransform`
- **View-side Data**: `ResourcePickupPresentationTag`, `PresentationLOD`, `ResourceTypeColor`, `MaterialPropertyOverride`

#### Fleets (Aggregate)
- **Sim-side Data**: `Space4XFleet`, `LocalTransform`, `FleetAggregateData`
- **View-side Data**: `FleetImpostorTag`, `FleetIconMesh`, `FleetVolumeBubble`, `FleetStrengthIndicator`

---

## 2. Rendering Architecture

### 2.1 Component Architecture

All presentation components are defined in `Space4X.Presentation` namespace:

**Tag Components:**
- `CarrierPresentationTag` - Marker for carrier entities
- `CraftPresentationTag` - Marker for craft entities
- `AsteroidPresentationTag` - Marker for asteroid entities
- `ResourcePickupPresentationTag` - Marker for resource pickups
- `FleetImpostorTag` - Marker for fleet impostors
- `ShouldRenderTag` - Marker for entities that should render (density sampling)
- `SelectedTag` - Marker for selected entities

**State Components:**
- `PresentationLOD` - LOD level and camera distance
- `CarrierVisualState` - Carrier visual state (Idle, Patrolling, Mining, Combat, Retreating)
- `CraftVisualState` - Craft visual state (Idle, Mining, Returning, Docked)
- `AsteroidVisualState` - Asteroid visual state (Full, MiningActive, Depleted)
- `MaterialPropertyOverride` - Material color/emissive overrides

**Color Components:**
- `FactionColor` - Per-entity faction color (float4 RGBA)
- `ResourceTypeColor` - Per-entity resource type color

**Configuration Singletons:**
- `PresentationLODConfig` - LOD distance thresholds
- `RenderDensityConfig` - Render density settings
- `PerformanceBudgetConfig` - Performance budget limits

### 2.2 Authoring Components

**Carrier Presentation:**
```csharp
CarrierPresentationAuthoring
├── HullId (string)
├── FactionColorValue (Color)
├── CarrierMesh (optional)
├── CarrierMaterial (optional)
└── InitialState (CarrierVisualStateType)
```

**Craft Presentation:**
```csharp
CraftPresentationAuthoring
├── CraftTypeId (string)
├── ParentCarrierObject (GameObject)
├── FactionColorValue (Color)
└── InitialState (CraftVisualStateType)
```

**Asteroid Presentation:**
```csharp
AsteroidPresentationAuthoring
├── ResourceTypeValue (ResourceType)
├── AsteroidMesh (optional)
├── AsteroidMaterial (optional)
├── InitialState (AsteroidVisualStateType)
└── InitialDepletionRatio (float 0-1)
```

### 2.3 Core Presentation Systems

**LOD System:**
- `Space4XPresentationLODSystem` - Assigns LOD levels based on camera distance
- `Space4XRenderDensitySystem` - Manages ShouldRenderTag based on density

**Entity Presentation Systems:**
- `Space4XCarrierPresentationSystem` - Updates carrier visual state and materials
- `Space4XCarrierStateFromFleetSystem` - Derives carrier state from fleet posture
- `Space4XCraftPresentationSystem` - Updates craft visual state and materials
- `Space4XAsteroidPresentationSystem` - Updates asteroid visual state and materials
- `Space4XResourcePickupPresentationSystem` - Updates resource pickup materials
- `Space4XFleetImpostorSystem` - Manages fleet impostor rendering

**Selection Systems:**
- `Space4XSelectionSystem` - Handles entity selection from input
- `Space4XSelectionHighlightSystem` - Highlights selected entities

**Metrics Systems:**
- `Space4XPresentationMetricsSystem` - Collects presentation metrics
- `Space4XPerformanceBudgetSystem` - Auto-adjusts LOD/density based on budgets

---

## 3. LOD Strategy

### 3.1 Distance Bands

| LOD Level | Distance Range | What Renders |
|-----------|---------------|--------------|
| FullDetail | 0-100 units | Full mesh, all crafts visible |
| ReducedDetail | 100-500 units | Simplified mesh, sampled crafts |
| Impostor | 500-2000 units | Fleet icon at centroid |
| Hidden | >2000 units | Not rendered |

### 3.2 Render Density Sampling

For crafts, a stable sampling algorithm determines which entities render:

```csharp
bool ShouldRenderCraft(Entity entity, float density)
{
    uint hash = entity.Index;
    float sampleValue = (hash % 1000) / 1000.0f;
    return sampleValue < density;
}
```

---

## 4. Input System

### 4.1 Input Components

**Selection Input:**
- `SelectionInput` - Click position, box selection, shift modifier, deselect

**Command Input:**
- `CommandInput` - Cycle fleets, toggle overlays, debug view

**Selection State:**
- `SelectionState` - Selected count, primary entity, selection type

### 4.2 Input Bridge

`Space4XSelectionInputBridge` MonoBehaviour:
- Reads Input System actions every frame
- Writes to ECS singleton components
- Creates default input actions if none provided

---

## 5. Performance Budgets

### 5.1 Default Budgets

| Budget | Value |
|--------|-------|
| Max Full Detail Carriers | 100 |
| Max Full Detail Crafts | 1000 |
| Max Reduced Detail Entities | 10,000 |
| Max Fleet Impostors | 1,000 |
| Max Draw Calls | 500 |
| Frame Time Budget | 16ms |

### 5.2 Auto-Adjustment

When enabled, the system automatically:
- Reduces LOD thresholds when over budget
- Reduces render density when too many visible crafts
- Increases thresholds/density when well under budget

---

## 6. Implementation Files

### Components
- `Assets/Scripts/Space4x/Presentation/Space4XPresentationComponents.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XInputComponents.cs`

### Authoring
- `Assets/Scripts/Space4x/Presentation/CarrierPresentationAuthoring.cs`
- `Assets/Scripts/Space4x/Presentation/CraftPresentationAuthoring.cs`
- `Assets/Scripts/Space4x/Presentation/AsteroidPresentationAuthoring.cs`
- `Assets/Scripts/Space4x/Presentation/Demo01Authoring.cs`

### Systems
- `Assets/Scripts/Space4x/Presentation/Space4XPresentationLODSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XCarrierPresentationSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XCraftPresentationSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XAsteroidPresentationSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XFleetImpostorSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XSelectionSystem.cs`
- `Assets/Scripts/Space4x/Presentation/Space4XPresentationMetrics.cs`

### Input Bridge
- `Assets/Scripts/Space4x/Presentation/Space4XSelectionInputBridge.cs`

---

## 7. Usage

### Adding Presentation to Existing Entities

Add the appropriate authoring component to your prefab or SubScene:

1. **Carriers**: Add `CarrierPresentationAuthoring`
2. **Crafts**: Add `CraftPresentationAuthoring`
3. **Asteroids**: Add `AsteroidPresentationAuthoring`

### Demo_01 Setup

1. Create a new scene with camera and lighting
2. Add a GameObject with `Demo01Authoring` component
3. Add a GameObject with `Space4XSelectionInputBridge` component
4. Add `Space4XMiningDemoAuthoring` for entity spawning
5. Enter Play mode

### Scale Testing

1. Configure `PerformanceBudgetConfigAuthoring` with desired limits
2. Enable `AutoAdjustLOD` and `AutoAdjustDensity`
3. Load scale scenario (10k, 100k, 1M entities)
4. Monitor `PresentationMetrics` via debug UI

---

## 8. Data Contracts with PureDOTS

**Required from PureDOTS:**
- `FleetAggregateData` component (centroid, strength, faction)
- `SpatialIndexedTag` for spatial queries
- `SpatialGridResidency` for LOD distance calculations
- Stable entity identifiers for render density sampling

**Space4X Responsibilities:**
- Read PureDOTS components (don't modify)
- Add presentation components to same entities
- Use frame-time for presentation (not tick-time)
- Respect PureDOTS spatial grid for LOD calculations

---

## 9. Success Criteria

**Demo_01:**
- 50-100 entities render with distinct faction colors
- Mining loop is visually clear
- Camera controls work smoothly
- Selection works
- Debug UI shows entity stats

**Scale:**
- 10k entities at 60 FPS with LOD
- 100k entities at 60 FPS with LOD + impostors
- 1M entities at 30+ FPS with aggressive LOD + impostors

---

**End of Document**

