# Best Practices for Rendering and Presenting ECS Entities (Unity DOTS)

Recorded: February 1, 2026

Purpose and Context

You are building a data-driven simulation using Unity DOTS. The simulation generates many
entities (villagers, resources, fleets, asteroids, etc.) but you do not yet have final art
assets. You need a way to render and present entities clearly and efficiently with placeholder
meshes without disrupting determinism or simulation performance.

This guide synthesizes official Unity Entities Graphics documentation and internal architecture
docs from the PureDOTS, Space4X and Godgame projects. It explains why a lightweight, data-driven
rendering layer is essential, how to structure the presentation systems, how the Render Catalog
mechanism binds semantic keys to meshes/materials, and how to pick and manage placeholder shapes
and colors. The recommendations are grounded in the simulation -> bridge -> presentation pattern
used in both games.

1) Entities Graphics and URP Requirements

URP is mandatory

Unity's Entities Graphics package relies on a Scriptable Render Pipeline. The internal URP summary
emphasizes that URP is a hard dependency - without it the EntitiesGraphicsSystem disables itself
and nothing renders. Projects must assign the URP pipeline both in Graphics Settings and Quality
Settings, and they provide an automated fix tool in case assets are missing. Use URP (or HDRP) for
your scenes; the built-in pipeline is not supported.

Required components for a renderable entity

Entities Graphics does not automatically render every entity. An entity must have specific
components:

- MaterialMeshInfo - stores indices into a shared RenderMeshArray for mesh/material selection.
- RenderBounds and WorldRenderBounds - bounding boxes used for culling.
- RenderFilterSettings - layer, shadows, and render flags.
- A shared RenderMeshArray - an array of meshes and materials that all entities in the subscene
  share.

These components are normally added automatically during the baking phase when a GameObject with
a MeshRenderer/MeshFilter is placed in a SubScene, or when your Render Catalog baker runs. Never
manually add rendering components; instead bake them via SubScenes or use
RenderMeshUtility.AddComponents only when creating prototypes.

Batching and placeholder meshes

Entities Graphics uses BatchRendererGroup and DOTS Instancing to batch instances that share the
same mesh and material into a single draw call. Unity's performance guide shows that 100 cubes
with the same material batch into one DrawInstanced call, whereas multiple materials/meshes reduce
batching efficiency. This means that a small palette of simple meshes (cubes, spheres, capsules)
with a few materials yields excellent performance and can visually differentiate entity classes
via color tints. Shared meshes also minimize shared-component fragmentation and memory overhead.

2) Runtime Entity Creation and RenderMeshArray

RenderMesh vs. RenderMeshArray

In Entities 1.x the RenderMesh struct is used only during baking; it does not exist at runtime.
Runtime rendering is driven by RenderMeshArray - a shared list of meshes and materials. Each
entity selects entries via its MaterialMeshInfo indices. Unity automatically packs all meshes and
materials in a subscene into one RenderMeshArray to minimize chunk fragmentation.

Create entities efficiently

There are two ways to supply rendering components at runtime:

- Use a baked prefab/prototype - bake a GameObject into a SubScene; at runtime call
  EntityManager.Instantiate(prefab) and set data like LocalTransform, MaterialMeshInfo index,
  color override, etc. This is the recommended pattern.
- Use RenderMeshUtility.AddComponents - a static API that adds the required rendering components
  when you dynamically create an entity. However, this API runs on the main thread and is less
  efficient; avoid calling it for thousands of entities.

To spawn many placeholder entities, bake one prototype per mesh/material combination, then clone
it in Burst jobs using EntityCommandBuffer.ParallelWriter.Instantiate. After instantiation, set
the MaterialMeshInfo index, transform, and any custom data. Avoid creating unique shared
components per entity, because unique shared components fragment chunks and slow iteration.

3) Presentation Bridge Architecture - Keeping Sim and Presentation Separate

Both Space4X and Godgame use a Presentation Bridge to decouple deterministic simulation from
volatile visuals. The architecture document divides the world into layers:

Layer | Owner | Responsibility
Simulation (Hot) | PureDOTS Runtime | Stores authoritative state (LocalTransform, gameplay data).
Bridge | PureDOTS Presentation systems | Converts simulation events into spawn/recycle commands.
Visual (Cold) | Game-specific presentation assemblies | Instantiates/updates visuals.
UI/HUD | Game projects | Reads registry snapshots and presentation handles.

Data flow and pooling

Authoring - Designers assign a deterministic presentation key to a simulation prefab
(space4x.carrier or godgame.villager.miner). Bakers write this key into a component or emit spawn
commands.

Simulation event - When gameplay systems spawn or despawn an entity, they enqueue a spawn/recycle
request on a shared command queue. Requests include descriptor hash, variant seed and optional
offsets for companion alignment.

Bridge processing - PresentationSpawnSystem/PresentationRecycleSystem processes these commands,
materializes visuals from the registry blob and attaches a PresentationHandle to the simulation
entity. These systems honor rewind/playback modes so visual churn does not affect determinism.

