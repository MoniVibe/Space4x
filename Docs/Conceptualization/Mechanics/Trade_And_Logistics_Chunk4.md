# Trade & Logistics – Chunk 4

> **Note:** This is a reference copy. The canonical source is [`Godgame/Docs/Concepts/Economy/Trade_And_Logistics_Chunk4.md`](../../../../Godgame/Docs/Concepts/Economy/Trade_And_Logistics_Chunk4.md).  
> **When updating:** Update the Godgame version first, then sync this copy.

**Type:** Design Specification  
**Status:** Design – Foundation  
**Version:** Chunk 4  
**Depends on:** Chunk 0 ([Economy Spine & Principles](Economy_Spine_And_Principles.md)), Chunk 1 ([Wealth & Ledgers](Wealth_And_Ledgers_Chunk1.md)), Chunk 2 ([Resources & Mass](Resources_And_Mass_Chunk2.md)), Chunk 3 ([Businesses & Production](Businesses_And_Production_Chunk3.md))  
**Feeds into:** Chunk 5 (Markets & Prices), Chunk 6 (Policies & Macro), Strategic AI (village/guild/fleet economic behavior)  
**Last Updated:** 2025-01-27

---

> **Full content:** See the canonical source document at [`Godgame/Docs/Concepts/Economy/Trade_And_Logistics_Chunk4.md`](../../../../Godgame/Docs/Concepts/Economy/Trade_And_Logistics_Chunk4.md) for complete specification, route templates, transport entities, core flows, integration patterns, and the "done" checklist.

---

## Space4X-Specific Notes

While the core trade and logistics patterns apply to both projects, Space4X has some specific considerations:

**Trade Nodes:**
- **Colonies**: Stationary trade nodes
- **Space Stations**: Orbital trade hubs
- **Outposts**: Small resource extraction sites
- **Carriers**: Mobile trade nodes (can act as trading posts)

**Transport Entities:**
- **Carriers**: Large mobile inventories with fuel constraints
- **Freighters**: Dedicated cargo ships
- **Courier Ships**: Fast, low-capacity transports
- **Logistics Fleets**: Task forces with combined cargo capacity

**Route Considerations:**
- **Fuel Requirements**: Space travel requires fuel (not just operating costs)
- **Gravity Wells**: Planetary gravity affects departure/arrival costs
- **Jump Gates**: FTL routes with different mechanics than sublight routes
- **Piracy**: Space pirates instead of bandits
- **Storms**: Solar storms, asteroid fields instead of weather

**Integration:**
- Production (Chunk 3) creates goods in colony inventories
- Trade (Chunk 4) moves goods between colonies via carriers/ships
- Markets (Chunk 5) will price goods based on supply/demand
- Policies (Chunk 6) will apply tariffs, embargoes, sanctions

---

**Last Updated:** 2025-01-27  
**Maintainer:** Economy Architecture Team  
**Status:** Design – Foundation. Implementation work should follow this specification.
















