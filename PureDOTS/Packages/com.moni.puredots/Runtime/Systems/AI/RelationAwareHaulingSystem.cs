using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using PureDOTS.Systems.Relations;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// System that makes entities haul together on good relations or avoid each other on bad relations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct RelationAwareHaulingSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;
        private ComponentLookup<EntityIntent> _intentLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<EntityRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
            _intentLookup = state.GetComponentLookup<EntityIntent>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _relationLookup = state.GetBufferLookup<EntityRelation>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            _entityInfoLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _relationLookup.Update(ref state);

            const float partnerRadiusSq = 25f; // 5 units
            const float avoidRadiusSq = 100f;  // 10 units
            const float fleeDistance = 8f;

            foreach (var (intent, entity) in SystemAPI.Query<RefRW<EntityIntent>>().WithEntityAccess())
            {
                if (intent.ValueRO.Mode != IntentMode.Gather ||
                    intent.ValueRO.Priority > InterruptPriority.Normal ||
                    !_relationLookup.HasBuffer(entity) ||
                    !_transformLookup.HasComponent(entity))
                {
                    continue;
                }

                var relations = _relationLookup[entity];
                if (relations.Length == 0)
                {
                    continue;
                }

                var transform = _transformLookup[entity];
                var currentIntent = intent.ValueRO;
                bool adjusted = false;

                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    if (relation.OtherEntity == Entity.Null ||
                        !_entityInfoLookup.Exists(relation.OtherEntity) ||
                        !_intentLookup.HasComponent(relation.OtherEntity) ||
                        !_transformLookup.HasComponent(relation.OtherEntity))
                    {
                        continue;
                    }

                    var otherIntent = _intentLookup[relation.OtherEntity];
                    if (otherIntent.Mode != IntentMode.Gather)
                    {
                        continue;
                    }

                    var otherTransform = _transformLookup[relation.OtherEntity];
                    float distanceSq = math.distancesq(transform.Position, otherTransform.Position);

                    if (relation.Intensity >= 50 && distanceSq <= partnerRadiusSq)
                    {
                        currentIntent.Mode = IntentMode.Follow;
                        currentIntent.TargetEntity = relation.OtherEntity;
                        currentIntent.TargetPosition = otherTransform.Position;
                        currentIntent.TriggeringInterrupt = InterruptType.GroupFormed;
                        currentIntent.Priority = InterruptPriority.Normal;
                        currentIntent.IntentSetTick = currentTick;
                        currentIntent.IsValid = 1;
                        intent.ValueRW = currentIntent;
                        adjusted = true;
                        break;
                    }

                    if (relation.Intensity <= -60 && distanceSq <= avoidRadiusSq)
                    {
                        float3 fleeDirection = math.normalizesafe(transform.Position - otherTransform.Position, new float3(0f, 1f, 0f));
                        float3 fleeTarget = transform.Position + fleeDirection * fleeDistance;

                        currentIntent.Mode = IntentMode.MoveTo;
                        currentIntent.TargetEntity = Entity.Null;
                        currentIntent.TargetPosition = fleeTarget;
                        currentIntent.TriggeringInterrupt = InterruptType.NewThreatDetected;
                        currentIntent.Priority = InterruptPriority.High;
                        currentIntent.IntentSetTick = currentTick;
                        currentIntent.IsValid = 1;
                        intent.ValueRW = currentIntent;
                        adjusted = true;
                        break;
                    }
                }

                if (!adjusted && currentIntent.Mode != intent.ValueRO.Mode)
                {
                    intent.ValueRW = currentIntent;
                }
            }
        }
    }
}

