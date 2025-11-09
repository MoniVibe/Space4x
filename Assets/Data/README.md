# Space4X Data Assets

This directory contains ScriptableObject assets and configuration data for the Space4X demo.

## Required Assets

### PureDotsRuntimeConfig
- **File:** `Space4x/Assets/Space4X/Config/PureDotsRuntimeConfig.asset` (already exists)
- **Purpose:** Main configuration for time, history, pooling, and resource types
- **Key Settings:**
  - Resource Types: Minerals, RareMetals, EnergyCrystals, OrganicMatter
  - Time settings: 60 FPS fixed timestep
  - History settings: Default rewind/history configuration

### SpatialPartitionProfile
- **File:** `Space4x/Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset` (already exists)
- **Purpose:** Defines spatial grid for efficient entity queries
- **Key Settings:**
  - Grid dimensions and cell size
  - Coverage area matching demo scene bounds

### Space4XCameraProfile (Optional)
- **File:** `Space4x/Assets/Data/Space4XCameraProfile.asset`
- **Purpose:** Camera movement and control configuration
- **Creation:** Create ScriptableObject if camera profile system is implemented
- **Key Settings:**
  - Pan/Zoom/Rotation speeds
  - Zoom limits
  - Pitch constraints
  - Pan bounds (optional)

### InputActionAsset
- **File:** `Space4x/Assets/InputSystem_Actions.inputactions` (already exists)
- **Purpose:** Defines input actions for camera control
- **Key Actions:**
  - Pan (WASD/Arrow Keys)
  - Zoom (Mouse Scroll)
  - Vertical Move (Q/E)
  - Rotate (Right Mouse Drag)

## Usage

These assets are referenced by authoring components on prefabs:
- `PureDotsConfigAuthoring` → references `PureDotsRuntimeConfig.asset`
- `SpatialPartitionAuthoring` → references `DefaultSpatialPartitionProfile.asset`
- `Space4XCameraAuthoring` → references `Space4XCameraProfile.asset` (if exists)
- `Space4XCameraInputAuthoring` → references `InputSystem_Actions.inputactions`

## Resource Types

Space4X uses these resource types (defined in `ResourceType` enum):
- **Minerals** (0) - Common construction material
- **RareMetals** (1) - Advanced components
- **EnergyCrystals** (2) - Power generation
- **OrganicMatter** (3) - Life support, research

## Registry Entities

Space4X registry entities (Colonies, Fleets, LogisticsRoutes, Anomalies) are typically created via:
- `Space4XSampleRegistryAuthoring` - Bulk creation for testing
- `Space4XMiningDemoAuthoring` - Creates carriers, vessels, asteroids

See `PureDOTS/Docs/TODO/Space4X_PrefabChecklist.md` for detailed creation instructions.

---

**Note:** Carriers, mining vessels, and asteroids have visual representation via `PlaceholderVisualAuthoring` components on their prefabs. Registry entities (colonies, fleets, routes, anomalies) are purely data-driven.


