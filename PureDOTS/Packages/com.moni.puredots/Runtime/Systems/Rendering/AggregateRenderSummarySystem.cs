using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Systems.Rendering
{
    /// <summary>
    /// Updates aggregate render summaries for LOD and impostor rendering.
    /// Runs in Unity.Entities.PresentationSystemGroup (non-deterministic, frame-time).
    /// Updates aggregates periodically based on AggregationInterval.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct AggregateRenderSummarySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggregateTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            // Update aggregate summaries
            foreach (var (aggregateState, renderSummary, members, entity) in 
                SystemAPI.Query<RefRW<AggregateState>, RefRW<AggregateRenderSummary>, DynamicBuffer<AggregateMemberElement>>()
                    .WithAll<AggregateTag>()
                    .WithEntityAccess())
            {
                // Check if update is needed based on interval
                if (aggregateState.ValueRO.AggregationInterval > 0 &&
                    tick - aggregateState.ValueRO.LastAggregationTick < aggregateState.ValueRO.AggregationInterval)
                {
                    continue;
                }

                // Calculate aggregate statistics
                var memberCount = members.Length;
                if (memberCount == 0)
                {
                    aggregateState.ValueRW.MemberCount = 0;
                    renderSummary.ValueRW.MemberCount = 0;
                    continue;
                }

                var sumPosition = float3.zero;
                var boundsMin = new float3(float.MaxValue);
                var boundsMax = new float3(float.MinValue);
                var totalHealth = 0f;
                var totalStrength = 0f;

                for (int i = 0; i < memberCount; i++)
                {
                    var member = members[i];
                    
                    // Get member position if available
                    if (SystemAPI.HasComponent<LocalTransform>(member.MemberEntity))
                    {
                        var transform = SystemAPI.GetComponent<LocalTransform>(member.MemberEntity);
                        sumPosition += transform.Position;
                        boundsMin = math.min(boundsMin, transform.Position);
                        boundsMax = math.max(boundsMax, transform.Position);
                    }

                    totalHealth += member.Health;
                    totalStrength += member.StrengthContribution;
                }

                var avgPosition = sumPosition / memberCount;

                // Update aggregate state
                aggregateState.ValueRW.MemberCount = memberCount;
                aggregateState.ValueRW.AveragePosition = avgPosition;
                aggregateState.ValueRW.BoundsMin = boundsMin;
                aggregateState.ValueRW.BoundsMax = boundsMax;
                aggregateState.ValueRW.TotalHealth = totalHealth;
                aggregateState.ValueRW.TotalStrength = totalStrength;
                aggregateState.ValueRW.LastAggregationTick = tick;

                // Update render summary
                AggregateRenderHelpers.CalculateBoundingSphere(
                    boundsMin, boundsMax,
                    out var boundsCenter, out var boundsRadius);

                renderSummary.ValueRW.MemberCount = memberCount;
                renderSummary.ValueRW.AveragePosition = avgPosition;
                renderSummary.ValueRW.BoundsCenter = boundsCenter;
                renderSummary.ValueRW.BoundsRadius = boundsRadius;
                renderSummary.ValueRW.TotalHealth = totalHealth;
                renderSummary.ValueRW.TotalStrength = totalStrength;
                renderSummary.ValueRW.LastUpdateTick = tick;
            }
        }
    }

    /// <summary>
    /// System group for rendering-related systems.
    /// Runs in Unity.Entities.PresentationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial class RenderingSystemGroup : ComponentSystemGroup { }
}

