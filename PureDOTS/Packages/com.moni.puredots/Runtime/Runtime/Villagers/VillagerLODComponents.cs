using PureDOTS.Runtime.Rendering;
using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Reference to the village aggregate this villager belongs to.
    /// Used for aggregate rendering and village-level statistics.
    /// </summary>
    public struct VillagerVillageRef : IComponentData
    {
        /// <summary>
        /// Entity of the village aggregate this villager belongs to.
        /// Entity.Null if not assigned to a village.
        /// </summary>
        public Entity VillageEntity;

        /// <summary>
        /// Index within the village (for stable sampling).
        /// </summary>
        public ushort VillagerIndex;

        /// <summary>
        /// Flags for village membership status.
        /// </summary>
        public byte Flags;
    }

    /// <summary>
    /// Flags for VillagerVillageRef.
    /// </summary>
    public static class VillagerVillageFlags
    {
        public const byte None = 0;
        public const byte IsActive = 1 << 0;
        public const byte IsLeader = 1 << 1;
        public const byte IsElder = 1 << 2;
        public const byte IsNewcomer = 1 << 3;
    }

    /// <summary>
    /// Static helper methods for adding LOD components to villagers.
    /// </summary>
    public static class VillagerLODHelpers
    {
        /// <summary>
        /// Adds LOD components to a villager entity.
        /// </summary>
        public static void AddLODComponents(EntityManager entityManager, Entity entity, float cullDistance = 200f)
        {
            // Add render LOD data
            entityManager.AddComponentData(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.5f, // Default importance
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            // Add cullable marker
            entityManager.AddComponentData(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 128 // Medium priority
            });

            // Add sample index for density control
            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            entityManager.AddComponentData(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds LOD components to a villager entity using ECB.
        /// </summary>
        public static void AddLODComponents(EntityCommandBuffer ecb, Entity entity, int entityIndex, float cullDistance = 200f)
        {
            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.5f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 128
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entityIndex, 100);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds village membership to a villager.
        /// </summary>
        public static void AddVillageMembership(
            EntityManager entityManager,
            Entity villagerEntity,
            Entity villageEntity,
            ushort villagerIndex)
        {
            entityManager.AddComponentData(villagerEntity, new VillagerVillageRef
            {
                VillageEntity = villageEntity,
                VillagerIndex = villagerIndex,
                Flags = VillagerVillageFlags.IsActive
            });

            // Also add aggregate membership for rendering
            entityManager.AddComponentData(villagerEntity, new AggregateMembership
            {
                AggregateEntity = villageEntity,
                MemberIndex = (byte)(villagerIndex % 256),
                Flags = AggregateMembership.FlagActive
            });
        }
    }
}

