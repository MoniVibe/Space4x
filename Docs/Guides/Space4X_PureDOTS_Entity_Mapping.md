# Space4X PureDOTS Entity Mapping Guide

**Status:** Implementation Guide  
**Category:** Space4X Integration  
**Scope:** How Space4X maps PureDOTS entities to UI and ship representation  
**Created:** 2025-01-XX  
**Last Updated:** 2025-01-XX

---

## Purpose

This document explains how Space4X maps PureDOTS agnostic entities to Space4X-specific UI and ship representation layers.

---

## Entity Types in Space4X

### Individual Entities: Pops/Crew ("Villagers")

**PureDOTS Foundation:**
- Uses: `VillagerAlignment`, `VillagerBehavior`, `VillagerInitiativeState`
- Systems: All PureDOTS villager systems process these entities
- **Note:** In Space4X, these are called "pops" or "crew" but use the same PureDOTS components

**Space4X Layer:**
- **NO 3D Presentation:** No GameObject, no mesh, no animation
- **UI Representation:** Pop cards, crew rosters, tooltips
- **Ship Representation:** Pops are represented via the ship they pilot/man/captain
- **Child Vessel Representation:** Commanding officers are represented via their child vessels
- **Components:** `Space4XPopUI`, `Space4XShipAssignment`, `Space4XCrewRole`

**Example:**
```csharp
// PureDOTS entity (agnostic - same as Godgame!)
Entity popEntity;
AddComponent(popEntity, new VillagerAlignment { MoralAxis = 70 });
AddComponent(popEntity, new VillagerBehavior { BoldScore = 60 });

// Space4X presentation layer (UI + ship assignment only)
AddComponent(popEntity, new Space4XPopUI 
{ 
    PopCardPrefab = popCardUI,
    TooltipData = ComputeTooltipFromAlignment(alignment)
});
AddComponent(popEntity, new Space4XShipAssignment 
{ 
    AssignedShip = shipEntity,
    Role = CrewRole.Captain
});
```

### Aggregate Entities: Planets

**PureDOTS Foundation:**
- Uses: `VillagerAlignment`, `VillagerBehavior`, `VillagerInitiativeState` (same as individuals!)
- Systems: Same PureDOTS systems process aggregates
- **Computation:** Alignment/behavior computed from pop averages

**Space4X Layer:**
- **UI Representation:** Planet view panel, pop list, culture display
- **Ship Representation:** Planet stations, orbital platforms
- **Components:** `Space4XPlanetUI`, `Space4XPlanetStations`
- **Visualization:**
  - Alignment affects planet culture UI (xenophilic/xenophobic display)
  - Behavior affects planet policy options (bold = expansionist policies)
  - Initiative affects planet development rate (visible in planet UI)

**Example:**
```csharp
// PureDOTS aggregate entity (same components as individual!)
Entity planetEntity;
AddComponent(planetEntity, new VillagerAlignment { MoralAxis = 55 }); // Averaged from pops
AddComponent(planetEntity, new VillagerBehavior { BoldScore = 40 });  // Averaged from pops

// Space4X presentation layer (UI + stations only)
AddComponent(planetEntity, new Space4XPlanetUI
{
    CultureDisplay = ComputeCultureFromAlignment(alignment),
    PolicyOptions = ComputePoliciesFromBehavior(behavior)
});
AddComponent(planetEntity, new Space4XPlanetStations
{
    OrbitalStations = stationEntities
});
```

### Aggregate Entities: Fleets

**PureDOTS Foundation:**
- Uses: Fleet component + `VillagerAlignment`, `VillagerBehavior` (same alignment/behavior!)
- Systems: Same PureDOTS systems process fleets

**Space4X Layer:**
- **UI Representation:** Fleet panel, ship list, formation UI
- **Ship Representation:** Collection of ships in formation
- **Components:** `Space4XFleetUI`, `Space4XFleetFormation`
- **Visualization:**
  - Alignment affects fleet banner/colors (UI only)
  - Behavior affects fleet tactics (bold = aggressive, craven = defensive)
  - Initiative affects fleet action frequency

### Aggregate Entities: Sectors

**PureDOTS Foundation:**
- Uses: Sector component + `VillagerAlignment`, `VillagerBehavior` (same alignment/behavior!)
- Systems: Same PureDOTS systems process sectors

**Space4X Layer:**
- **UI Representation:** Sector map view, system list, governance panel
- **Ship Representation:** Sector-wide stations, defense platforms
- **Components:** `Space4XSectorUI`, `Space4XSectorStations`
- **Visualization:**
  - Alignment affects sector governance style (UI)
  - Behavior affects sector expansion policies
  - Initiative affects sector development rate

---

## Presentation Component Pattern

### Pattern: PureDOTS Foundation + Space4X UI/Ship Representation

**All entities follow this pattern:**

1. **PureDOTS Foundation (Agnostic):**
   ```csharp
   AddComponent(entity, new VillagerAlignment { ... });
   AddComponent(entity, new VillagerBehavior { ... });
   AddComponent(entity, new VillagerInitiativeState { ... });
   ```

2. **Space4X Presentation (Game-Specific):**
   ```csharp
   // For individual pops
   AddComponent(entity, new Space4XPopUI { ... });
   AddComponent(entity, new Space4XShipAssignment { ... });
   
   // For aggregate planets
   AddComponent(entity, new Space4XPlanetUI { ... });
   AddComponent(entity, new Space4XPlanetStations { ... });
   
   // For aggregate fleets
   AddComponent(entity, new Space4XFleetUI { ... });
   AddComponent(entity, new Space4XFleetFormation { ... });
   ```

