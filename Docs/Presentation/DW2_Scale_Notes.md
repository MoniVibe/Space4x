# DW2-Style Scale + Presentation Notes (Space4X)

## Sim domain separation
- Keep strategic/empire logic cached and periodic.
- Keep unit-level behavior responsive and local.

## Cache + invalidation examples
- Fleet knowledge, refuel hubs, mining routes, threat volumes.
- Invalidate on: capture, hub build/destroy, treaty changes, new colony.

## Presentation performance
- Use PresentationLayerConfig + RenderKey.LOD to omit small meshes when zoomed out.
- Batch projectiles and impacts into unified buffers where possible.
- Prefer procedural shader variation for stars/nebulae over unique assets.

## Travel as cost fields
- Nebulae/storm regions should modify travel cost, damage risk, sensor visibility.
- Navigation uses cost fields, not hard lanes.
