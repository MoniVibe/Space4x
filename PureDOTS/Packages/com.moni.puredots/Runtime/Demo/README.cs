/// <summary>
/// PureDOTS Demo Assembly - DEMO-ONLY SYSTEMS FOR TESTING
///
/// IMPORTANT: This assembly contains DEMO-ONLY systems that are disabled by default.
/// They are provided solely for testing and validation of PureDOTS infrastructure.
/// Real games should NOT use these systems in production.
///
/// DEMO SYSTEMS ARE DISABLED BY DEFAULT:
/// All demo systems have [DisableAutoCreation] and only run when the "demo" profile
/// is explicitly activated via PURE_DOTS_BOOTSTRAP_PROFILE=demo environment variable
/// or SystemRegistry.OverrideActiveProfile().
///
/// RENDERING PATH FOR REAL GAMES:
/// ==============================
/// Real games should implement their own rendering pipeline:
///
/// 1. Define game-specific RenderKey components (e.g., Space4XRenderKey, GodgameRenderKey)
/// 2. Create RenderCatalogAuthoring/Baker that populates render data
/// 3. Implement ApplyRenderCatalogSystem that assigns MaterialMeshInfo based on RenderKeys
/// 4. Use game-specific RenderMeshArrays with proper mesh/material indices
///
/// DO NOT use the demo rendering systems (SharedRenderBootstrap, AssignVisualsSystem, etc.)
/// in real game scenes. They are examples only.
///
/// OVERVIEW:
/// This assembly provides game-agnostic demo systems and components that host games
/// (Godgame, Space4x) can reference to get visual ECS validation and simple demo behaviors.
///
/// NAMESPACES:
///
/// - PureDOTS.Demo.Orbit: Orbit demo systems and components (DEMO ONLY)
/// - PureDOTS.Demo.Village: Village demo systems and components (DEMO ONLY)
/// - PureDOTS.Demo.Rendering: Shared rendering utilities for demos (EXAMPLE ONLY)
///
/// ORBIT DEMO (PureDOTS.Demo.Orbit) - DEMO ONLY:
///
/// Systems:
/// - OrbitCubeSystem: [DisableAutoCreation] Spawns debug cubes. Only runs in demo profile.
///
/// Components:
/// - OrbitCubeTag: Tag component marking orbit cube entities
/// - OrbitCube: Orbital motion parameters (radius, angular speed, angle, height)
///
/// VILLAGE DEMO (PureDOTS.Demo.Village) - DEMO ONLY:
///
/// Systems:
/// - VillageDemoBootstrapSystem: [DisableAutoCreation] Creates demo entities. Only runs in demo profile.
/// - VillageVisualSetupSystem: [DisableAutoCreation] Sets up demo visuals. Only runs in demo profile.
/// - VillagerWalkLoopSystem: [DisableAutoCreation] Moves villagers. Only runs in demo profile.
/// - VillageDebugSystem: [DisableAutoCreation] Logs counts. Only runs in demo profile.
///
/// Components:
/// - VillageWorldTag: World-level tag to enable demo village systems
/// - VillageTag: Marks village aggregate entities (homes, workplaces)
/// - VillagerTag: Marks villager entities
/// - HomeLot: Position marker for village home structures (float3 Position)
/// - WorkLot: Position marker for village workplace structures (float3 Position)
/// - VillagerHome: Stores a villager's home position (float3 Position)
/// - VillagerWork: Stores a villager's work position (float3 Position)
/// - VillagerState: Tracks villager phase (byte Phase; 0=going to work, 1=going home)
///
/// RENDERING UTILITIES (PureDOTS.Demo.Rendering) - EXAMPLE ONLY:
///
/// IMPORTANT: These are examples for testing. Real games should implement their own rendering.
///
/// - DemoMeshIndices: Static class with mesh index constants (example only)
/// - RenderMeshArraySingleton: Shared component for demo rendering (example only)
/// - SharedRenderBootstrap: [DisableAutoCreation] Demo bootstrap. Only runs in demo profile.
/// - UniversalDebugRenderSetupSystem: [DisableAutoCreation] Auto-assigns render components. Only runs in demo profile.
/// - VisualProfileBootstrapSystem: [DisableAutoCreation] Demo visual catalog. Only runs in demo profile.
/// - AssignVisualsSystem: [DisableAutoCreation] Demo render assignment. Only runs in demo profile.
///
/// REQUIREMENTS FOR DEMO USAGE:
///
/// To activate demo systems (for testing only):
/// 1. Set environment variable: PURE_DOTS_BOOTSTRAP_PROFILE=demo
/// 2. Or call: SystemRegistry.OverrideActiveProfile(SystemRegistry.BuiltinProfiles.Demo.Id)
/// 3. Populate RenderMeshArraySingleton with meshes at DemoMeshIndices
///
/// USAGE FOR TESTING ONLY:
///
/// Demo systems are for infrastructure validation, not game development.
/// Use them to verify PureDOTS systems work, then implement proper game-specific systems.
/// </summary>
namespace PureDOTS.Demo
{
    // This file exists only for documentation purposes.
    // The actual implementation is in the Orbit/, Village/, and Rendering/ subdirectories.
}

