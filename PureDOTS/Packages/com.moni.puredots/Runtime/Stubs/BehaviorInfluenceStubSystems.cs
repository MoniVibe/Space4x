using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Behavior
{
    /// <summary>
    /// Minimal behavior MVP: charges initiative and emits need requests as satisfaction decays.
    /// </summary>
    [BurstCompile]
    public partial struct BehaviorInfluenceSystem : ISystem
    {
        ComponentLookup<BehaviorModifier> _modifierLookup;
        ComponentLookup<NeedCategory> _needCategoryLookup;
        ComponentLookup<NeedSatisfaction> _needSatisfactionLookup;
        BufferLookup<NeedRequestElement> _needRequestLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _modifierLookup = state.GetComponentLookup<BehaviorModifier>(true);
            _needCategoryLookup = state.GetComponentLookup<NeedCategory>(true);
            _needSatisfactionLookup = state.GetComponentLookup<NeedSatisfaction>();
            _needRequestLookup = state.GetBufferLookup<NeedRequestElement>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            _modifierLookup.Update(ref state);
            _needCategoryLookup.Update(ref state);
            _needSatisfactionLookup.Update(ref state);
            _needRequestLookup.Update(ref state);

            var modifierLookup = _modifierLookup;
            var needCategoryLookup = _needCategoryLookup;
            var needRequestLookup = _needRequestLookup;

            foreach (var (initiative, entity) in SystemAPI.Query<RefRW<InitiativeStat>>().WithEntityAccess())
            {
                float modifier = 1f;
                if (modifierLookup.HasComponent(entity))
                {
                    modifier += math.max(0f, modifierLookup[entity].Value);
                }

                var stat = initiative.ValueRW;
                float target = math.max(0.01f, stat.Cooldown <= 0f ? 1f : stat.Cooldown);
                stat.Charge += deltaTime * modifier;

                if (stat.Charge >= target)
                {
                    stat.Charge -= target;
                    if (needCategoryLookup.HasComponent(entity) && needRequestLookup.HasBuffer(entity))
                    {
                        var buffer = needRequestLookup[entity];
                        var needType = needCategoryLookup[entity].Type;
                        buffer.Add(new NeedRequestElement
                        {
                            NeedType = needType,
                            Urgency = 1f
                        });
                    }
                }

                initiative.ValueRW = stat;
            }

            foreach (var (satisfaction, entity) in SystemAPI.Query<RefRW<NeedSatisfaction>>().WithEntityAccess())
            {
                if (!needCategoryLookup.HasComponent(entity) || !needRequestLookup.HasBuffer(entity))
                    continue;

                var sat = satisfaction.ValueRW;
                sat.Value = math.max(0f, sat.Value - deltaTime * 0.1f);

                if (sat.Value <= 0f)
                {
                    var buffer = needRequestLookup[entity];
                    buffer.Add(new NeedRequestElement
                    {
                        NeedType = needCategoryLookup[entity].Type,
                        Urgency = 1f
                    });
                    sat.Value = 1f;
                }

                satisfaction.ValueRW = sat;
            }
        }
    }
}
