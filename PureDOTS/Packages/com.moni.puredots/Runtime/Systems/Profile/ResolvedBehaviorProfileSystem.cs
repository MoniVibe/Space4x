using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Profile
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTuningSystem))]
    public partial struct ResolvedBehaviorProfileSystem : ISystem
    {
        private static readonly FixedString32Bytes AxisLawfulChaotic = new FixedString32Bytes("LawfulChaotic");
        private EntityQuery _missingResolvedQuery;
        private ComponentLookup<PersonalityAxes> _personalityLookup;
        private ComponentLookup<BehaviorTuning> _tuningLookup;
        private ComponentLookup<BehaviorDisposition> _dispositionLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                state.Enabled = false;
                return;
            }

            _missingResolvedQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<TraitAxisValue>(),
                    ComponentType.ReadOnly<PersonalityAxes>(),
                    ComponentType.ReadOnly<BehaviorTuning>(),
                    ComponentType.ReadOnly<BehaviorDisposition>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<ResolvedBehaviorProfile>()
                }
            });

            _personalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _tuningLookup = state.GetComponentLookup<BehaviorTuning>(true);
            _dispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _personalityLookup.Update(ref state);
            _tuningLookup.Update(ref state);
            _dispositionLookup.Update(ref state);

            if (!_missingResolvedQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _missingResolvedQuery.ToEntityArray(Allocator.Temp);
                var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var resolved = ResolveProfile(ref state, entity);
                    ecb.AddComponent(entity, resolved);
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            foreach (var (axes, resolved, entity) in SystemAPI
                         .Query<DynamicBuffer<TraitAxisValue>, RefRW<ResolvedBehaviorProfile>>()
                         .WithChangeFilter<TraitAxisValue>()
                         .WithEntityAccess())
            {
                resolved.ValueRW = ResolveProfile(entity, axes);
            }

            foreach (var (_, resolved, entity) in SystemAPI
                         .Query<RefRO<PersonalityAxes>, RefRW<ResolvedBehaviorProfile>>()
                         .WithChangeFilter<PersonalityAxes>()
                         .WithEntityAccess())
            {
                resolved.ValueRW = ResolveProfile(ref state, entity);
            }

            foreach (var (_, resolved, entity) in SystemAPI
                         .Query<RefRO<BehaviorTuning>, RefRW<ResolvedBehaviorProfile>>()
                         .WithChangeFilter<BehaviorTuning>()
                         .WithEntityAccess())
            {
                resolved.ValueRW = ResolveProfile(ref state, entity);
            }

            foreach (var (_, resolved, entity) in SystemAPI
                         .Query<RefRO<BehaviorDisposition>, RefRW<ResolvedBehaviorProfile>>()
                         .WithChangeFilter<BehaviorDisposition>()
                         .WithEntityAccess())
            {
                resolved.ValueRW = ResolveProfile(ref state, entity);
            }
        }

        private ResolvedBehaviorProfile ResolveProfile(ref SystemState state, Entity entity)
        {
            if (state.EntityManager.HasBuffer<TraitAxisValue>(entity))
            {
                var buffer = state.EntityManager.GetBuffer<TraitAxisValue>(entity, true);
                return ResolveProfile(entity, buffer);
            }

            return ResolveProfile(entity, default);
        }

        private ResolvedBehaviorProfile ResolveProfile(Entity entity, DynamicBuffer<TraitAxisValue> traitBuffer)
        {
            var resolved = ResolvedBehaviorProfile.Neutral;
            var hasLawfulAxis = false;
            if (traitBuffer.IsCreated && traitBuffer.Length > 0 &&
                TraitAxisLookup.TryGetValue(entity, AxisLawfulChaotic, out var axisValue, traitBuffer))
            {
                var normalized = NormalizeAxisValue(axisValue);
                resolved.Chaos01 = math.saturate((1f - normalized) * 0.5f);
                hasLawfulAxis = true;
            }

            resolved.Risk01 = ResolveRisk01(entity);
            resolved.Obedience01 = ResolveObedience01(entity);

            if (!hasLawfulAxis)
            {
                resolved.Chaos01 = math.saturate(0.5f * (resolved.Risk01 + (1f - resolved.Obedience01)));
            }

            return resolved.Sanitized();
        }

        private float ResolveRisk01(Entity entity)
        {
            if (_personalityLookup.HasComponent(entity))
            {
                var personality = _personalityLookup[entity];
                return math.saturate((personality.RiskTolerance + 1f) * 0.5f);
            }

            if (_dispositionLookup.HasComponent(entity))
            {
                return math.saturate(_dispositionLookup[entity].RiskTolerance);
            }

            return 0.5f;
        }

        private float ResolveObedience01(Entity entity)
        {
            if (_tuningLookup.HasComponent(entity))
            {
                var tuning = _tuningLookup[entity];
                return math.saturate(tuning.ObedienceBias * 0.5f);
            }

            if (_dispositionLookup.HasComponent(entity))
            {
                return math.saturate(_dispositionLookup[entity].Compliance);
            }

            return 0.5f;
        }

        private static float NormalizeAxisValue(float value)
        {
            if (math.abs(value) > 1.5f)
            {
                value /= 100f;
            }

            return math.clamp(value, -1f, 1f);
        }
    }
}
