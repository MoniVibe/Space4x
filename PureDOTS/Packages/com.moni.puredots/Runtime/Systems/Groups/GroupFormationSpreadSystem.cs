using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Computes lightweight cohesion/spread metric for formations.
    /// This is intentionally cheap: group-local, no spatial queries, no LOS, no allocations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GroupDecisionSystemGroup))]
    public partial struct GroupFormationSpreadSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GroupFormationSpread>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);

            foreach (var (formation, groupXform, members, spread) in SystemAPI.Query<
                         RefRO<GroupFormation>,
                         RefRO<LocalTransform>,
                         DynamicBuffer<GroupMember>,
                         RefRW<GroupFormationSpread>>())
            {
                if (members.Length == 0)
                {
                    spread.ValueRW.CohesionNormalized = 0f;
                    continue;
                }

                var groupPos = groupXform.ValueRO.Position;
                var sumDist = 0f;
                var sampleCount = 0;
                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if ((member.Flags & GroupMemberFlags.Active) == 0)
                    {
                        continue;
                    }

                    var memberEntity = member.MemberEntity;
                    if (!_transformLookup.HasComponent(memberEntity))
                    {
                        continue;
                    }

                    var p = _transformLookup[memberEntity].Position;
                    sumDist += math.distance(groupPos, p);
                    sampleCount++;
                }

                if (sampleCount == 0)
                {
                    spread.ValueRW.CohesionNormalized = 0f;
                    continue;
                }

                var avgDist = sumDist / sampleCount;
                var spacing = math.max(0.25f, formation.ValueRO.Spacing);
                var expected = spacing * math.sqrt(sampleCount);
                var denom = math.max(0.25f, expected * 1.5f);
                spread.ValueRW.CohesionNormalized = math.saturate(1f - (avgDist / denom));
            }
        }
    }
}





