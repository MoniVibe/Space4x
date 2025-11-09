# Archived: Hybrid Showcase Scenes

**Status**: ARCHIVED - No longer in active use

These scenes were created for the hybrid showcase concept that combined both Godgame and Space4x in a single scene. This approach has been deprecated in favor of developing each project independently.

## Contents

- `HybridShowcase.unity` - Main hybrid scene
- `GodgameShowcase_SubScene.unity` - Godgame subscene
- `Space4XShowcase_SubScene.unity` - Space4X subscene
- `HybridPresentationRegistry.asset` - Presentation registry for hybrid scene

## Why Archived

1. Cross-project asset sharing is complex in Unity
2. Each project should maintain its own scenes and assets
3. PureDOTS package systems are still available to both projects independently

## Migration

If you need functionality from these scenes:
- Extract game-specific content to respective project scenes
- Use PureDOTS package systems (`HybridControlCoordinator`, etc.) in each project independently
- Create project-specific bootstrap scripts if needed

## Reference

See `PureDOTS/Docs/ScenePrep/HybridShowcaseDecision.md` for full context.


