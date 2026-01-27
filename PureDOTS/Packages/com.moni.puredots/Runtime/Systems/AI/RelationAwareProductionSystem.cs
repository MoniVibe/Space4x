using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Cooperation;
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
    /// System that makes builders and craftsmen work together based on relations.
    /// Good relations: Form production teams, work together
    /// Bad relations: Work separately, avoid collaboration
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    [UpdateBefore(typeof(AISystemGroup))]
    public partial struct RelationAwareProductionSystem : ISystem
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

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
            var processedPairs = new NativeParallelHashSet<ulong>(128, state.WorldUpdateAllocator);

            const float collaborateRadiusSq = 36f; // 6 units
            const float avoidRadiusSq = 81f;       // 9 units
            const float separationDistance = 6f;

            foreach (var (intent, entity) in SystemAPI.Query<RefRW<EntityIntent>>().WithEntityAccess())
            {
                if ((intent.ValueRO.Mode != IntentMode.Build && intent.ValueRO.Mode != IntentMode.Gather) ||
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
                    bool otherWorking = otherIntent.Mode == IntentMode.Build || otherIntent.Mode == IntentMode.Gather;
                    if (!otherWorking)
                    {
                        continue;
                    }

                    var otherTransform = _transformLookup[relation.OtherEntity];
                    float distanceSq = math.distancesq(transform.Position, otherTransform.Position);
                    ulong pairKey = GetPairKey(entity.Index, relation.OtherEntity.Index);

                    if (relation.Intensity >= 40 && distanceSq <= collaborateRadiusSq)
                    {
                        if (processedPairs.Add(pairKey))
                        {
                            EmitInteractionRequest(ref ecb, entity, relation.OtherEntity, InteractionOutcome.Positive, true);
                        }

                        currentIntent.TargetEntity = relation.OtherEntity;
                        currentIntent.TargetPosition = otherTransform.Position;
                        currentIntent.TriggeringInterrupt = InterruptType.GroupFormed;
                        currentIntent.Priority = (InterruptPriority)math.max((int)currentIntent.Priority, (int)InterruptPriority.Normal);
                        currentIntent.IsValid = 1;
                        currentIntent.IntentSetTick = currentTick;
                        intent.ValueRW = currentIntent;
                        break;
                    }

                    if (relation.Intensity <= -45 && distanceSq <= avoidRadiusSq)
                    {
                        if (processedPairs.Add(pairKey))
                        {
                            EmitInteractionRequest(ref ecb, entity, relation.OtherEntity, InteractionOutcome.Negative, false);
                        }

                        float3 separationDir = math.normalizesafe(transform.Position - otherTransform.Position, new float3(0f, 1f, 0f));
                        float3 newTarget = transform.Position + separationDir * separationDistance;

                        currentIntent.Mode = IntentMode.MoveTo;
                        currentIntent.TargetEntity = Entity.Null;
                        currentIntent.TargetPosition = newTarget;
                        currentIntent.TriggeringInterrupt = InterruptType.OrderCancelled;
                        currentIntent.Priority = InterruptPriority.High;
                        currentIntent.IntentSetTick = currentTick;
                        currentIntent.IsValid = 1;
                        intent.ValueRW = currentIntent;
                        break;
                    }
                }
            }
        }

        [BurstCompile]
        private static ulong GetPairKey(int aIndex, int bIndex)
        {
            ulong low = (ulong)math.min(aIndex, bIndex);
            ulong high = (ulong)math.max(aIndex, bIndex);
            return (high << 32) | low;
        }

        private static void EmitInteractionRequest(
            ref EntityCommandBuffer ecb,
            Entity entityA,
            Entity entityB,
            InteractionOutcome outcome,
            bool mutual)
        {
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new RecordInteractionRequest
            {
                EntityA = entityA,
                EntityB = entityB,
                Outcome = outcome,
                IntensityChange = 0,
                TrustChange = 0,
                IsMutual = mutual
            });
        }
    }
}

