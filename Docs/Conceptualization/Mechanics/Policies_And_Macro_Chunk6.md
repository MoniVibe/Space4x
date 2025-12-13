# Policies & Macro – Chunk 6

> **Note:** This is a reference copy. The canonical source is [`Godgame/Docs/Concepts/Economy/Policies_And_Macro_Chunk6.md`](../../../../Godgame/Docs/Concepts/Economy/Policies_And_Macro_Chunk6.md).  
> **When updating:** Update the Godgame version first, then sync this copy.

**Type:** Design Specification  
**Status:** Design – Foundation  
**Version:** Chunk 6  
**Depends on:** Chunk 0 ([Economy Spine & Principles](Economy_Spine_And_Principles.md)), Chunk 1 ([Wealth & Ledgers](Wealth_And_Ledgers_Chunk1.md)), Chunk 2 ([Resources & Mass](Resources_And_Mass_Chunk2.md)), Chunk 3 ([Businesses & Production](Businesses_And_Production_Chunk3.md)), Chunk 4 ([Trade & Logistics](Trade_And_Logistics_Chunk4.md)), Chunk 5 ([Markets & Prices](Markets_And_Prices_Chunk5.md))  
**Feeds into:** Social / political simulation (colonies, guilds, elites, courts), Strategic AI (empires, guilds, megacorps), Event systems (strikes, riots, coups, sanctions, wars)  
**Last Updated:** 2025-01-27

---

> **Full content:** See the canonical source document at [`Godgame/Docs/Concepts/Economy/Policies_And_Macro_Chunk6.md`](../../../../Godgame/Docs/Concepts/Economy/Policies_And_Macro_Chunk6.md) for complete specification, policy data models, tax/tariff/debt flows, enforcement/smuggling mechanics, unrest hooks, and the "done" checklist.

---

## Space4X-Specific Notes

While the core policy patterns apply to both projects, Space4X has some specific considerations:

**Fiscal Policy:**
- **Colony Taxes**: Income tax, business tax, trade taxes
- **Fleet Taxes**: Maintenance taxes on carrier fleets
- **Resource Extraction Taxes**: Taxes on mining operations
- **Budget Categories**: Fleet maintenance, research, infrastructure, defense, welfare

**Trade Policy:**
- **Space Tariffs**: Tariffs on inter-colony trade routes
- **Embargoes**: Block trade with specific colonies/factions
- **Sanctions**: Economic warfare between factions
- **Quotas**: Limit imports/exports of critical resources (fuel, ores, components)

**Debt & Credit:**
- **Inter-Colony Loans**: Loans between colonies
- **Fleet Financing**: Loans for carrier construction
- **Default Consequences**: Asset seizure (mines, stations), tribute, vassalization
- **Debt Vassalization**: Colonies become vassals of creditor colonies/factions

**Enforcement:**
- **Patrol Fleets**: Enforcement strength based on patrol fleet presence
- **Inspection Stations**: Trade inspection at jump gates and stations
- **Corruption**: Bribery of inspectors and patrol commanders
- **Smuggling**: High-risk trade routes bypassing tariffs/embargoes

**Unrest:**
- **Colony Unrest**: Protests, strikes, riots triggered by economic stress
- **Fleet Mutinies**: Mutinies in underfunded fleets
- **Revolts**: Colonies revolting against oppressive policies
- **Coups**: Elite coups against colonial governments

**Integration:**
- Production (Chunk 3) creates goods in colony inventories
- Trade (Chunk 4) moves goods between colonies via carriers
- Markets (Chunk 5) price goods based on supply/demand
- Policies (Chunk 6) apply taxes, tariffs, embargoes, sanctions

---

**Last Updated:** 2025-01-27  
**Maintainer:** Economy Architecture Team  
**Status:** Design – Foundation. Implementation work should follow this specification.



















