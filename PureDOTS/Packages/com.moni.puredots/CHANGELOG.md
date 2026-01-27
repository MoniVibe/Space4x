# Changelog

All notable changes to PureDOTS will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **New contracts**: PhysicsProvider v1, PhysicsCollisionEvent v1 (see `Docs/Contracts.md`)
- **Provider architecture**: IPhysicsProvider interface with implementations (None, Entities, Havok stub)
- **Adapter structure**: Adapters folder organization in game projects
- **Contract documentation**: `Docs/Contracts.md` for shared system contracts

### Changed
- **PhysicsConfig**: Added ProviderId field for selecting physics backend
- **PhysicsEventSystem**: Now respects ProviderId selection (skips if None or non-Entities)
- **Adapters.md**: Updated with adapter folder structure documentation
- **INTEGRATION_GUIDE.md**: Added provider and contract patterns section

### Planned
- API documentation generation
- Performance benchmarking suite
- Additional spatial grid providers

## [0.1.0] - 2025-01-XX

### Added
- Core time and rewind system
- Registry infrastructure with spatial awareness
- Spatial grid system with residency tracking
- Resource management systems
- Authoring components and bakers
- Editor tooling and validation
- Deterministic simulation groups
- Framework bootstrap system

### Framework Components
- `TimeState`, `RewindState`, `HistorySettings`
- `ResourceRegistry`, `StorehouseRegistry`, `VillagerRegistry`
- `SpatialGridConfig`, `SpatialGridState`
- `RegistryMetadata`, `RegistryHealth`

### Systems
- `TimeTickSystem`, `RewindCoordinatorSystem`
- `ResourceRegistrySystem`, `StorehouseRegistrySystem`
- `SpatialGridBuildSystem`, `SpatialGridUpdateSystem`
- `PureDotsWorldBootstrap`

### Authoring
- `PureDotsConfigAuthoring`, `TimeSettingsAuthoring`
- `ResourceSourceAuthoring`, `StorehouseAuthoring`
- `VillagerAuthoring` (framework example)

### Changed
- Moved game-specific transport components out of framework
- Formalized package structure
- Established framework/game separation conventions

### Fixed
- Singleton cleanup system timing
- ResourceEntries NativeArray initialization
- Transform sync conflicts
- Registry bootstrap ordering

## Version History

- **0.1.0**: Initial framework release
  - Core infrastructure established
  - Registry and spatial systems implemented
  - Authoring tools provided
  - Framework/game separation formalized











