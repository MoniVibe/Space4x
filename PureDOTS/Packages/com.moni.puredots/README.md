# Pure DOTS Core Framework

**Version**: 0.1.0  
**Unity**: 2022.3+  
**License**: [Your License]

## Overview

PureDOTS is an **environmental daemon** - a deterministic DOTS framework providing core simulation infrastructure for Unity game projects. It provides time management, rewind systems, registry infrastructure, spatial partitioning, and authoring tools that games build upon.

## Installation

### Local Development

Add to your game project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.moni.puredots": "file:../PureDOTS/Packages/com.moni.puredots"
  }
}
```

### Git Repository

```json
{
  "dependencies": {
    "com.moni.puredots": "https://github.com/moni/puredots.git?path=/Packages/com.moni.puredots#v1.0.0"
  }
}
```

## Quick Start

1. **Add Package Dependency** (see Installation above)

2. **Create Game Assembly** (`Assets/Scripts/GameName.asmdef`):
   ```json
   {
     "name": "GameName.Runtime",
     "references": [
       "PureDOTS.Runtime",
       "PureDOTS.Systems"
     ]
   }
   ```

3. **Setup Scene**:
   - Add `PureDotsConfigAuthoring` component to root scene
   - Reference `PureDotsRuntimeConfig` asset
   - Create SubScenes for game entities

4. **Implement Game Systems**:
   ```csharp
   using PureDOTS.Runtime.Components;
   using PureDOTS.Systems;
   using Unity.Entities;
   
   namespace GameName.Systems
   {
       [UpdateInGroup(typeof(SimulationSystemGroup))]
       public partial struct GameSystem : ISystem
       {
           public void OnUpdate(ref SystemState state)
           {
               var timeState = SystemAPI.GetSingleton<TimeState>();
               // Use PureDOTS infrastructure
           }
       }
   }
   ```

## Core Features

### Time & Determinism
- Fixed-step time system
- Rewind/playback support
- History recording
- Deterministic simulation groups

### Registry Infrastructure
- Generic registry patterns
- Spatial-aware registries
- Health monitoring
- Metadata tracking

### Spatial Systems
- Spatial grid partitioning
- Cell-based queries
- Residency tracking
- Spatial synchronization

### Authoring Tools
- Component bakers
- Configuration assets
- Editor validation
- Scene setup tools

## Package Structure

```
com.moni.puredots/
├── Runtime/
│   ├── Runtime/        # Core components (TimeState, RewindState, etc.)
│   ├── Systems/        # Framework systems
│   ├── Authoring/      # Baker components
│   ├── Config/         # Configuration assets
│   └── Input/          # Input handling
└── Editor/             # Editor tooling
```

## Assembly Definitions

- **PureDOTS.Runtime**: Core components and data structures
- **PureDOTS.Systems**: Framework systems
- **PureDOTS.Authoring**: Authoring components and bakers
- **PureDOTS.Editor**: Editor-only tools

## Dependencies

**Version Lock** (see `Docs/Vision/core.md` for details):
- Unity Entities **1.4.2** (NOT 1.5+)
- Unity NetCode **1.8** (integration on hold until single-player runtime is stable)
- Unity Input System **1.7.0** (NOT legacy `UnityEngine.Input`)
- Unity Burst 1.8.24
- Unity Collections 2.6.2
- Unity Mathematics 1.3.2
- Unity Physics 1.0.16

**IMPORTANT**: Do NOT upgrade to Entities 1.5+ or use legacy Input APIs. See `Docs/Vision/core.md` for version lock details.

## Documentation

- **Version Lock**: `Docs/Vision/core.md` - Critical version compatibility information
- **Architecture**: `Docs/Architecture/PureDOTS_As_Framework.md`
- **Integration Guide**: `Docs/Guides/UsingPureDOTSInAGame.md`
- **Integration Spec**: `Docs/PUREDOTS_INTEGRATION_SPEC.md` - Canonical integration patterns
- **Vision**: `Docs/Vision.md` - Design pillars and roadmap
- **API Reference**: See source code documentation

## Critical Coding Patterns

**These patterns are MANDATORY. Violations cause compile errors.**

### P0: Verify Dependencies First

Before writing code that uses `TimeState`, `RewindState`, or any component:
```bash
grep -r "struct TimeState" --include="*.cs"
```
If not found: **create it first** or flag as blocker.

### P1: Buffer Mutation - Use Indexed Access

```csharp
// ❌ WRONG (CS1654)
foreach (var item in buffer) { item.Value = 5; }

// ✅ CORRECT
for (int i = 0; i < buffer.Length; i++)
{
    var item = buffer[i];
    item.Value = 5;
    buffer[i] = item;
}
```

### P1: Blob Access - Always Use Ref

```csharp
// ❌ WRONG (EA0001)
var catalog = blobRef.Value;

// ✅ CORRECT
ref var catalog = ref blobRef.Value;
```

### P2: Rewind Guard

```csharp
var rewind = SystemAPI.GetSingleton<RewindState>();
if (rewind.Mode != RewindMode.Record) return;
```

See `Docs/PUREDOTS_INTEGRATION_SPEC.md` for complete pattern documentation.

## Versioning

PureDOTS follows [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking API changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

## Support

For issues, questions, or contributions, see the main repository documentation.

## License

[Your License Here]











