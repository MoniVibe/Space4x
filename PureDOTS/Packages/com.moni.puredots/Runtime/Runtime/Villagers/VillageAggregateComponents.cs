using PureDOTS.Runtime.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Village aggregate entity marker.
    /// </summary>
    public struct VillageTag : IComponentData { }

    /// <summary>
    /// Village aggregate state for Godgame.
    /// </summary>
    public struct VillageState : IComponentData
    {
        public int PopulationCount;
        public float3 CenterPosition;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float TotalFood;
        public float TotalWealth;
        public float AverageMorale;
        public float AverageFaith;
        public byte DominantDeityIndex;
        public uint LastUpdateTick;
        public uint UpdateInterval;
    }

    /// <summary>
    /// Village render summary for impostor rendering.
    /// </summary>
    public struct VillageRenderSummary : IComponentData
    {
        public int PopulationCount;
        public float3 CenterPosition;
        public float3 BoundsCenter;
        public float BoundsRadius;
        public float TotalWealth;
        public float AverageMorale;
        public float AverageFaith;
        public byte DominantBuildingType;
        public byte FactionIndex;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Helper methods for creating and managing village aggregates.
    /// </summary>
    public static class VillageAggregateHelpers
    {
        /// <summary>
        /// Creates a village aggregate entity.
        /// </summary>
        public static Entity CreateVillageAggregate(
            EntityManager entityManager,
            float3 position,
            int expectedPopulation = 20)
        {
            var villageEntity = entityManager.CreateEntity();

            entityManager.AddComponent<VillageTag>(villageEntity);
            entityManager.AddComponent<AggregateTag>(villageEntity);

            entityManager.AddComponentData(villageEntity, new VillageState
            {
                PopulationCount = 0,
                CenterPosition = position,
                BoundsMin = position - 50f,
                BoundsMax = position + 50f,
                TotalFood = 0f,
                TotalWealth = 0f,
                AverageMorale = 0f,
                AverageFaith = 0f,
                DominantDeityIndex = 0,
                LastUpdateTick = 0,
                UpdateInterval = 60
            });

            entityManager.AddComponentData(villageEntity, new VillageRenderSummary
            {
                PopulationCount = 0,
                CenterPosition = position,
                BoundsCenter = position,
                BoundsRadius = 50f,
                TotalWealth = 0f,
                AverageMorale = 0f,
                AverageFaith = 0f,
                DominantBuildingType = 0,
                FactionIndex = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(villageEntity, new AggregateState
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsMin = position - 50f,
                BoundsMax = position + 50f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                LastAggregationTick = 0,
                AggregationInterval = 60
            });

            entityManager.AddComponentData(villageEntity, new AggregateRenderSummary
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsCenter = position,
                BoundsRadius = 50f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                DominantTypeIndex = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(villageEntity, new AggregateRenderConfig
            {
                AggregateRenderDistance = 200f,
                MinMembersForMarker = 5,
                MaxIndividualRender = 100,
                UpdateInterval = 60
            });

            entityManager.AddBuffer<AggregateMemberElement>(villageEntity);

            // Add LOD for the village itself
            entityManager.AddComponentData(villageEntity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.9f, // High importance
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(villageEntity, new RenderCullable
            {
                CullDistance = 500f,
                Priority = 200 // High priority
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(villageEntity.Index, 100);
            entityManager.AddComponentData(villageEntity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });

            return villageEntity;
        }

        /// <summary>
        /// Adds a villager to a village aggregate.
        /// </summary>
        public static void AddVillagerToVillage(
            EntityManager entityManager,
            Entity villagerEntity,
            Entity villageEntity,
            ushort villagerIndex)
        {
            // Add village reference
            entityManager.AddComponentData(villagerEntity, new VillagerVillageRef
            {
                VillageEntity = villageEntity,
                VillagerIndex = villagerIndex,
                Flags = VillagerVillageFlags.IsActive
            });

            // Add aggregate membership
            entityManager.AddComponentData(villagerEntity, new AggregateMembership
            {
                AggregateEntity = villageEntity,
                MemberIndex = (byte)(villagerIndex % 256),
                Flags = AggregateMembership.FlagActive
            });

            // Add to village member buffer
            if (entityManager.HasBuffer<AggregateMemberElement>(villageEntity))
            {
                var buffer = entityManager.GetBuffer<AggregateMemberElement>(villageEntity);
                buffer.Add(new AggregateMemberElement
                {
                    MemberEntity = villagerEntity,
                    StrengthContribution = 1f,
                    Health = 100f
                });
            }
        }
    }
}

