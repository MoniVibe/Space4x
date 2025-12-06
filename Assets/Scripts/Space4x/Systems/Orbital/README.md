# Space4X Orbital Integration

Space4X-specific orbital motion systems building on PureDOTS orbital framework.

## Systems

### `Carrier6DoFMotionSystem`
Carrier-specific 6-DoF motion constraints.

**Features**:
- Angular velocity damping (0.95x per frame)
- Max angular velocity limit (1.0 rad/s)
- Linear velocity damping (atmospheric drag simulation)
- Runs at 60 Hz (Local6DoFSystem domain)

**Usage**: Automatically processes carriers with `SixDoFState` + `OrbitalObjectTag`.

### `FleetOrbitalSystem`
Fleet-level orbital motion inheriting from parent stellar systems.

**Features**:
- Update frequency: 0.01 Hz (every 600 ticks at 60Hz)
- Mean-field drift based on shell membership
- Links to `Space4XFleet` component

**Usage**: Automatically processes fleets with `SixDoFState` + `Space4XFleet` + `ShellMembership`.

## Authoring

Use `OrbitalAuthoring` component (from PureDOTS) on:
- Carrier prefabs
- Fleet entities
- Stellar system frames
- Planetary objects

See `PureDOTS/Docs/Guides/Orbital6DoFIntegrationGuide.md` for authoring setup.

## Integration Points

### Mining System
Carriers with orbital motion can mine while orbiting. Mining system reads `SixDoFState.Position` for mining location.

### Fleet Combat
Fleet orbital motion provides base position for combat calculations. Combat systems query `SixDoFState` for fleet positions.

### Jump Routes
Use `SphericalShellQuerySystem.QueryEntitiesInRadius()` to find jump destinations within range.

## See Also

- `PureDOTS/Docs/Guides/Orbital6DoFIntegrationGuide.md` - Complete integration guide
- `PureDOTS/Runtime/Systems/Orbital/README.md` - System reference

