# Entity Progression & Opportunities

**Vision**
Enable entities to rise from "nobody" to galactic stellarch through a steady stream of opportunities: jobs, missions, discoveries, and special POIs. Progress is earned through actions, risk, reputation, and strategic alignment.

---

## 1) Progression Ladder (Narrative + Mechanical)

**Civilians & Crew**
- **Entry**: deckhands, technicians, couriers.
- **Growth**: promotions through competency, loyalty, and risk.
- **Power**: becomes chief officers, fleet captains, or faction lieutenants.

**Operators & Captains**
- **Entry**: small patrols, mining crews, escorts.
- **Growth**: mission success, operational reputation, fleet size.
- **Power**: command carriers, lead task forces, influence doctrine.

**Factions & Empires**
- **Entry**: small stations, mining colonies, pirate bands.
- **Growth**: territory, alliances, economic throughput, research.
- **Power**: empire leadership, stellarch dominance, doctrinal control.

---

## 2) Opportunity Types (What Entities Can Do)

### A) Jobs (routine)
- Mining contracts
- Escort convoys
- Patrol routes
- Supply runs
- Salvage operations
- Logistics optimization
- Survey routes
- Repair & refit work orders
- Smuggling / black-market runs (faction dependent)
- Training drills / readiness cycles

### B) Missions (directed)
- Secure a POI / system
- Establish a station / outpost
- Recover a relic / artifact
- Rescue / extraction
- Diplomatic envoy
- Raid or strike mission
- Counter‑piracy sweep
- Intel capture / signal intercept
- Strategic blockade or escort corridor
- Covert ops (low visibility, high reward)

### C) Events & Fortune
- Lucky treasure finds
- Ancient cache / derelict discovery
- Experimental tech unlock
- Crisis response (“answer the call”)

---

## 3) POI & Special Finds

**Common POIs**
- Derelicts
- Asteroid vaults
- Rogue science labs
- Ruined megastructures

**Rare POIs**
- Stellarch relics
- Progenitor ruins
- Quantum anomalies
- Lost fleets

**POI Outcomes**
- Loot (credits, resources, modules)
- Reputation (faction trust, influence)
- Knowledge (research unlocks)
- Threat (ambushes, curses, guardians)

---

## 4) Progression Rewards

- **Assets**: ships, stations, fleets
- **Influence**: reputation, political leverage
- **Knowledge**: research unlocks, tech tiers
- **Authority**: command scope, directive power
- **Legacy**: lineage, history, cultural footprint

---

## 5) Systems Needed (Pipeline)

- **Opportunity Generator**: emits jobs/missions based on world state.
- **Eligibility + Filtering**: only relevant entities see opportunities.
- **Acceptance → Execution**: jobs become orders, then tasks.
- **Outcome + Credit**: apply rewards, reputation, and story hooks.

---

## 6) Agent‑Facing Control Hooks

LLM agents should be able to:
- Query available opportunities by scope and risk.
- Issue acceptance or decline decisions.
- Prioritize categories (wealth, influence, research).
- Allocate assets to missions (ships, crews, time).

---

## 7) Roadmap (Mutable)

- **Short‑term**: define job/mission schema for a single faction.
- **Mid‑term**: add POI discovery/loot pipeline with risk outcomes.
- **Long‑term**: rank progression ladder and stellarch ascension arc.

---

## 8) Job & Mission Schema (Draft)

**Opportunity**
```
id
type (Job | Mission | Event)
category (mining, escort, patrol, salvage, research, diplomacy, combat)
issuer (faction / station / NPC)
scope (sector / system / route)
risk (0–1)
reward (credits, reputation, tech, assets)
constraints (time, budget, fleet size, faction alignment)
expiresAtTick
```

**Accepted Opportunity**
```
opportunityId
assignee (fleet / captain / station)
issuedTick
plan (ordered task list)
status (active / completed / failed / aborted)
outcome (reason + metrics)
```

---

## 9) ECS Sketch (Where It Can Live)

- **Opportunity feed**: `Space4XMissionBoardComponents` (offers + filtering).
- **POI catalog**: `Space4XGalaxyContentComponents` (poi traits + rarity).
- **Order execution**: `Space4XFactionGoal` -> captain/fleet orders (mission queue).
- **Outcome pipeline**: new buffer for `OpportunityOutcome` (reward + reason).

---

## 10) Stellarch Ascension Arc (Milestones)

1. **Local Hero**: completes 3–5 jobs with positive outcomes.
2. **Sector Actor**: manages small fleet; earns first POI discovery.
3. **Regional Power**: controls a trade route or production hub.
4. **Faction Leader**: directs doctrine + territory policy.
5. **Stellarch**: unifies multiple regions; controls major POI relics.
6. **Legacy**: establishes institutions and doctrines that persist post‑rewind.
