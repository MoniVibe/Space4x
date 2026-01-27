#if PUREDOTS_SCENARIO

using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Village;
using PureDOTS.Runtime.Platform;
using PureDOTS.Systems.Bootstrap;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.IntergroupRelations
{
    /// <summary>
    /// Initializes organization relations between neighboring villages and carriers.
    /// Sets initial Attitude based on alignment/outlook compatibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ScenarioRunnerSystem))]
    public partial struct OrgRelationInitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var scenarioState = SystemAPI.GetSingleton<ScenarioState>();
            if (!scenarioState.IsInitialized)
            {
                return;
            }

            // Only run once
            if (SystemAPI.HasSingleton<OrgRelationInitCompleteTag>())
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Create orgs for villages (Godgame)
            if (scenarioState.EnableGodgame)
            {
                var villageQuery = SystemAPI.QueryBuilder()
                    .WithAll<VillageTag, LocalTransform, OwnerOrg>()
                    .Build();
                var villages = villageQuery.ToEntityArray(Allocator.Temp);
                var villageTransforms = villageQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                var villageOwnerOrgs = villageQuery.ToComponentDataArray<OwnerOrg>(Allocator.Temp);

                // Create relations between neighboring villages
                for (int i = 0; i < villages.Length; i++)
                {
                    for (int j = i + 1; j < villages.Length; j++)
                    {
                        float distance = math.distance(villageTransforms[i].Position, villageTransforms[j].Position);
                        if (distance < 50f) // Neighboring threshold
                        {
                            CreateOrgRelation(ref state, ref ecb, villageOwnerOrgs[i].OrgEntity, villageOwnerOrgs[j].OrgEntity, 20f); // Friendly start
                        }
                    }
                }

                villages.Dispose();
                villageTransforms.Dispose();
                villageOwnerOrgs.Dispose();
            }

            // Create orgs for carriers (Space4X)
            if (scenarioState.EnableSpace4x)
            {
                var carrierQuery = SystemAPI.QueryBuilder()
                    .WithAll<PlatformTag, LocalTransform, OwnerOrg>()
                    .Build();
                var carriers = carrierQuery.ToEntityArray(Allocator.Temp);
                var carrierTransforms = carrierQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                var carrierOwnerOrgs = carrierQuery.ToComponentDataArray<OwnerOrg>(Allocator.Temp);

                // Create relations between carriers (two opposing factions)
                for (int i = 0; i < carriers.Length; i++)
                {
                    for (int j = i + 1; j < carriers.Length; j++)
                    {
                        // For now, alternate between friendly and hostile
                        float attitude = (i % 2 == j % 2) ? 30f : -30f; // Same faction = friendly, different = hostile
                        CreateOrgRelation(ref state, ref ecb, carrierOwnerOrgs[i].OrgEntity, carrierOwnerOrgs[j].OrgEntity, attitude);
                    }
                }

                carriers.Dispose();
                carrierTransforms.Dispose();
                carrierOwnerOrgs.Dispose();
            }

            // Mark as complete
            var completeEntity = ecb.CreateEntity();
            ecb.AddComponent<OrgRelationInitCompleteTag>(completeEntity);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void CreateOrgRelation(ref SystemState state, ref EntityCommandBuffer ecb, Entity orgA, Entity orgB, float initialAttitude)
        {
            // Check if relation already exists
            var relationQuery = SystemAPI.QueryBuilder()
                .WithAll<OrgRelation, OrgRelationTag>()
                .Build();
            var relations = relationQuery.ToComponentDataArray<OrgRelation>(Allocator.Temp);

            bool exists = false;
            for (int i = 0; i < relations.Length; i++)
            {
                if ((relations[i].OrgA == orgA && relations[i].OrgB == orgB) ||
                    (relations[i].OrgA == orgB && relations[i].OrgB == orgA))
                {
                    exists = true;
                    break;
                }
            }
            relations.Dispose();

            if (!exists)
            {
                var relationEntity = ecb.CreateEntity();
                ecb.AddComponent(relationEntity, new OrgRelation
                {
                    OrgA = orgA,
                    OrgB = orgB,
                    Attitude = initialAttitude
                });
                ecb.AddComponent<OrgRelationTag>(relationEntity);
            }
        }
    }

    /// <summary>
    /// Tag to mark that org relations have been initialized.
    /// </summary>
    public struct OrgRelationInitCompleteTag : IComponentData { }
}

#endif