3. **Space4X Systems (Game-Specific):**
   - Read PureDOTS components
   - Update UI components
   - Handle ship assignments
   - Render UI panels

### Presentation Pipeline (Semantic → Variant → Presenter)

Space4X now uses the shared PureDOTS presentation contract:

1. **Semantic authoring:** gameplay assigns `RenderSemanticKey` (and optional `RenderKey` for LOD hints), enables `MeshPresenter`, and adds `RenderFlags`. Spawners do _not_ pick meshes or materials directly.
2. **Variant resolution:** `ResolveRenderVariantSystem` consumes semantic ids plus `ActiveRenderTheme`, optional `RenderThemeOverride`, and the entity's `RenderKey.LOD` to write `RenderVariantKey`.
3. **Presenter application:** `RenderVariantResolveSystem` toggles the per-entity presenter (Mesh/Sprite/Debug) without structural churn, and `ApplyRenderVariantSystem` writes `MaterialMeshInfo`, `RenderBounds`, and world bounds for mesh presenters.

**Runtime controls:**

- Global reskins: set the `ActiveRenderTheme` singleton `{ ThemeId = … }` and every entity re-resolves automatically.
- Per-entity overrides: add/enable `RenderThemeOverride` to force a specific theme id, or write a `RenderVariantKey` directly for temporary states (construction, ghost previews, etc.).
- Presenter toggles: enable/disable `MeshPresenter`, `SpritePresenter`, or `DebugPresenter` to switch render modes (LOD impostors, debugging, hiding entities) without touching archetypes.
- Instanced overrides: add `RenderTint`, `RenderTexSlice`, or `RenderUvTransform` for per-entity cosmetic variety that keeps instancing intact.

**Spawn defaults:** authoring only needs to set the semantic key, enable `MeshPresenter`, and optionally add overrides (tint/theme). Theme 0 in the catalog maps canon ids (carriers, miners, asteroids, etc.) to loud placeholder meshes so renders are deterministic even before art arrives.

---

## Key Differences from Godgame

### No 3D Presentation

**Space4X entities have NO 3D representation:**
- No GameObjects
- No meshes
- No animations
- No visual effects

**Instead:**
- UI-only representation (cards, panels, tooltips)
- Ship-based representation (pops assigned to ships)
- Child vessel representation (commanding officers via their vessels)

### Ship Assignment System

**Individual pops are represented via ships:**
- Pop assigned to ship → Pop visible via ship
- Pop is captain → Pop visible via ship + child vessels
- Pop is crew → Pop visible in ship crew roster UI

**Aggregate entities are represented via ship collections:**
- Planet → Orbital stations
- Fleet → Ship formation
- Sector → Sector-wide stations

---

## Implementation Checklist

### For Individual Pops

- [x] PureDOTS components: `VillagerAlignment`, `VillagerBehavior`, `VillagerInitiativeState`
- [ ] Space4X UI: `Space4XPopUI` component (pop card, tooltip)
- [ ] Space4X ship assignment: `Space4XShipAssignment` component
- [ ] Space4X crew role: `Space4XCrewRole` component
- [ ] Space4X UI systems: Pop card rendering, tooltip display

### For Aggregate Planets

- [x] PureDOTS components: `VillagerAlignment`, `VillagerBehavior`, `VillagerInitiativeState` (aggregated)
- [ ] Space4X UI: `Space4XPlanetUI` component (planet panel, pop list)
- [ ] Space4X stations: `Space4XPlanetStations` component (orbital platforms)
- [ ] Space4X UI systems: Planet panel rendering, culture display

### For Aggregate Fleets

- [x] PureDOTS components: Fleet + `VillagerAlignment`, `VillagerBehavior` (aggregated)
- [ ] Space4X UI: `Space4XFleetUI` component (fleet panel, ship list)
- [ ] Space4X formation: `Space4XFleetFormation` component (ship formation)
- [ ] Space4X UI systems: Fleet panel rendering, formation display

### For Aggregate Sectors

- [x] PureDOTS components: Sector + `VillagerAlignment`, `VillagerBehavior` (aggregated)
- [ ] Space4X UI: `Space4XSectorUI` component (sector map, system list)
- [ ] Space4X stations: `Space4XSectorStations` component (sector-wide stations)
- [ ] Space4X UI systems: Sector map rendering, governance panel

---

## Next Steps

1. **Create Space4X UI components** for each entity type
2. **Implement ship assignment systems** (pop → ship, captain → child vessels)
3. **Build UI panels** showing PureDOTS data in Space4X context
4. **Wire up ship representation** (pops visible via ships, aggregates via ship collections)

---

## Example: Pop → Ship → UI Flow

```
PureDOTS Pop Entity
  ↓ Has VillagerAlignment, VillagerBehavior
Space4X Ship Assignment System
  ↓ Assigns pop to ship
Ship Entity
  ↓ Has Space4XShipVisuals (3D ship model)
Space4X UI System
  ↓ Reads pop alignment/behavior
Pop Card UI
  ↓ Displays alignment/behavior in tooltip
Player sees pop data via ship + UI
```

---

**Related Documentation:**
- PureDOTS Entity Agnostic Design: `PureDOTS/Docs/Concepts/Entity_Agnostic_Design.md`
- Space4X Registry Bridge: (TBD)

---

**Last Updated:** 2025-01-XX  
**Status:** Implementation Guide - In Progress
