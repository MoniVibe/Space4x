#if TRI_ENABLE_INTERGROUP_RELATIONS
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// Applies event deltas to organization relations.
    /// Updates Attitude/Trust/Fear/Respect/Dependence and toggles treaties based on event outcomes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrgRelationInitSystem))]
    public partial struct OrgRelationEventImpactSystem : ISystem
    {
        private EntityQuery _orgRelationQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _orgRelationQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<OrgRelation>(),
                ComponentType.ReadOnly<OrgRelationTag>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            var currentTick = timeState.Tick;

            foreach (var (relationEvent, entity) in SystemAPI.Query<RefRO<OrgRelationEvent>>()
                .WithEntityAccess())
            {
                Entity? relationEntity = FindRelationEntity(ref state, relationEvent.ValueRO.SourceOrg, relationEvent.ValueRO.TargetOrg);
                
                if (!relationEntity.HasValue)
                {
                    var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
                    relationEntity = CreateRelationEntity(ref state, ecbSingleton, relationEvent.ValueRO.SourceOrg, relationEvent.ValueRO.TargetOrg, currentTick);
                }

                if (relationEntity.HasValue && SystemAPI.HasComponent<OrgRelation>(relationEntity.Value))
                {
                    var relation = SystemAPI.GetComponentRW<OrgRelation>(relationEntity.Value);

                    ApplyEventDeltas(ref relation, relationEvent.ValueRO, ref state);

                    relation.ValueRW.Kind = DetermineRelationKind(relation.ValueRO.Attitude);

                    relation.ValueRW.LastUpdateTick = currentTick;
                }

                state.EntityManager.RemoveComponent<OrgRelationEvent>(entity);
            }
        }

        private static void ApplyEventDeltas(ref RefRW<OrgRelation> relation, OrgRelationEvent evt, ref SystemState state)
        {
            float personaModifier = 1f;
            var personaLookup = state.GetComponentLookup<OrgPersona>(true);
            personaLookup.Update(ref state);
            if (personaLookup.HasComponent(evt.SourceOrg))
            {
                var persona = personaLookup[evt.SourceOrg];
                
                if (evt.AttitudeDelta < 0f)
                {
                    personaModifier = 0.5f + persona.VengefulForgiving * 0.5f;
                }
                else
                {
                    personaModifier = 1f - persona.VengefulForgiving * 0.3f;
                }
            }

            relation.ValueRW.Attitude = math.clamp(
                relation.ValueRO.Attitude + evt.AttitudeDelta * personaModifier, 
                -100f, 100f);

            relation.ValueRW.Trust = math.clamp(
                relation.ValueRO.Trust + evt.TrustDelta, 
                0f, 1f);

            relation.ValueRW.Fear = math.clamp(
                relation.ValueRO.Fear + evt.FearDelta, 
                0f, 1f);

            relation.ValueRW.Respect = math.clamp(
                relation.ValueRO.Respect + evt.RespectDelta, 
                0f, 1f);

            relation.ValueRW.Dependence = math.clamp(
                relation.ValueRO.Dependence + evt.DependenceDelta, 
                0f, 1f);

            relation.ValueRW.Treaties |= evt.TreatyFlagsToAdd;
            relation.ValueRW.Treaties &= ~evt.TreatyFlagsToRemove;
        }

        private Entity? FindRelationEntity(ref SystemState state, Entity orgA, Entity orgB)
        {
            var relations = _orgRelationQuery.ToComponentDataArray<OrgRelation>(Allocator.Temp);
            var entities = _orgRelationQuery.ToEntityArray(Allocator.Temp);
            
            for (int i = 0; i < relations.Length; i++)
            {
                var relation = relations[i];
                if ((relation.OrgA == orgA && relation.OrgB == orgB) ||
                    (relation.OrgA == orgB && relation.OrgB == orgA))
                {
                    var result = entities[i];
                    relations.Dispose();
                    entities.Dispose();
                    return result;
                }
            }
            
            relations.Dispose();
            entities.Dispose();
            return null;
        }

        private static Entity CreateRelationEntity(ref SystemState state, EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton, Entity orgA, Entity orgB, uint currentTick)
        {
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var relationEntity = ecb.CreateEntity();
            ecb.AddComponent(relationEntity, new OrgRelationTag());

            ecb.AddComponent(relationEntity, new OrgRelation
            {
                OrgA = orgA,
                OrgB = orgB,
                Kind = OrgRelationKind.Neutral,
                Treaties = OrgTreatyFlags.None,
                Attitude = 0f,
                Trust = 0.5f,
                Fear = 0f,
                Respect = 0.5f,
                Dependence = 0f,
                EstablishedTick = currentTick,
                LastUpdateTick = currentTick
            });

            return relationEntity;
        }

        private static OrgRelationKind DetermineRelationKind(float attitude)
        {
            if (attitude >= 50f)
                return OrgRelationKind.Allied;
            if (attitude >= 25f)
                return OrgRelationKind.Friendly;
            if (attitude <= -50f)
                return OrgRelationKind.Hostile;
            if (attitude <= -25f)
                return OrgRelationKind.Rival;
            return OrgRelationKind.Neutral;
        }
    }
}
#else
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.IntergroupRelations
{
    // [TRI-STUB] Disabled in MVP baseline.
    [BurstCompile]
    public partial struct OrgRelationEventImpactSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
        }
    }
}
#endif
