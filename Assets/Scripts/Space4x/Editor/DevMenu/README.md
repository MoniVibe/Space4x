# Space4X Developer Menu

A RimWorld-style developer menu for runtime entity spawning, inspection, and testing.

## Quick Start

1. **Add DevMenu to Scene**
   - Create an empty GameObject in your scene
   - Add the `Space4XDevMenuUI` component
   - Assign a `Space4XDevSpawnRegistry` asset (create one via Assets > Create > Space4X > Dev > Spawn Registry)

2. **Toggle Menu**
   - Press **F12** or **~** (tilde) to open/close the dev menu

3. **Spawn Entities**
   - Select a faction (Player, Pirates, Traders)
   - Set spawn count and spread
   - Click on a category (Carriers, Capital Ships, etc.)
   - Click "Spawn" next to any template

## Features

### Spawn Panel
- **Faction Selection**: Choose which faction spawned entities belong to
- **Spawn Count**: 1-1000 entities at once (use x10 for bulk)
- **Spread**: How far apart spawned entities are placed
- **Spawn at Cursor**: Toggle to spawn at mouse position vs manual coordinates
- **Search Filter**: Filter templates by name

### Entity Categories
- **Carriers**: Light, Fleet, Super carriers with hangar bays
- **Capital Ships**: Frigates, Destroyers, Cruisers, Battleships, Dreadnoughts
- **Strike Craft**: Fighters, Bombers, Interceptors, Gunships
- **Support Vessels**: Miners, Haulers, Repair Tenders
- **Stations**: Outposts, Starbases
- **Celestial**: Asteroids with various resource types

### Quick Actions (Debug Tab)
- **Spawn Test Fleet**: 1 Carrier + 12 Fighters for quick testing
- **Spawn Combat Scenario**: Two opposing fleets for combat testing
- **Spawn Mining Operation**: Carrier + miners + asteroids

### Entity List
- View all spawned entities with positions
- Click "→" to focus camera on entity

## Creating New Templates

Edit the `Space4XDevSpawnRegistry` asset to add new entity templates:

```csharp
// In Space4XDevSpawnRegistry.cs
public CarrierTemplate[] carriers = new CarrierTemplate[]
{
    new CarrierTemplate
    {
        id = "my_custom_carrier",
        displayName = "Custom Carrier",
        description = "My custom carrier description",
        speed = 6f,
        hangarCapacity = 18,
        shieldStrength = 750f,
        hullPoints = 1500f,
        color = new Color(0.4f, 0.5f, 0.7f)
    }
};
```

## Integration with Combat Demo

Use with `Space4XCombatDemoAuthoring` for pre-configured combat scenarios:

1. Create a new scene
2. Add a GameObject with:
   - `PureDotsConfigAuthoring`
   - `SpatialPartitionAuthoring`
   - `Space4XCombatDemoAuthoring`
3. Configure fleet compositions in the inspector
4. Enter play mode - fleets spawn automatically
5. Use DevMenu (F12) to spawn additional entities

## Performance Testing

The dev menu is designed for Burst-compatible spawning:
- Spawn 100+ entities at once for stress testing
- Use the Entity List to monitor entity counts
- Watch Debug tab for frame time impact

## Files

```
Assets/Scripts/Space4x/
├── Editor/DevMenu/
│   ├── Space4XDevSpawnRegistry.cs    # Template catalog (ScriptableObject)
│   ├── Space4XDevMenuUI.cs           # Runtime IMGUI menu
│   └── README.md                      # This file
├── Systems/Dev/
│   └── Space4XDevSpawnSystem.cs      # Processes spawn requests
└── Authoring/
    └── Space4XCombatDemoAuthoring.cs # Pre-configured combat scenes
```

## Tips

- Hold **Shift** when clicking "Clear All Entities" to confirm
- Use high spawn counts (100+) to test Burst/ECS performance
- The dev menu works in builds, not just the editor
- Spawned entities get full AI components - they will behave autonomously

