# Space4X Scale and Zoom (Game Appendix)

This appendix applies the shared framework in `puredots/Docs/Concepts/Core/Scale_And_Zoom_Framework.md` to Space4X.

## Scale intent
- Craft should read as tiny compared to carriers.
- Carriers should read as tiny compared to large asteroids.
- Planets are enormous, with an orbital band where fleet battles can occur before zooming to surface.
- Titans and mega-titans can exist far above carrier scale without changing semantics.
- Micro-scale action (bridge or hangar) must remain coherent within macro-scale battles.
- Presentation stays continuous: no scene swaps or layer splits.
- Semantics are size-agnostic; a "frigate" can be 100km if the sim says so.

## Baseline sizes (current targets)
- Individual: 0.5m - 5m
- Craft: 10m - 100m
- Light carrier: ~0.5km
- Heavy carrier: 2km - 5km
- Superheavy carrier: 5km - 10km
- Titan: 20km - 200km
- Mega-titan: 500km - 2,000km
- Large asteroid: 10km - 200km
- Planet radius: 6,000km - 10,000km

## Target ratios (order-of-magnitude)
- Craft : Carrier = 1 : 30-100
- Carrier : Large asteroid = 1 : 5-50
- Large asteroid : Planet radius = 1 : 200-5000
- Carrier : Titan = 1 : 10-100
- Carrier : Mega-titan = 1 : 250-1000

## Proxy rules
- Planets may render as proxy spheres at distance; when zoomed in, surface detail resolves in-place.
- System view uses aggregated proxies for fleets, stations, and colonies while preserving absolute positions.
- Small entities remain visible as LOD glyphs (dots/billboards) at macro zoom.
- Micro entities keep absolute placement so a bridge duel can be observed in the same continuous space.

## Transition bands
- Micro/ship scale: craft and carriers are readable and separable.
- Orbital band: battles occur here; carriers should still be visible as small bodies.
- System band: planets and orbits dominate; fleets become LOD glyphs.
- Galaxy band: systems aggregate to points or minimal glyphs.

## Current tuning touchpoints
- `space4x/Assets/Scripts/Space4x/Presentation/Space4XPresentationDepthSystem.cs` (scale ranges for craft/carrier/asteroids).
- `space4x/Assets/Data/Space4XRenderCatalog_v2.asset` (mesh bounds that must match scale).
- `puredots/Docs/Camera/Space4X_Camera_TruthSource.md` (zoom thresholds to align with tiers).

## Notes
- Keep cull distances generous; use LOD rather than hard cutoffs.
- Any origin shifting must be presentation-only and visually seamless.