Visual update - Presentation systems read PresentationHandle and transform data to drive
renderers, VFX, audio, etc. Visual entities can interpolate and attach effects without feeding
back into gameplay. Pooling is central: when recycling is enabled, visual entities are reused
rather than destroyed.

Keeping simulation lean

Simulation (hot) entities remain lean: they contain only gameplay data, LocalTransform, tags and
a PresentationKey or RenderSemanticKey. All render-specific data (meshes, materials, VFX state)
lives on the companion (visual) entity, which is always replaceable. This ensures determinism and
memory efficiency. Never store UnityEngine.Object references or AssetDatabase GUIDs in simulation
state; instead use stable IDs and look up assets through registries.

Registry and authoring

Presentation descriptors live in registry assets (PresentationRegistryAsset). Each descriptor
binds a key to a prefab, default offset/scale/tint and flags. Keys are namespaced with a dot
(space4x.carrier.idle) and must be deterministic (<= 48 characters). Game projects extend the
shared registry by adding descriptors in their own assets. The runtime registry is created via a
baker that produces a PresentationRegistryReference and a PresentationCommandQueue.

Domain adapters

Each gameplay domain should have a presentation adapter system that reads simulation data and
decides when to enqueue spawn or recycle requests. Examples include VillagerPresentationAdapter,
StructurePresentationAdapter, ResourcePresentationAdapter, CrewPresentationAdapter, and
FleetPresentationAdapter. Adapters run in SimulationSystemGroup; visual updates (animator params,
VFX) occur in PresentationSystemGroup. This separation prevents presentation logic from leaking
into simulation and makes visuals plug-and-play.

4) Render Catalog System: Space4X and Godgame

Concept and data flow

Both projects rely on a Render Catalog to map semantic keys to mesh/material variants. A catalog
asset (RenderPresentationCatalogDefinition) contains a list of Variants (mesh, material, bounds,
presenter mask) and one or more Themes that map a SemanticKey to variant indices for LOD 0/1/2.
During baking, the RenderPresentationCatalogBaker converts the definition into a runtime
RenderPresentationCatalog and RenderMeshArray entity. At runtime, ResolveRenderVariantSystem and
ApplyRenderVariantSystem translate an entity's RenderSemanticKey into a MaterialMeshInfo index and
update its RenderBounds.

Adding new entries

Space4X - To add a new visual: open Space4XRenderCatalog_v2.asset; append a variant with
mesh/material/bounds/presenter mask; update Theme 0 to map the SemanticKey to the new variant
indices; ensure gameplay writes RenderSemanticKey using values from Space4XRenderKeys. The catalog
baker bumps the version automatically.

Godgame - The process is similar: open GodgameRenderCatalog.asset, add a variant, update Theme 0
mapping, and ensure gameplay writes the correct RenderSemanticKey using GodgameSemanticKeys. The
authoring doc stresses that the canonical catalog lives in PureDOTS and the Godgame asset is the
single source of truth.

Stable semantic key values

Stable integer constants allow the simulation to specify a visual archetype without referring to
assets. Key values must match the catalog entries:

Space4X keys (excerpt): Carrier = 200, Miner = 210, Asteroid = 220, Projectile = 230,
FleetImpostor = 240, Individual = 250, StrikeCraft = 260, ResourcePickup = 270, GhostTether = 280.

Godgame keys: VillagerMiner = 100, VillagerFarmer = 101, VillagerForester = 102, VillagerBreeder = 103,
VillagerWorshipper = 104, VillagerRefiner = 105, VillagerPeacekeeper = 106, VillagerCombatant = 107,
Villager (alias) = VillagerMiner; VillageCenter = 110; ResourceChunk = 120; Vegetation = 130;
ResourceNode = 140; Storehouse = 150; Housing = 151; Worship = 152; ConstructionGhost = 160;
Band = 170; GhostTether = 280.

Both catalogs support asset upgrade pipelines: import new hero meshes from Assetsvault, convert
materials to URP, append new variants and update theme mappings; never reorder existing entries.
Runtime verification steps ensure that RenderPresentationCatalog.Blob.IsCreated is true, that the
RenderMeshArrayEntity has a RenderMeshArray shared component, and that entities with
RenderSemanticKey resolve to a MaterialMeshInfo.

5) Guidelines for Large-Scale Simulations and Presentation (Processed Insights)

The Processed Insights -> Actionables document outlines additional practices to maintain
performance and determinism in large simulations:

- LOD and culling must not alter simulation state; the presentation layer controls perceived
  scale while the simulation step remains fixed.
- Distance rendering policy: use impostors/LODs for distant villages/biomes; rely on instancing
  and batching for vegetation and props; use shader variation instead of unique meshes to express
  biome diversity. This reinforces the recommendation to use simple placeholder meshes with color
  variation.
- Cache and invalidation: cache region/biome views and invalidate them only when relevant changes
  occur.
- Maintain a hard sim/presentation boundary: never let the camera or UI influence simulation, and
  avoid global recomputations; update expensive systems incrementally.
