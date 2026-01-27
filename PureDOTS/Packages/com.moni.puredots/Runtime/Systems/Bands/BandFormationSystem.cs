using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains derived formation data so both games can reason about band footprint and facing deterministically.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(BandAggregationSystem))]
    public partial struct BandFormationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BandFormation>();
            state.RequireForUpdate<BandStats>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();

            foreach (var (formation, stats, transform) in SystemAPI
                         .Query<RefRW<BandFormation>, RefRO<BandStats>, RefRO<LocalTransform>>())
            {
                var statsValue = stats.ValueRO;
                var formationValue = formation.ValueRO;

                var desiredFormation = formationValue.Formation;
                var flags = statsValue.Flags;

                if ((flags & BandStatusFlags.Engaged) != 0)
                {
                    desiredFormation = BandFormationType.Line;
                }
                else if ((flags & (BandStatusFlags.Moving | BandStatusFlags.Routing)) != 0)
                {
                    desiredFormation = BandFormationType.Column;
                }
                else if ((flags & BandStatusFlags.Resting) != 0)
                {
                    desiredFormation = BandFormationType.Circle;
                }

                var baseSpacing = formationValue.Spacing > 0f ? formationValue.Spacing : 1.5f;
                var cohesionRatio = math.saturate(statsValue.Cohesion / 100f);
                var spacing = math.clamp(baseSpacing * math.lerp(1.35f, 0.85f, cohesionRatio), 0.75f, 3.5f);

                var memberCount = math.max(1, statsValue.MemberCount);
                var columnCount = math.clamp((int)math.round(math.sqrt(memberCount)), 1, 16);
                var rowCount = math.max(1, (int)math.ceil((float)memberCount / columnCount));

                if (desiredFormation == BandFormationType.Line)
                {
                    rowCount = 1;
                    columnCount = memberCount;
                }
                else if (desiredFormation == BandFormationType.Column)
                {
                    columnCount = math.max(1, memberCount / math.max(1, rowCount));
                }

                var width = math.max(spacing, columnCount * spacing);
                var depth = math.max(spacing, rowCount * spacing);

                var rotation = transform.ValueRO.Rotation;
                var forward = math.mul(rotation, new float3(0f, 0f, 1f));
                forward.y = 0f;
                forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));

                var stability = math.saturate((statsValue.Cohesion + statsValue.AverageDiscipline) * 0.5f / 100f);

                formation.ValueRW = new BandFormation
                {
                    Formation = desiredFormation,
                    Spacing = spacing,
                    Width = width,
                    Depth = depth,
                    Facing = forward,
                    Anchor = transform.ValueRO.Position,
                    Stability = stability,
                    LastSolveTick = time.Tick
                };
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
