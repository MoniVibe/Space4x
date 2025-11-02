# Mechanic: Resource Chains

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Economy

**One-line description**: A concise set of resource transformations keeps production interesting without overloading players with materials.

## Core Concept

The economy relies on a handful of intuitive resource families that flow from mining to processing to construction. We emphasise depth through combinations rather than volume of unique items, enabling players to understand supply lines quickly while still making meaningful tradeoffs.

## Foundational Taxonomy

| Family | Raw Input | Refined Output | Advanced Composite | Notes |
|--------|-----------|----------------|--------------------|-------|
| Metals | Iron Ore | Iron Ingots | Steel (Iron + Carbon) | Baseline structural material for most builds. |
| Advanced Metals | Titanium Ore | Titanium Ingots | Plasteel (Steel + Polymers) | Used for hulls, armour, high-stress components. |
| Organics | Biomass | Nutrients | Biopolymers (Nutrients + Petrochemicals) | Supports life support, biotech modules. |
| Petrochemicals | Hydrocarbon Ice | Refined Fuels | Polymers (Refined Fuels + Catalysts) | Drives propulsion, forms composites. |
| Electronics | Rare Earths | Conductors | Quantum Cores (Conductors + Silicates) | Powers advanced systems, sensors, AI cores. |

### Combination Conventions

- **Steel** = Iron Ingots + Carbon (refined from biomass or hydrocarbon sources).
- **Plasteel** = Steel + Polymers.
- **Composite Alloys** = Plasteel + Quantum Cores (for late-game hulls and reactors).
- **Circuit Mesh** = Conductors + Polymers (baseline electronics output).
- **Life Support Kits** = Nutrients + Polymers (colony growth accelerators).
- **Culinary Spectrum** = Nutrient pastes up to gourmet/nano-gastronomy tiers (common → epic → legendary).
  - **Deferred Feature**: Quality affects morale, population growth, and trade value. Abstracted until core loops stabilize.

These placeholders set expectations for future expansion without committing to exhaustive chains. Each combination should appear in no more than two tiers to keep logistics manageable.

## Processing Principles

- **Facilities**: Different recipes and efficiencies per facility type (refinery, fabricator, foundry, etc.).
- **Mobile Carriers**: Operate identically to stations when processing; no efficiency penalty for being mobile.
- **Role Switching**: Carriers can switch processing modes via module swapping (physical slots with refit time based on tech level and crew experience).
- **Recipes**: Data-driven, modder-friendly format (ScriptableObjects or JSON) allowing custom resource chains.
- **Conversion Ratios**: Vary by facility tier and crew skill (2:1 base, improved by upgrades and experience).
- **Tech Diffusion**: Upgrades take time to reach all carriers; facilities gradually switch to better methods over time, often requiring facility upgrades to adopt new recipes.

### Technical Notes
- **PureDOTS Integration**: Use blob assets for recipe catalogs; DOTS buffers for per-facility active recipes.
- **Module System**: Requires component archetype changes during refit (ECB-based with transition states).
- **Crew Skill**: Leverage existing crew/morale systems; apply skill multipliers in Burst jobs for throughput calculations.

## Integration Touchpoints

- **Mining Loop** supplies raw families; exploration identifies rare sources for advanced composites.
- **Construction Loop** consumes refined and composite outputs, with project blueprints specifying tier requirements.
- **Haul Loop** moves intermediate goods between processing nodes and build sites.

## Tuning Guardrails

- Keep total unique resource SKUs under ~12 for base game to avoid bloat.
- Focus complexity on production choices (which composites to prioritise) and logistics rather than memorising recipes.
- Introduce tech upgrades that consolidate steps (e.g., direct Plasteel fabrication) as late-game pacing levers.

## Carrier Modularity

- **Role Switching**: Carriers can switch between processing, mining, hauling, and combat roles via module swapping.
- **Physical Slots**: Modules occupy physical slots with refit time based on tech level and crew experience.
- **Stat Impact**: Modules affect carrier stats (mining module reduces cargo space, armor reduces speed, etc.).
- **Refit Locations**: Carriers can swap modules only if relevant facilities allow it (e.g., shipyard facility on another carrier, mobile repair depot). Otherwise, carriers must dock at stations or colony orbitals for refit.
- **Field Refit Tech**: Higher tech levels enable more capable mobile facilities, allowing complex refits in the field.

### Technical Notes
- **Module System Requirements**: See `Docs/TODO/4xdotsrequest.md` for PureDOTS module framework needs.
- **Module Entities**: Each module is a separate entity with its own health, enabling per-instance damage tracking.
- **Repair Prioritization**: Players can choose which modules to repair first; repair systems process repair queue by priority.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-11-02 | Added carrier modularity, tech diffusion, culinary deferred | Captured design answers |
| 2025-10-31 | Initial draft | Established low-bloat resource framework |

---

*Last Updated: November 2, 2025*  
*Document Owner: Design Team*
