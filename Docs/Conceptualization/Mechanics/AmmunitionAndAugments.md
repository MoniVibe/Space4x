# Mechanic: Ammunition & Augmentation Framework

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Combat / Progression

## Ammunition Model

- **Archetype**: Ammunition acts as a modifier for kinetic/explosive weapons; weapons still consume their native ordnance per fire cycle.
- **Attributes**:
  - **Tier** – Drives baseline penetration, blast radius, and reliability.
  - **Quality** – Fine control over spread/dispersion, misfire risk, maintenance load.
  - **Rarity** – Availability, black-market value, diplomatic leverage.
  - **Manufacturer** – Signature traits (e.g., armor shred, EMP payload, smart shrapnel) that scale with manufacturer experience.
- **Variants**: Slugs, rail penetrators, flak clusters, breaching charges, siege torpedoes; each variant defines compatible mount classes and permitted weapon families.
- **Production**: Facilities craft ammo batches using resource chains; legendary runs require manufacturer XP milestones plus officer endorsements.
- **Logistics**: Ammo inventories consume ordnance storage; per-shot consumption references both weapon and ammo tier to compute supply drain.

## Augmentation Schema (Cross-Species)

- **Goal**: Unified data layer for limb/organ augmentations across species, respecting anatomy differences while sharing core simulation.
- **Component Layout (Draft)**:
  - `SentientAnatomy` (blob) – Species-defined limb/organ slots, health multipliers, augmentation compatibility tags.
  - `AugmentationInventory` (dynamic buffer) – Installed augment references `{SlotId, AugmentId, Quality, Tier, ManufacturerId, StatusFlags}`.
  - `AugmentationStats` (component) – Aggregated modifiers (Physique, Finesse, Will, General), upkeep costs, risk factors.
  - `AugmentationExperience` (component) – Tracks usage XP per augment archetype, unlocking trait evolutions.
  - `AugmentationContracts` (component) – Installer provenance, warranty duration, legal status (licensed, rogue, black-market).
- **Blob Assets**:
  - `AugmentDefinition` – Base stats, slot requirements, trait tree, failure thresholds.
  - `SpeciesAugmentProfile` – Cross-reference table linking species anatomy to compatible augment families and required adapters.
- **Installers**:
  - **Docs** – Licensed medtechs; use `AugmentationContracts` to record compliance and warranty perks.
  - **Rippers** – Illicit surgeons; push risk flags, can bypass slot compatibility with penalties.
- **Progression Hooks**:
  - Augments feed Physique/Finesse/Will XP modifiers, unlock skill/passive nodes, and gate certain titles or mission roles.
  - Aggregated crews treat individual augments as flat modifiers; personal installations still matter for officer-level abilities or interfacing with advanced neural cockpits.
  - Manufacturer experience modifies augment trait evolution similar to facility products.

## Integration To-Do

1. Define ammo resource entries and production recipes in `ResourceChains` doc.
2. Specify augmentation installers and downtime events in crew progression systems.
3. Align with manufacturer experience model (Facility Archetypes doc) for legendary ammo/augment runs.
4. Draft UI mock (roster panel) showing ammo loadouts and installed augment list per officer.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-11-05 | Initial draft | Documented ammo quality/rarity/tier/manufacturer model and cross-species augmentation schema |

---

*Last Updated: November 5, 2025*  
*Document Owner: Design Team*
