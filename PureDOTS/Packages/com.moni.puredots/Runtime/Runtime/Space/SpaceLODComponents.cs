using PureDOTS.Runtime.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Reference to the fleet aggregate this carrier/craft belongs to.
    /// Used for aggregate rendering and fleet-level statistics.
    /// </summary>
    public struct FleetMemberRef : IComponentData
    {
        /// <summary>
        /// Entity of the fleet aggregate this carrier belongs to.
        /// Entity.Null if not assigned to a fleet.
        /// </summary>
        public Entity FleetEntity;

        /// <summary>
        /// Index within the fleet (for stable sampling).
        /// </summary>
        public ushort MemberIndex;

        /// <summary>
        /// Flags for fleet membership status.
        /// </summary>
        public byte Flags;
    }

    /// <summary>
    /// Flags for FleetMemberRef.
    /// </summary>
    public static class FleetMemberFlags
    {
        public const byte None = 0;
        public const byte IsActive = 1 << 0;
        public const byte IsFlagship = 1 << 1;
        public const byte IsEscort = 1 << 2;
        public const byte IsSupport = 1 << 3;
    }

    /// <summary>
    /// Fleet aggregate entity marker.
    /// </summary>
    public struct FleetTag : IComponentData { }

    /// <summary>
    /// Fleet aggregate state for Space4X.
    /// </summary>
    public struct FleetState : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsMin;
        public float3 BoundsMax;
        public float TotalStrength;
        public float TotalHealth;
        public float TotalCargoCapacity;
        public uint LastUpdateTick;
        public uint UpdateInterval;
    }

    /// <summary>
    /// Fleet render summary for impostor rendering.
    /// </summary>
    public struct FleetRenderSummary : IComponentData
    {
        public int MemberCount;
        public float3 AveragePosition;
        public float3 BoundsCenter;
        public float BoundsRadius;
        public float TotalStrength;
        public float TotalHealth;
        public byte DominantShipType;
        public byte FactionIndex;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Helper methods for adding LOD components to Space4X entities.
    /// </summary>
    public static class SpaceLODHelpers
    {
        /// <summary>
        /// Adds LOD components to a carrier/craft entity.
        /// </summary>
        public static void AddLODComponents(EntityManager entityManager, Entity entity, float cullDistance = 300f, float importance = 0.6f)
        {
            entityManager.AddComponentData(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = importance,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 160 // Higher priority than ground units
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            entityManager.AddComponentData(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds LOD components to a carrier/craft entity using ECB.
        /// </summary>
        public static void AddLODComponents(EntityCommandBuffer ecb, Entity entity, int entityIndex, float cullDistance = 300f, float importance = 0.6f)
        {
            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = importance,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 160
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
        /// Adds fleet membership to a carrier/craft.
        /// </summary>
        public static void AddFleetMembership(
            EntityManager entityManager,
            Entity memberEntity,
            Entity fleetEntity,
            ushort memberIndex,
            byte flags = FleetMemberFlags.IsActive)
        {
            entityManager.AddComponentData(memberEntity, new FleetMemberRef
            {
                FleetEntity = fleetEntity,
                MemberIndex = memberIndex,
                Flags = flags
            });

            // Also add aggregate membership for rendering
            entityManager.AddComponentData(memberEntity, new AggregateMembership
            {
                AggregateEntity = fleetEntity,
                MemberIndex = (byte)(memberIndex % 256),
                Flags = AggregateMembership.FlagActive
            });
        }

        /// <summary>
        /// Creates a fleet aggregate entity.
        /// </summary>
        public static Entity CreateFleetAggregate(
            EntityManager entityManager,
            float3 position,
            int expectedMemberCount = 10)
        {
            var fleetEntity = entityManager.CreateEntity();

            entityManager.AddComponent<FleetTag>(fleetEntity);
            entityManager.AddComponent<AggregateTag>(fleetEntity);

            entityManager.AddComponentData(fleetEntity, new FleetState
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsMin = position - 100f,
                BoundsMax = position + 100f,
                TotalStrength = 0f,
                TotalHealth = 0f,
                TotalCargoCapacity = 0f,
                LastUpdateTick = 0,
                UpdateInterval = 30
            });

            entityManager.AddComponentData(fleetEntity, new FleetRenderSummary
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsCenter = position,
                BoundsRadius = 100f,
                TotalStrength = 0f,
                TotalHealth = 0f,
                DominantShipType = 0,
                FactionIndex = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(fleetEntity, new AggregateState
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsMin = position - 100f,
                BoundsMax = position + 100f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                LastAggregationTick = 0,
                AggregationInterval = 30
            });

            entityManager.AddComponentData(fleetEntity, new AggregateRenderSummary
            {
                MemberCount = 0,
                AveragePosition = position,
                BoundsCenter = position,
                BoundsRadius = 100f,
                TotalHealth = 0f,
                AverageMorale = 0f,
                TotalStrength = 0f,
                DominantTypeIndex = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(fleetEntity, new AggregateRenderConfig
            {
                AggregateRenderDistance = 500f,
                MinMembersForMarker = 3,
                MaxIndividualRender = 20,
                UpdateInterval = 30
            });

            entityManager.AddBuffer<AggregateMemberElement>(fleetEntity);

            // Add LOD for the fleet itself
            AddLODComponents(entityManager, fleetEntity, 1000f, 0.8f);

            return fleetEntity;
        }

        /// <summary>
        /// Adds LOD components to an asteroid entity.
        /// </summary>
        public static void AddAsteroidLODComponents(EntityManager entityManager, Entity entity, float cullDistance = 500f)
        {
            entityManager.AddComponentData(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.2f, // Low importance
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            entityManager.AddComponentData(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 32 // Low priority
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            entityManager.AddComponentData(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }
    }
}

