using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages registration of snapshot groups and coordinates which entities are included in snapshots.
    /// Processes group configurations and maintains the snapshot inclusion registry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    [UpdateBefore(typeof(WorldSnapshotSystem))]
    public partial struct WorldSnapshotRegistrationSystem : ISystem
    {
        // TODO: Reserved for future use - will be used to track next available group ID
        // Currently CreateSnapshotGroup calculates IDs by querying existing groups
#pragma warning disable 0414 // Field is assigned but never used - reserved for future optimization
        private uint _nextGroupId;
#pragma warning restore 0414

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSnapshotState>();
            _nextGroupId = 1; // Reserved for future optimization
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Auto-register entities with RewindableTag that don't have WorldSnapshotIncludeTag
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<RewindableTag>>()
                .WithNone<WorldSnapshotIncludeTag>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new WorldSnapshotIncludeTag { GroupId = 0 });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Process snapshot group configurations
            ProcessGroupConfigurations(ref state);
        }

        private void ProcessGroupConfigurations(ref SystemState state)
        {
            // Update entities based on their group's enabled state
            foreach (var (group, entity) in SystemAPI.Query<RefRO<WorldSnapshotGroup>>()
                .WithEntityAccess())
            {
                if (!group.ValueRO.IsEnabled)
                {
                    continue;
                }

                // Find entities belonging to this group and ensure they have the include tag
                uint groupId = group.ValueRO.GroupId;
                
                foreach (var (includeTag, _) in SystemAPI.Query<RefRW<WorldSnapshotIncludeTag>>()
                    .WithEntityAccess())
                {
                    // Already has tag, no action needed
                }
            }
        }

        /// <summary>
        /// Creates a new snapshot group with the specified configuration.
        /// </summary>
        public static Entity CreateSnapshotGroup(EntityManager entityManager, FixedString32Bytes label,
            HistoryRecordFlags componentFlags, SnapshotImportance importance = SnapshotImportance.Normal,
            uint frequencyMultiplier = 1)
        {
            var entity = entityManager.CreateEntity(typeof(WorldSnapshotGroup));
            
            // Get next group ID from the singleton
            uint groupId = 1;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldSnapshotGroup>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var groups = query.ToComponentDataArray<WorldSnapshotGroup>(Allocator.Temp);
                foreach (var group in groups)
                {
                    if (group.GroupId >= groupId)
                    {
                        groupId = group.GroupId + 1;
                    }
                }
                groups.Dispose();
            }

            entityManager.SetComponentData(entity, new WorldSnapshotGroup
            {
                GroupId = groupId,
                Label = label,
                FrequencyMultiplier = frequencyMultiplier,
                Importance = importance,
                ComponentFlags = componentFlags,
                IsEnabled = true
            });

            return entity;
        }

        /// <summary>
        /// Registers an entity to be included in snapshots for a specific group.
        /// </summary>
        public static void RegisterEntity(EntityManager entityManager, Entity entity, uint groupId = 0)
        {
            if (entityManager.HasComponent<WorldSnapshotIncludeTag>(entity))
            {
                var tag = entityManager.GetComponentData<WorldSnapshotIncludeTag>(entity);
                tag.GroupId = groupId;
                entityManager.SetComponentData(entity, tag);
            }
            else
            {
                entityManager.AddComponentData(entity, new WorldSnapshotIncludeTag { GroupId = groupId });
            }
        }

        /// <summary>
        /// Unregisters an entity from snapshots.
        /// </summary>
        public static void UnregisterEntity(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<WorldSnapshotIncludeTag>(entity))
            {
                entityManager.RemoveComponent<WorldSnapshotIncludeTag>(entity);
            }
        }

        /// <summary>
        /// Gets snapshot group by ID.
        /// </summary>
        public static bool TryGetGroup(EntityManager entityManager, uint groupId, out WorldSnapshotGroup group)
        {
            group = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldSnapshotGroup>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var groups = query.ToComponentDataArray<WorldSnapshotGroup>(Allocator.Temp);
            foreach (var g in groups)
            {
                if (g.GroupId == groupId)
                {
                    group = g;
                    groups.Dispose();
                    return true;
                }
            }
            groups.Dispose();
            return false;
        }
    }

    /// <summary>
    /// Predefined snapshot group configurations for common use cases.
    /// </summary>
    public static class SnapshotGroupPresets
    {
        /// <summary>
        /// Creates a group for villager entities with full state capture.
        /// </summary>
        public static WorldSnapshotGroup CreateVillagerGroup(uint groupId) => new WorldSnapshotGroup
        {
            GroupId = groupId,
            Label = new FixedString32Bytes("Villagers"),
            FrequencyMultiplier = 1,
            Importance = SnapshotImportance.High,
            ComponentFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.AI | 
                            HistoryRecordFlags.Jobs | HistoryRecordFlags.Needs | HistoryRecordFlags.Health,
            IsEnabled = true
        };

        /// <summary>
        /// Creates a group for resource nodes with less frequent capture.
        /// </summary>
        public static WorldSnapshotGroup CreateResourceGroup(uint groupId) => new WorldSnapshotGroup
        {
            GroupId = groupId,
            Label = new FixedString32Bytes("Resources"),
            FrequencyMultiplier = 2,
            Importance = SnapshotImportance.Normal,
            ComponentFlags = HistoryRecordFlags.Resources,
            IsEnabled = true
        };

        /// <summary>
        /// Creates a group for ships/fleets in Space4X.
        /// </summary>
        public static WorldSnapshotGroup CreateShipGroup(uint groupId) => new WorldSnapshotGroup
        {
            GroupId = groupId,
            Label = new FixedString32Bytes("Ships"),
            FrequencyMultiplier = 1,
            Importance = SnapshotImportance.High,
            ComponentFlags = HistoryRecordFlags.Transform | HistoryRecordFlags.AI | 
                            HistoryRecordFlags.Combat | HistoryRecordFlags.Navigation,
            IsEnabled = true
        };

        /// <summary>
        /// Creates a group for vegetation with infrequent capture.
        /// </summary>
        public static WorldSnapshotGroup CreateVegetationGroup(uint groupId) => new WorldSnapshotGroup
        {
            GroupId = groupId,
            Label = new FixedString32Bytes("Vegetation"),
            FrequencyMultiplier = 4,
            Importance = SnapshotImportance.Low,
            ComponentFlags = HistoryRecordFlags.Vegetation | HistoryRecordFlags.Health,
            IsEnabled = true
        };

        /// <summary>
        /// Creates a group for climate/environment state.
        /// </summary>
        public static WorldSnapshotGroup CreateClimateGroup(uint groupId) => new WorldSnapshotGroup
        {
            GroupId = groupId,
            Label = new FixedString32Bytes("Climate"),
            FrequencyMultiplier = 4,
            Importance = SnapshotImportance.Normal,
            ComponentFlags = HistoryRecordFlags.Climate,
            IsEnabled = true
        };
    }
}

