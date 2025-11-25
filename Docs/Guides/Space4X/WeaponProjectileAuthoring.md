# Weapon & Projectile Authoring Guide

This guide explains how to author weapons, projectiles, and turrets in Space4X using the Prefab Maker tool.

## Overview

Weapons, projectiles, and turrets in Space4X are **data-driven**. Their behavior is defined entirely by specs that are baked into blob assets at build time. The Prefab Maker generates:

1. **Spec Blobs**: Data structures that define weapon/projectile/turret behavior
2. **Optional Presentation Tokens**: Thin visual-only prefabs for muzzle flashes, tracers, impacts, and turret shells

This design preserves determinism, avoids asset debt, and keeps gameplay free of GameObject ties.

## Step 1: Create Catalog Prefabs

### Weapon Catalog

1. Create a GameObject in your scene or as a prefab
2. Add the `WeaponCatalogAuthoring` component
3. Add weapon entries to the `weapons` list:

```csharp
// Each weapon entry defines:
- id: Unique identifier (e.g., "LaserCannon_Mk1")
- weaponClass: Laser, Kinetic, Missile, Beam, Plasma
- fireRate: Shots per second
- burstCount: Burst count (1..N)
- spreadDeg: Cone spread in degrees
- energyCost: Energy cost per shot
- heatCost: Heat cost per shot
- leadBias: Lead bias (0..1, aiming hint)
- projectileId: References a ProjectileSpec ID
```

### Projectile Catalog

1. Create a GameObject with `ProjectileCatalogAuthoring` component
2. Add projectile entries:

```csharp
// Each projectile entry defines:
- id: Unique identifier (e.g., "LaserBolt_Standard")
- kind: Ballistic, BeamTick, Missile, AoE
- speed: m/s (0 for hitscan beam)
- lifetime: Seconds
- gravity: m/s² (0 for space)
- turnRateDeg: Homing turn rate (for missiles)
- seekRadius: Homing acquisition radius
- pierce: How many targets can pass through
- chainRange: Chaining arc range (0 = none)
- aoERadius: Explosion radius
- damage: Kinetic, Energy, Explosive values
- onHitEffects: List of effect operations (status, knockback, etc.)
```

### Turret Catalog

1. Create a GameObject with `TurretCatalogAuthoring` component
2. Add turret entries:

```csharp
// Each turret entry defines:
- id: Unique identifier (e.g., "Turret_Standard")
- arcLimitDeg: Traverse arc limit (0-360)
- traverseSpeedDegPerSec: Traverse speed
- elevationMinDeg: Minimum elevation (-90 to 90)
- elevationMaxDeg: Maximum elevation (-90 to 90)
- recoilForce: Recoil force
- socketName: Socket name for muzzle binding (e.g., "Socket_Muzzle")
```

## Step 2: Use Prefab Maker

1. Open **Window > Space4X > Prefab Maker**
2. Navigate to the **Editor** tab
3. Select **Weapons**, **Projectiles**, or **Turrets** category
4. Templates will be loaded from your catalog prefabs
5. Edit templates as needed (validation will show issues)
6. Use **Batch Generate** tab to generate:
   - Spec blobs (always generated)
   - Presentation tokens (optional, disable "Placeholders Only" to generate)

## Step 3: Validation Rules

### Weapon Validation
- Fire rate must be >= 0
- Burst count must be >= 1
- Spread must be >= 0
- Energy/heat costs must be >= 0
- Lead bias must be between 0 and 1
- Projectile ID must reference a valid projectile

### Projectile Validation
- Speed must be >= 0
- Lifetime must be >= 0
- Gravity must be >= 0
- Turn rate must be between 0 and 720 deg/s
- Seek radius must be >= 0
- Pierce must be >= 0
- Chain range must be >= 0
- AoE radius must be >= 0
- **Beam constraint**: Beam projectiles (ProjectileKind.BeamTick) must have Speed = 0
- **Missile constraint**: Missiles require TurnRateDeg > 0
- **Damage budget**: Projectile must have at least some damage