- Registry and presentation contracts: store stable IDs and small overrides in simulation; never
  embed UnityEngine.Object references; avoid SharedComponentData for skins or profiles to prevent
  chunk fragmentation.
- Rendering and transforms: prefer prefab instantiation/pooling over RenderMeshUtility.AddComponents
  for bulk creation; use Parent/Child plus LocalTransform for hierarchy; only use PostTransformMatrix
  for non-uniform scaling.

These constraints ensure that your simulation remains deterministic and scalable even as you
visualize thousands or millions of entities.

6) Placeholder Mesh and Color Palette Recommendations

Based on the semantic keys above and Unity's batching rules, you can design a placeholder palette
that uses a minimal set of meshes and materials, while still conveying entity roles. Use simple
primitives (cube, sphere, capsule, cylinder, plane) for all entities and differentiate them by
size and color. Color tints can be implemented via material property overrides per entity, which
avoid creating new materials and preserve batching.

Space4X placeholder palette

Semantic key (ID) | Suggested shape | Color hint | Notes
Carrier (200) | Capsule elongated along forward axis | Grey with a stripe | Capital ships; scale to differentiate.
Miner (210) | Cube | Warm orange | Mining drones or haulers.
Asteroid (220) | Sphere | Dark brown/grey | Asteroids and debris.
Projectile (230) | Small elongated capsule | Bright yellow/white | Bullets or lasers; optional trail VFX.
FleetImpostor (240) | Quad (billboard) | Blue | Distant fleets.
Individual (250) | Tiny capsule | Faction color | Crew specialists.
StrikeCraft (260) | Small capsule or cone | Light grey with accent | Fighters; size differentiates from carriers.
ResourcePickup (270) | Sphere or cube | Color by resource type | Floating cargo containers.
GhostTether (280) | Cylinder or line | Translucent white/blue | Tethers/ropes between entities.

Godgame placeholder palette

Semantic key (ID) | Suggested shape | Color hint | Notes
Villagers (100-107) | Capsule | Role colors | Miner orange, Farmer green, Forester brown, Breeder purple,
Worshipper white/gold, Refiner yellow, Peacekeeper blue, Combatant red.
VillageCenter (110) | Cube | Light blue | Central storehouse or town hall.
ResourceChunk (120) | Cube | Resource color | Stone grey, ore grey, wood brown, herbs green, agriculture yellow.
Vegetation (130) | Cylinder + cone | Varied greens | Trees or bushes.
ResourceNode (140) | Cube with small protrusions | Cyan or resource-tinted | Mining/outcrop points.
Storehouse (150) | Cube | Orange | Storage building.
Housing (151) | Cube with gabled roof | Teal | Houses or huts.
Worship (152) | Cube | Purple | Temples or shrines.
ConstructionGhost (160) | Transparent cube | Semi-transparent grey | Under construction.
Band (170) | Capsule group | Yellow | Armies/adventurer groups.
GhostTether (280) | Line or rope | Translucent white/blue | Task tethers.

Implementing the palette

- Create a RenderMeshArray asset containing placeholder meshes (cube, sphere, capsule, cylinder,
  quad) and a few URP materials (Unlit and Lit).
- Define semantic keys in code (Space4XRenderKeys, GodgameSemanticKeys) and ensure the values match
  the catalog entries. Use utilities to map simulation roles to keys.
- Add variants to your Render Catalog asset: for each shape assign the mesh, material and bounds.
  For URP lit materials, set base color to white so tints apply correctly.
- Author themes that map semantic keys to variant indices. Theme 0 can be the greybox theme; future
  themes map the same keys to hero assets.
- Use per-entity material property overrides to apply colors. Entities Graphics supports base-color
  overrides without breaking batching.
- Instantiate prototypes rather than adding rendering components from scratch. Bake each mesh/material
  pair into a SubScene; use EntityManager.Instantiate to clone and set MaterialMeshInfo indices and
  colors at runtime.
- Pool visual entities via your presentation bridge: mark spawn requests with AllowPooling so that
  recycled visuals are reused. This prevents memory churn when your simulation spawns and despawns
  large numbers of entities.

7) Conclusion

Rendering and presenting DOTS entities requires careful separation of concerns and a data-driven
pipeline. Use URP and Entities Graphics; ensure each entity has MaterialMeshInfo, RenderBounds,
and a shared RenderMeshArray. The Presentation Bridge pattern keeps the simulation deterministic
while allowing the visual layer to evolve; spawn/recycle requests drive instantiation and pooling
of visuals without polluting simulation state. The Render Catalog binds stable semantic keys to
mesh/material variants; adding new entries involves updating the catalog asset and writing the
proper keys in gameplay code.

For your data-driven simulation, using simple placeholder meshes (cube, sphere, capsule) with a
small palette of URP materials is not only acceptable but aligns with Unity's best practices for
batching performance. Combining these meshes with per-entity color overrides yields clear visual
categories while keeping draw calls low. As real assets become available, update the catalog's
variants and themes without changing simulation code - the semantic keys remain the same.
