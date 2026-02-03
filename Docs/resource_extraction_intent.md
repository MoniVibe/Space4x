Space4X Resource Extraction Intent (MVP → Presentation)
=======================================================

Summary
-------
We currently run an MVP mining loop (numeric bite + direct transfer). This document
captures the intended long‑term behavior so the presentation slice can expand without
losing the design goals.

Current MVP (implemented)
-------------------------
- Mining is numeric: miners decrement asteroid resource amounts and accumulate cargo.
- Delivery is direct: on proximity, cargo transfers to carrier ResourceStorage.
- No mesh morphing, no embedded resource voxels, no floating pickups, no hauler loop.

Target Intent (future presentation slice)
-----------------------------------------
1) Mesh‑embedded deposits
   - The simulation must not depend on render meshes.
   - Use a deterministic, baked data representation (Blob) for deposits.
   - Runtime sim updates only the deposit state (remaining mass / depletion stage).

2) Bite mechanics
   - Miners apply discrete "bite" volumes to the deposit.
   - Bites are deterministic and ordered (tick + stable miner id).
   - A bite reduces local deposit mass and produces output.

3) Morphing / deformation (presentation‑only)
   - Visuals read the deposit state and/or bite volumes.
   - MVP morph option: stage swap (depletion stage → mesh variant).
   - Advanced options: bite spheres (SDF) or voxel grid + mesh rebuild.
   - Headless runs should skip all mesh deformation systems.

4) Pickup / hauler pipeline
   - Mining output spawns ResourcePickup entities in space.
   - Haulers acquire pickups, collect into inventory, and deliver to storage.
   - Deterministic assignment and transfer order (stable id ordering).

5) Refinement + quality
   - Raw resources refine into higher‑quality outputs.
   - Purity/quality flows into module/ship quality aggregates.
   - MVP formula should be integer/fixed‑point to preserve determinism.

Implementation notes
--------------------
- Keep sim deterministic: avoid unordered container iteration; sort by stable id.
- Prefer Blob assets for read‑only deposit templates.
- Use enableable components / change filtering to reduce churn.
- Presentation systems should be optional and non‑authoritative.
