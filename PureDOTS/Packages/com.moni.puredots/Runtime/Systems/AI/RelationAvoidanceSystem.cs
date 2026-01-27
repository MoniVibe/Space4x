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
    /// System that makes entities avoid enemies on bad relations (-50 to -100).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct RelationAvoidanceSystem : ISystem
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
            _intentLookup = state.GetComponentLookup<EntityIntent>(false);
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

            const float avoidRadiusSq = 144f; // 12 units
            const float fleeDistance = 10f;

            foreach (var (relations, entity) in SystemAPI.Query<DynamicBuffer<EntityRelation>>().WithEntityAccess())
            {
                if (relations.Length == 0 ||
                    !_intentLookup.HasComponent(entity) ||
                    !_transformLookup.HasComponent(entity))
                {
                    continue;
                }

                var transform = _transformLookup[entity];
                var intent = _intentLookup[entity];
                bool intentUpdated = false;

                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    if (relation.Intensity > -50 ||
                        relation.OtherEntity == Entity.Null ||
                        !_entityInfoLookup.Exists(relation.OtherEntity) ||
                        !_transformLookup.HasComponent(relation.OtherEntity))
                    {
                        continue;
                    }

                    var otherTransform = _transformLookup[relation.OtherEntity];
                    float distanceSq = math.distancesq(transform.Position, otherTransform.Position);
                    if (distanceSq > avoidRadiusSq)
                    {
                        continue;
                    }

                    float3 fleeDirection = math.normalizesafe(transform.Position - otherTransform.Position, new float3(0f, 1f, 0f));
                    float3 newTarget = transform.Position + fleeDirection * fleeDistance;

                    intent.Mode = IntentMode.MoveTo;
                    intent.TargetEntity = Entity.Null;
                    intent.TargetPosition = newTarget;
                    intent.TriggeringInterrupt = InterruptType.NewThreatDetected;
                    intent.Priority = InterruptPriority.High;
                    intent.IntentSetTick = currentTick;
                    intent.IsValid = 1;
                    _intentLookup[entity] = intent;
                    intentUpdated = true;
                    break;
                }

                if (intentUpdated)
                {
                    continue;
                }
            }
        }
    }
}

