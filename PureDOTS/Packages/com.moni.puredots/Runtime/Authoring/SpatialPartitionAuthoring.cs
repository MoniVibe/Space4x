using PureDOTS.Runtime.Spatial;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PureDOTS/Spatial Partition Authoring")]
    public sealed class SpatialPartitionAuthoring : MonoBehaviour
    {
        public SpatialPartitionProfile profile;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (profile == null || !profile.DrawGizmo)
            {
                return;
            }

            var bounds = profile.ToBounds();
            var cellSize = profile.CellSize;
            var config = profile.ToComponent();
            var cellCounts = config.CellCounts;
            
            // Draw bounds wireframe
            Gizmos.color = profile.GizmoColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            
            // Draw semi-transparent fill
            Gizmos.color = new Color(profile.GizmoColor.r, profile.GizmoColor.g, profile.GizmoColor.b, Mathf.Clamp01(profile.GizmoColor.a * 0.3f));
            Gizmos.DrawCube(bounds.center, bounds.size);
            
            // Draw cell grid lines (limit to reasonable count to avoid performance issues)
            if (cellCounts.x <= 64 && cellCounts.z <= 64)
            {
                var min = bounds.min;
                var max = bounds.max;
                var gridColor = new Color(profile.GizmoColor.r, profile.GizmoColor.g, profile.GizmoColor.b, profile.GizmoColor.a * 0.5f);
                Gizmos.color = gridColor;
                
                // Draw XZ plane grid lines (for 2D navigation)
                if (profile.LockYAxisToOne || cellCounts.y == 1)
                {
                    var y = bounds.center.y;
                    
                    // Draw lines along X axis
                    for (int z = 0; z <= cellCounts.z; z++)
                    {
                        var zPos = min.z + (z * cellSize);
                        if (zPos <= max.z)
                        {
                            Gizmos.DrawLine(
                                new Vector3(min.x, y, zPos),
                                new Vector3(max.x, y, zPos));
                        }
                    }
                    
                    // Draw lines along Z axis
                    for (int x = 0; x <= cellCounts.x; x++)
                    {
                        var xPos = min.x + (x * cellSize);
                        if (xPos <= max.x)
                        {
                            Gizmos.DrawLine(
                                new Vector3(xPos, y, min.z),
                                new Vector3(xPos, y, max.z));
                        }
                    }
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            // Draw bounds outline even when not selected (subtle)
            if (profile != null && profile.DrawGizmo)
            {
                var bounds = profile.ToBounds();
                var outlineColor = new Color(profile.GizmoColor.r, profile.GizmoColor.g, profile.GizmoColor.b, profile.GizmoColor.a * 0.2f);
                Gizmos.color = outlineColor;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }

    public sealed class SpatialPartitionBaker : Baker<SpatialPartitionAuthoring>
    {
        public override void Bake(SpatialPartitionAuthoring authoring)
        {
            if (authoring.profile == null)
            {
                Debug.LogWarning("SpatialPartitionAuthoring has no profile asset assigned.", authoring);
                return;
            }

            var config = authoring.profile.ToComponent();
            var state = CreateDefaultState();
            var thresholds = new SpatialRebuildThresholds
            {
                MaxDirtyOpsForPartialRebuild = authoring.profile.MaxDirtyOpsForPartialRebuild,
                MaxDirtyRatioForPartialRebuild = authoring.profile.MaxDirtyRatioForPartialRebuild,
                MinEntryCountForPartialRebuild = authoring.profile.MinEntryCountForPartialRebuild
            };

            // Removed runtime injection logic to prevent duplicate singleton creation.
            // The CoreSingletonBootstrapSystem handles the default creation if no authoring exists.
            // If authoring exists, this baker will create the entity in the baking world, which is then moved to the main world.
            // The previous logic was trying to modify the main world directly from a baker, which is generally unsafe and can lead to race conditions or duplicates.

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, config);
            AddComponent(entity, state);
            AddComponent(entity, thresholds);
            AddBuffer<SpatialGridCellRange>(entity);
            AddBuffer<SpatialGridEntry>(entity);
            AddBuffer<SpatialGridStagingEntry>(entity);
            AddBuffer<SpatialGridStagingCellRange>(entity);
            AddBuffer<SpatialGridEntryLookup>(entity);
            AddBuffer<SpatialGridDirtyOp>(entity);
        }

        private static SpatialGridState CreateDefaultState()
        {
            return new SpatialGridState
            {
                ActiveBufferIndex = 0,
                TotalEntries = 0,
                Version = 0,
                LastUpdateTick = 0,
                LastDirtyTick = 0,
                DirtyVersion = 0,
                DirtyAddCount = 0,
                DirtyUpdateCount = 0,
                DirtyRemoveCount = 0,
                LastRebuildMilliseconds = 0f,
                LastStrategy = SpatialGridRebuildStrategy.None
            };
        }

        private static void EnsureBuffer<T>(EntityManager entityManager, Entity entity) where T : unmanaged, IBufferElementData
        {
            if (!entityManager.HasBuffer<T>(entity))
            {
                entityManager.AddBuffer<T>(entity);
            }
        }
    }
}
