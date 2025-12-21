# Scale + LOD Notes (Space4X)

Goal: seamless space scale where small craft, carriers, asteroids, and planets coexist without layer swaps.

## Principles
- LOD is presentation-only. Simulation state and orbits are fixed-step deterministic.
- Large battles and small-scale interactions must remain in the same runtime layer.

## Layer mapping (default intent)
- Orbital: vessels, strike craft, projectiles, resource pickups
- System: carriers, asteroids, fleet impostors
- Galactic: far-distance icons for fleets/systems when needed

## Presentation hooks
- PresentationLayerConfig authoring drives per-layer distance multipliers.
- RenderKey.LOD drives impostor/icon swaps at range.
- Cull distances remain presentation-only and can scale per layer.

## Scale cues
- Carriers vs vessels vs asteroids: keep real scale in sim, swap to icons for distance.
- Planets can be proxy-anchored visually, but simulation remains continuous.
