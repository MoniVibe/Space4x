# Markets & Prices – Chunk 5

> **Note:** This is a reference copy. The canonical source is [`Godgame/Docs/Concepts/Economy/Markets_And_Prices_Chunk5.md`](../../../../Godgame/Docs/Concepts/Economy/Markets_And_Prices_Chunk5.md).  
> **When updating:** Update the Godgame version first, then sync this copy.

**Type:** Design Specification  
**Status:** Design – Foundation  
**Version:** Chunk 5  
**Depends on:** Chunk 0 ([Economy Spine & Principles](Economy_Spine_And_Principles.md)), Chunk 1 ([Wealth & Ledgers](Wealth_And_Ledgers_Chunk1.md)), Chunk 2 ([Resources & Mass](Resources_And_Mass_Chunk2.md)), Chunk 3 ([Businesses & Production](Businesses_And_Production_Chunk3.md)), Chunk 4 ([Trade & Logistics](Trade_And_Logistics_Chunk4.md))  
**Feeds into:** Chunk 6 (Policies & Macro), Strategic AI (village/guild/fleet economic decisions), UI (price displays, economic heatmaps)  
**Last Updated:** 2025-01-27

---

> **Full content:** See the canonical source document at [`Godgame/Docs/Concepts/Economy/Markets_And_Prices_Chunk5.md`](../../../../Godgame/Docs/Concepts/Economy/Markets_And_Prices_Chunk5.md) for complete specification, market representation, pricing formulas, supply/demand measurement, market interaction, and the "done" checklist.

---

## Space4X-Specific Notes

While the core market and pricing patterns apply to both projects, Space4X has some specific considerations:

**Market Nodes:**
- **Colonies**: Stationary markets with local production and consumption
- **Space Stations**: Trade hub markets with high throughput
- **Carriers**: Mobile markets (can act as trading posts in deep space)

**Good Types:**
- Space4X items: Ores, alloys, fuel, components, starship modules, artifacts
- Different base prices reflect space-age economic scale
- Fuel prices critical for space travel economics

**Event Multipliers:**
- **War**: Fleet movements, blockades affect prices
- **Piracy**: Increased risk affects transport costs and prices
- **Resource Depletion**: Asteroid fields exhausted, prices spike
- **Tech Breakthrough**: New production methods lower prices

**Arbitrage:**
- Price differences between colonies drive trade routes
- Fuel costs factor into arbitrage calculations
- Jump gate access affects trade opportunities

**Integration:**
- Production (Chunk 3) creates goods in colony inventories
- Trade (Chunk 4) moves goods between colonies
- Markets (Chunk 5) price goods based on supply/demand
- Policies (Chunk 6) will apply tariffs, embargoes, sanctions

---

**Last Updated:** 2025-01-27  
**Maintainer:** Economy Architecture Team  
**Status:** Design – Foundation. Implementation work should follow this specification.


















