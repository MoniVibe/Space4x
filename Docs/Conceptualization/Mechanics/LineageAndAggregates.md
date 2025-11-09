# Mechanic: Lineages, Dynasties & Aggregate Entities

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: Social / Political

**One-line description**: Individual captains, agents, and specialists develop persistent expertise while dynasties, families, guilds, and fleets act as aggregate entities that inherit loyalties, assets, and diplomatic weight.

## Core Concept

Space4X treats people as more than anonymous stats. Every notable entity (ace captains, intelligence officers, engineers) accrues personal history, expertise, and relationships. Simultaneously, aggregate groups—dynasties, elite families, guild houses, mercenary bands, expeditionary fleets—operate as composite actors that pool resources, contract services, and exert influence on empires.

### Individual Entities
- **Roles**: Carrier captains, spy handlers, magistrates, fleet tacticians, science leads.  
- **Progression**: Borrowing from Godgame’s individual guidance model, players can “preordain” a captain toward doctrines (e.g., logistics savant, battle-hardened warlord). Experience gained in missions pushes them along the chosen path.  
- **Expertise**: Each individual tracks expertise vectors (CarrierCommand, Espionage, Logistics, Psionic, Beastmastery for fauna wranglers). Expertise grants bonuses (faster command resolution, safer infiltration, better anomaly handling).  
- **Lesson Quality**: High-expertise individuals can train juniors, improving crew quality or reducing doctrine drift when transferred.

### Aggregate Entities
- **Families & Dynasties**: Lineages own capital ships, stations, and political capital. They negotiate privileges (tax exemptions, private shipyards) and can defect if mistreated. Dynasties maintain heir pools; when a notable captain dies, heirs inherit part of their expertise or grudges.  
- **Guilds & Corporations**: House specialists (engineers guild, spy cabals) that can be contracted. Their aggregate stats determine mission success odds.  
- **Armies & Bands**: Expeditionary task forces, militia coalitions, pilgrim convoys; their movement speed equals the average of member entities and they have pooled upkeep rules.  
- **Patronage Webs**: Individuals belong to one or more aggregates. Orders can be issued via the aggregate (e.g., “House Aristeia mobilizes all carriers it owns”). Loyalty checks consider both individual disposition and aggregate reputation.

## Gameplay Flows

### Recruitment & Contracts
1. **Identify Need**: Player requires a stealth band, carrier captain, or logistics dynasty.  
2. **Assess Lineage**: Review dynasty dossier showing doctrine, assets, suspicion, current contracts.  
3. **Offer Contract**: Pay credits, grant political concessions, or promise territory.  
4. **Deploy**: Aggregate dispatches members; individuals within inherit contract traits (e.g., must obey trade embargo clauses).

### Succession & Legacy
- Notable individuals have heirs or proteges. When a captain retires or dies, their lineage appoints a successor who inherits partial expertise (e.g., 50% of Navigation expertise).  
- Dynasties track **Prestige** and **Stress**. High stress (losses, unpaid fees) can spark schisms or coups; prestige unlocks unique ships or tech advantages.

### Aggregate Economy
- Dynasties own shares in stations or trade routes; profits feed into their loyalty level.  
- Guilds invest in R&D; contracting them grants access to exclusive modules.  
- Armies require logistics support proportional to their average speed and upkeep. Peacekeepers funded by the empire pull from central budgets.

## System Interactions

| System | Interaction |
|--------|-------------|
| **Espionage Framework** | Spies embed within dynasties or pose as heirs; suspicion travels along lineage trees. |
| **Alignment & Law** | Dynasties carry alignment signatures; violating doctrines (e.g., conscripting pacifist houses) triggers compliance penalties. |
| **Economy & Corruption** | Families skim from routes they manage; anti-corruption drives can seize assets, lowering loyalty. |
| **Fleet Composition** | Aggregates owning fleets can refuse suicidal orders; individual captains with high Courage may obey anyway, creating tension. |
| **Event System** | Succession crises, coups, inheritance disputes generate branching events affecting diplomacy and access to ships. |

## Data Model Sketch

```
IndividualEntity {
    EntityId
    Name, LineageId
    Role (Captain, SpyMaster, Magistrate, Scientist)
    Expertise[] (type, tier)
    FocusProfile (Aggressive, Logistics, Espionage)
    LoyaltyScores (Empire, Lineage, Guild)
    Traits (Bold, Coward, Zealot)
}

AggregateEntity {
    AggregateId
    Type (Dynasty, Guild, Army, Band, Corporation)
    Members[] (Individuals / Sub-aggregates)
    Assets (Ships, Stations, Contracts)
    Reputation / Prestige
    AlignmentSignature
}
```

## Balance & Design Considerations

1. **Agency vs Autonomy**: Players set directives for aggregates, but individuals still weigh loyalty and focus allocation before executing orders.  
2. **Scarcity of Talent**: Top-tier dynasties should be rare; losing their trust hurts.  
3. **Costs & Benefits**: Hiring elite families is expensive but grants access to legendary ships or doctrines.  
4. **Defection Risk**: When loyalty drops, individuals can defect, taking assets and intel. Need counterintelligence hooks.  
5. **Cross-Game Consistency**: Keep the data flow compatible with PureDOTS component contracts (e.g., `LineageId`, `AggregateMembershipBuffer`).

## Implementation Notes

- Reuse PureDOTS concepts for expertise, focus, and suspicion. Add `LineageComponent`, `AggregateMembership`, and `PrestigeScore`.  
- Author ScriptableObject dossiers for key dynasties, linking to starting fleets and political goals.  
- Provide UI overlays highlighting aggregate ownership of ships/stations and lineage portraits for notable captains.  
- Extend event templates: “Dynasty Succession Vote,” “Guild Strike,” “Band Mutiny.”

## Next Steps

1. Map existing Space4X characters (captains, spy bands) to the new individual model; identify missing expertise tracks.  
2. Define a starter set of dynasties/guilds tied to campaign modes.  
3. Integrate loyalty and prestige into alignment/compliance systems.  
4. Prototype UI for lineage trees and aggregate contract negotiations.

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
