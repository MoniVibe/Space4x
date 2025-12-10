// DEPRECATED: This file has been replaced by:
// 1. PureDOTS.Runtime.Rendering types (RenderLODData, RenderCullable, RenderSampleIndex) - use these for LOD/culling
// 2. Space4X.Presentation types in Assets/Scripts/Space4x/Rendering/Space4XPresentationTags.cs - use these for Space4X-specific tags
//
// Migration guide:
// - Replace Space4X.Presentation.RenderLODData with PureDOTS.Runtime.Rendering.RenderLODData
//   - Field rename: DistanceToCamera -> CameraDistance
//   - Field rename: Importance -> ImportanceScore
// - Replace Space4X.Presentation.RenderCullable with PureDOTS.Runtime.Rendering.RenderCullable (structure matches)
// - Replace Space4X.Presentation.RenderSampleIndex with PureDOTS.Runtime.Rendering.RenderSampleIndex
//   - Field rename: Index -> SampleIndex
// - Space4X-specific tags (CarrierPresentationTag, FleetImpostorTag, etc.) moved to Space4XPresentationTags.cs
//
// Do not use types from this file - they are legacy duplicates.
