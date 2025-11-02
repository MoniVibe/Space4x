# Mechanic: Entity Ownership & Hierarchy

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Social / Governance

**One-line description**: Individuals and organizations own assets, form layered allegiances, and can splinter into new factions or empires based on doctrine and opportunity.

## Core Concept

Every sentient entity may own property (ships, stations, colonies) and align with multiple organizations simultaneously. Ownership and allegiance drive access to resources, command authority, and morale obligations. Over time, ambitious leaders can assert independence, creating factions or full empires that inherit and contest assets.

## Ownership Layers

| Layer | Examples | Notes |
|-------|----------|-------|
| Individual | Captains, engineers, colonists | Own personal ships, modules, cargo; carry doctrine alignment traits. |
| Crew/Household | Families, guild memberships | Share pooled resources and loyalties. |
| Fleet/Ship | Carrier groups, mobile colonies | Collectively own hangars, fabrication bays, strike craft. |
| Colony | Planetary or orbital settlements | Control local infrastructure, resource rights, civic policies. |
| Faction | Guilds, corporations, militias | Cross-cut colonies/fleets with specialised goals. |
| Empire | Sovereign political entity | Claims star systems, issues mandates, levies taxes. |

Entities may belong to multiple layers concurrently (e.g., a colonist from Colony A, member of Guild Theta, serving aboard Empire Ship Resolute, under Empire Helios). Conflicts between layers feed into compliance systems defined in `Docs/TODO/Alignment.md`.

## Asset Ownership Rules

- Ownership is represented as a deterministic link between an entity and the asset’s registry entry; assets can have joint ownership via pooled guilds or corporate shares.
- When colonists disembark from mobile fleets, they retain ownership of personal ships and may reassign them to new colonies or factions.
- Empire-level claims cascade: if an empire fractures, subordinate assets inherit allegiances based on their majority stakeholder (e.g., dominant guild or colony).

## Allegiance & Splintering

- Any entity with sufficient assets and followers can declare independence, forming a faction or empire. Doctrine alignment, morale, and compliance thresholds gate this transition.
- Migration events (colonists leaving fleets) create morale shocks and can seed new colonies or sub-factions that compete for resources.
- Empires may host internal factions; loyalty scores determine whether those factions cooperate, secede peacefully, or ignite conflicts.

## Integration Touchpoints

- **Combat Loop**: Asset ownership dictates command rights during engagements; splinter groups may take ships with them.
- **Construction Loop**: Ownership determines who can authorise projects and consume stored resources.
- **Haul Loop**: Trade rights and tariffs hinge on faction/guild relationships.
- **Alignment Systems**: Doctrine compliance checks evaluate whether multi-layer allegiances remain stable.

## Data Representation Considerations

- Use dynamic buffers to list all allegiances per entity (colony, faction, empire, guild, company, fleet, ship). Each entry stores role, loyalty strength, and ownership share.
- Assets maintain back-references to their owners and hosting organizations for deterministic retrieval.
- Serialized starting states should expose ownership tables so players can script bespoke empires or free-agent fleets.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Defined ownership and allegiance framework |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
