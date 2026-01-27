using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Rendering
{
    /// <summary>
    /// Summary data for aggregate entities (fleets, villages, guilds).
    /// Used for rendering impostors when individual members are too far.
    /// Updated periodically by AggregateRenderSummarySystem.
    /// </summary>
    public struct AggregateRenderSummary : IComponentData
    {
        /// <summary>
        /// Number of members in aggregate.
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Average position of members (centroid).
        /// </summary>
        public float3 AveragePosition;

        /// <summary>
        /// Bounding sphere center.
        /// </summary>
        public float3 BoundsCenter;

        /// <summary>
        /// Bounding sphere radius.
        /// </summary>
        public float BoundsRadius;

        /// <summary>
        /// Total health of all members (for health bar display).
        /// </summary>
        public float TotalHealth;

        /// <summary>
        /// Average morale of members (for villages/bands).
        /// </summary>
        public float AverageMorale;

        /// <summary>
        /// Total strength/power of aggregate (for fleets/armies).
        /// </summary>
        public float TotalStrength;

        /// <summary>
        /// Dominant visual identifier (e.g., faction index, type icon).
        /// </summary>
        public byte DominantTypeIndex;

        /// <summary>
        /// Last aggregation update tick.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Tracks aggregate membership and provides summary data.
    /// Added to individual entities that belong to an aggregate.
    /// </summary>
    public struct AggregateMembership : IComponentData
    {
        /// <summary>
        /// Reference to aggregate entity (village, fleet, etc.).
        /// </summary>
        public Entity AggregateEntity;

        /// <summary>
        /// Index within aggregate (for stable sampling).
        /// </summary>
        public byte MemberIndex;

        /// <summary>
        /// Membership flags (active, leader, etc.).
        /// </summary>
        public byte Flags;

        public const byte FlagActive = 1 << 0;
        public const byte FlagLeader = 1 << 1;
        public const byte FlagVisible = 1 << 2;
    }

    /// <summary>
    /// Maintains aggregate summaries efficiently.
    /// Updated periodically, not every tick.
    /// </summary>
    public struct AggregateState : IComponentData
    {
        /// <summary>
        /// Number of members in aggregate.
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Average position of members.
        /// </summary>
        public float3 AveragePosition;

        /// <summary>
        /// Bounding box minimum.
        /// </summary>
        public float3 BoundsMin;

        /// <summary>
        /// Bounding box maximum.
        /// </summary>
        public float3 BoundsMax;

        /// <summary>
        /// Total health of all members.
        /// </summary>
        public float TotalHealth;

        /// <summary>
        /// Average morale of members.
        /// </summary>
        public float AverageMorale;

        /// <summary>
        /// Total strength/power of aggregate.
        /// </summary>
        public float TotalStrength;

        /// <summary>
        /// Last aggregation update tick.
        /// </summary>
        public uint LastAggregationTick;

        /// <summary>
        /// Ticks between updates (configurable per aggregate type).
        /// </summary>
        public uint AggregationInterval;
    }

    /// <summary>
    /// Configuration for aggregate rendering behavior.
    /// </summary>
    public struct AggregateRenderConfig : IComponentData
    {
        /// <summary>
        /// Distance at which to switch from individual to aggregate rendering.
        /// </summary>
        public float AggregateRenderDistance;

        /// <summary>
        /// Minimum members to show aggregate marker.
        /// </summary>
        public int MinMembersForMarker;

        /// <summary>
        /// Maximum members to render individually (beyond this, use density).
        /// </summary>
        public int MaxIndividualRender;

        /// <summary>
        /// Update interval in ticks.
        /// </summary>
        public uint UpdateInterval;
    }

    /// <summary>
    /// Tag indicating entity is an aggregate (village, fleet, guild, etc.).
    /// </summary>
    public struct AggregateTag : IComponentData { }

    /// <summary>
    /// Buffer element for tracking aggregate members.
    /// Stored on aggregate entity for efficient iteration.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct AggregateMemberElement : IBufferElementData
    {
        /// <summary>
        /// Member entity reference.
        /// </summary>
        public Entity MemberEntity;

        /// <summary>
        /// Member's contribution to aggregate strength.
        /// </summary>
        public float StrengthContribution;

        /// <summary>
        /// Member's current health.
        /// </summary>
        public float Health;
    }

    /// <summary>
    /// Helper methods for aggregate rendering calculations.
    /// </summary>
    public static class AggregateRenderHelpers
    {
        /// <summary>
        /// Calculates bounding sphere from bounds min/max.
        /// </summary>
        public static void CalculateBoundingSphere(
            float3 boundsMin, 
            float3 boundsMax, 
            out float3 center, 
            out float radius)
        {
            center = (boundsMin + boundsMax) * 0.5f;
            radius = math.length(boundsMax - center);
        }

        /// <summary>
        /// Determines if aggregate should render as impostor based on distance.
        /// </summary>
        public static bool ShouldRenderAsImpostor(
            float cameraDistance, 
            in AggregateRenderConfig config,
            int memberCount)
        {
            if (memberCount < config.MinMembersForMarker)
                return false;
            return cameraDistance > config.AggregateRenderDistance;
        }

        /// <summary>
        /// Calculates render density for aggregate members based on count.
        /// </summary>
        public static ushort CalculateMemberDensity(int memberCount, int maxIndividualRender)
        {
            if (maxIndividualRender <= 0 || memberCount <= maxIndividualRender)
                return 1; // Render all
            
            // Calculate density to stay under max
            return (ushort)math.max(1, (memberCount + maxIndividualRender - 1) / maxIndividualRender);
        }
    }
}

