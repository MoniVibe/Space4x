# Space4X RenderKey Truth Source

**Canonical RenderKey Type for Space4X**: `Space4X.Rendering.RenderKey`

**Source of Truth**: `Assets/Scripts/Space4x/Rendering/Systems/CheckRenderEntitiesSystem.cs`

**Evidence**: This system successfully queries 50 RenderKey entities at runtime, confirming this is the correct type.

**Fields Used**: ArchetypeId, KeyId (standard render key fields)

**All Space4X render systems MUST use this type**. No other RenderKey variations (PureDOTS.Rendering.RenderKey, Space4XRenderKey, etc.) should be used in Space4X codebase.

## Current Status
- ✅ CheckRenderEntitiesSystem: Uses correct type
- ✅ DiagnoseEntityComponents: Fixed to use Space4X.Rendering.RenderKey
- ✅ ApplyRenderCatalogSystem: Fixed to use Space4X.Rendering.RenderKey
- ✅ Space4X_TestRenderKeySpawnerSystem: Fixed to use Space4X.Rendering.RenderKey

## Migration Notes
When updating other systems to use the canonical type:
1. Update namespace imports to include `Space4X.Rendering`
2. Change component queries from other RenderKey types to `Space4X.Rendering.RenderKey`
3. Verify entity counts match CheckRenderEntitiesSystem (currently 50 entities)

## Fixed Systems (CoPlay Report 2)
✅ DiagnoseEntityComponents: Now queries `Space4X.Rendering.RenderKey` directly (removed reflection)
✅ ApplyRenderCatalogSystem: Fixed to use Space4X.Rendering.RenderKey + cloned bootstrap attributes from working spawner
✅ Space4X_TestRenderKeySpawnerSystem: Changed from `PureDOTS.Rendering.RenderKey` alias to `Space4X.Rendering.RenderKey`
✅ DebugVerifyVisualsSystem: New diagnostic system to verify MaterialMeshInfo assignment

## Diagnostic Pipeline
1. **CheckRenderEntitiesSystem**: Verifies RenderKey entities exist (50 expected)
2. **DiagnoseEntityComponents**: Detailed inspection of RenderKey entities
3. **DebugVerifyVisualsSystem**: Confirms MaterialMeshInfo assignment (critical render success)

All systems should now find the same 50 RenderKey entities that CheckRenderEntitiesSystem reports.

## Bootstrap Integration Fix
### ApplyRenderCatalogSystem Creation Issue
**Problem**: System was not being created in Default World despite having correct logic.

**Root Cause**: Bootstrap heuristics were filtering out the system despite correct attributes.

**Solution Applied**:
1. **Converted to SystemBase**: Changed from `ISystem` struct to `SystemBase` class to match working systems
2. **Copied exact attributes** from `CheckRenderEntitiesSystem` (which does get created):
   - `[UpdateInGroup(typeof(SimulationSystemGroup))]` (same as working system)
   - Changed from `struct` to `class` inheritance
3. **Forced creation failsafe**: Added code in `Space4XCoreSingletonGuardSystem.OnCreate()` to manually instantiate via `GetOrCreateSystemManaged<T>()`

**Expected Logs**: When you enter Play mode, you should now see:
```
[Space4XCoreSingletonGuardSystem] Forced creation of ApplyRenderCatalogSystem.
[ApplyRenderCatalogSystem] OnCreate in world: Default World
[ApplyRenderCatalogSystem] OnUpdate tick (TEMP).
```

**Status**: ✅ Restored real catalog logic using SystemBase patterns. System now assigns MaterialMeshInfo and bounds to RenderKey entities.

**Current Group**: `SimulationSystemGroup` (confirmed working). Can move to `PresentationSystemGroup` once render timing is verified.

**New Debug System**: `DebugVerifyVisualsSystem` added to verify MaterialMeshInfo assignment success.

## Expected Warnings (Ignore for Now)
### UpdateAfter Attribute Warnings
**Pattern**: `"Ignoring invalid [UpdateAfterAttribute]"` or similar

**Cause**: Systems in other groups (PureDOTS core, demo, AI, etc.) have ordering attributes inconsistent with their group membership.

**Status**: Expected and harmless - these do not prevent the world from running.

**Action**: **Do not touch these systems** until ECS visuals + camera sanity are fully implemented.

**Console Filtering**: Consider adding a log filter in Unity Console to hide:
- `Ignoring invalid [Unity.Entities.Update`
- `Ignoring invalid [UpdateAfterAttribute]`

This will reduce noise while debugging Space4X rendering and camera systems.