### Turret Validation
- Arc limit must be between 0 and 360 degrees
- Traverse speed must be >= 0
- Elevation angles must be between -90 and 90 degrees
- Min elevation must be <= max elevation
- Recoil force must be >= 0
- Socket name is required

### Cross-Reference Validation
- Every weapon's `projectileId` must reference a valid projectile in the ProjectileCatalog

## Step 4: Presentation Tokens (Optional)

Presentation tokens are thin visual-only prefabs that can be bound to weapons/projectiles/turrets at runtime. They are **optional** - the simulation will run without them (only visuals will be missing).

### Weapon Presentation Tokens
- Generated as `{WeaponId}_Muzzle.prefab`
- Contains `WeaponIdAuthoring` component
- Placeholder visual: small sphere (muzzle flash)

### Projectile Presentation Tokens
- Generated as `{ProjectileId}_Tracer.prefab` and `{ProjectileId}_Impact.prefab`
- Contains `ProjectileIdAuthoring` component
- Placeholder visuals: cylinder (tracer), sphere (impact)

### Turret Presentation Tokens
- Generated as `{TurretId}.prefab`
- Contains `TurretIdAuthoring` component
- Placeholder visual: cylinder (turret base)
- Includes socket child transform for muzzle binding

## Step 5: Binding Sets

The Prefab Maker generates two binding sets:

- **Minimal**: Essential bindings only
- **Fancy**: Full bindings with metadata

These are stored in:
- `Assets/Space4X/Bindings/Space4XPresentationBinding_Minimal.asset`
- `Assets/Space4X/Bindings/Space4XPresentationBinding_Fancy.asset`

Bindings map:
- `WeaponId` → muzzle FX prefab
- `ProjectileId` → tracer/impact prefabs
- `TurretId` → turret shell prefab

## Runtime Systems

### FireSystem (FixedStep)
- Reads `WeaponSpec` from blob
- Checks cooldown/energy/heat
- Computes lead using `leadBias`
- Enqueues projectile entities with `ProjectileSpec` via ECB

### ProjectileAdvanceSystem (FixedStep)
- Integrates ballistic motion or homing
- Performs hits (Unity Physics or ray/overlap)
- Applies `EffectOp`s from `ProjectileSpec`
- Decrements pierce/chain counters
- Queues impact FX requests

### BeamSystem (FixedStep)
- Samples hitscan each tick
- Applies tick damage
- Fires beam FX requests

### Presentation Systems
- Read request buffers from gameplay systems
- Play muzzle/beam/impact visuals based on bindings
- No structural changes outside Begin/End Presentation ECBs

## Testing & Determinism

The Prefab Maker includes validation for:

- **Idempotency**: Running the generator twice produces identical hashes (blobs + prefabs)
- **Determinism**: Seeded firing scenarios produce same hit and damage totals at different frame rates
- **Homing bounds**: Missiles never NaN; angle clamp respected
- **Pierce/Chain invariants**: No extra hits beyond limits
- **Binding optionality**: Removing all presentation bindings → simulation still runs; only visuals disappear

## Best Practices

1. **Keep specs GameObject-free**: Only IDs, numbers, and references by ID
2. **Use presentation tokens sparingly**: Only generate them if you need visual feedback during development
3. **Validate cross-references**: Ensure every weapon's `projectileId` exists in the ProjectileCatalog
4. **Test determinism**: Run seeded firing scenarios to verify consistent behavior
5. **Document damage budgets**: Keep damage values reasonable per weapon class

## Example Workflow

1. Create `WeaponCatalog.prefab` with a laser cannon entry
2. Create `ProjectileCatalog.prefab` with a laser bolt entry
3. Reference the projectile ID in the weapon's `projectileId` field
4. Open Prefab Maker → Editor tab → Weapons
5. Verify validation passes (green checkmarks)
6. Generate prefabs (specs + optional presentation tokens)
7. Test in play mode: weapon fires projectiles with correct behavior

