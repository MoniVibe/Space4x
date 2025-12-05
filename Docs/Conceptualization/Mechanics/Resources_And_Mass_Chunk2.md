# Resources & Mass – Chunk 2

> **Note:** This is a reference copy. The canonical source is [`Godgame/Docs/Concepts/Economy/Resources_And_Mass_Chunk2.md`](../../../../Godgame/Docs/Concepts/Economy/Resources_And_Mass_Chunk2.md).  
> **When updating:** Update the Godgame version first, then sync this copy.

**Type:** Design Specification  
**Status:** Design – Foundation  
**Version:** Chunk 2  
**Depends on:** Chunk 0 ([Economy Spine & Principles](Economy_Spine_And_Principles.md)), Chunk 1 ([Wealth & Ledgers](Wealth_And_Ledgers_Chunk1.md))  
**Feeds into:** Chunk 3 (Businesses & Production), Chunk 4 (Trade & Logistics), Chunk 5 (Markets & Prices), Environment/Climate/Vegetation, Combat/Equipment/Courts  
**Last Updated:** 2025-01-27

---

> **Full content:** See the canonical source document at [`Godgame/Docs/Concepts/Economy/Resources_And_Mass_Chunk2.md`](../../../../Godgame/Docs/Concepts/Economy/Resources_And_Mass_Chunk2.md) for complete specification, ItemSpec catalog structure, inventory model, capacity rules, mass calculations, basic flows, and the "done" checklist.

---

## Space4X-Specific Notes

While the core resource and inventory patterns apply to both projects, Space4X has some specific considerations:

**ItemSpec Catalog:**
- Space4X items: Ores, alloys, fuel, components, starship modules, artifacts
- Mass calculations critical for fuel consumption and movement
- Volume constraints important for cargo holds and ship design

**Inventory Containers:**
- **Carriers**: Large mobile inventories with fuel constraints
- **Colonies**: Stationary inventories with specialized storage (ore processing, fuel storage)
- **Ships**: Smaller mobile inventories with strict capacity limits
- **Cargo Holds**: Specialized containers for bulk transport

**Mass & Movement:**
- Mass affects fuel consumption (critical for space travel)
- Gravity variations across planets/moons affect effective weight
- Volume constraints for cargo holds and ship design
- Environmental factors (atmosphere, re-entry) affect transport

**Gravity/Environment:**
- Chunk 2 stores mass only
- Higher layers (Chunk 4: Logistics) combine mass × local gravity for effective weight
- Planetary atmosphere affects re-entry constraints and cargo handling

---

**Last Updated:** 2025-01-27  
**Maintainer:** Economy Architecture Team  
**Status:** Design – Foundation. Implementation work should follow this specification.





