#if PUREDOTS_SCENARIO

using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Village;
using PureDOTS.Runtime.Villager;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Formation;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Platform;
#if SPACE4X_AVAILABLE
using Space4X.Mining;
#endif
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Bootstrap
{
    using VillageTagRuntime = PureDOTS.Runtime.Village.VillageTag;

    /// <summary>
    /// PureDOTS system that spawns scenario entities from ScenarioConfig.
    /// Spreads spawning across multiple frames using the BootPhase state machine.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ScenarioBootstrapSystem))]
    public partial struct ScenarioRunnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Ensure a config singleton exists even if the authoring was omitted.
            if (!SystemAPI.HasSingleton<ScenarioConfig>())
            {
                var cfgEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<ScenarioConfig>(cfgEntity);
                state.EntityManager.SetComponentData(cfgEntity, new ScenarioConfig
                {
                    EnableGodgame = true,
                    EnableSpace4x = true,
                    EnableEconomy = false,
                    GodgameSeed = 12345u,
                    Space4xSeed = 67890u,
                    VillageCount = 1,
                    VillagersPerVillage = 3,
                    CarrierCount = 1,
                    AsteroidCount = 2,
                    StartingBandCount = 0,
                    Difficulty = 0.5f,
                    Density = 0.5f
                });
            }

            // Optional legacy visuals hook for simulation/test scenes.
#if PUREDOTS_LEGACY_SCENARIO_VISUALS && PUREDOTS_LEGACY_SCENARIO_ASM
            if (!SystemAPI.HasSingleton<PureDOTS.LegacyScenario.Village.VillageWorldTag>())
            {
                var tagEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<PureDOTS.LegacyScenario.Village.VillageWorldTag>(tagEntity);
            }
#endif
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioConfig>(out var config))
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<ScenarioState>(out var scenarioEntity))
            {
                return;
            }

            var em = state.EntityManager;
            var scenarioState = SystemAPI.GetComponent<ScenarioState>(scenarioEntity);

            switch (scenarioState.BootPhase)
            {
                case ScenarioBootPhase.None:
                    // Initialize boot phase based on config
                    if (config.EnableGodgame)
                    {
                        scenarioState.BootPhase = ScenarioBootPhase.SpawnGodgame;
                    }
                    else if (config.EnableSpace4x)
                    {
                        scenarioState.BootPhase = ScenarioBootPhase.SpawnSpace4x;
                    }
                    else
                    {
                        // Nothing to spawn, mark as done
                        scenarioState.BootPhase = ScenarioBootPhase.Done;
                        scenarioState.IsInitialized = true;
                    }
                    em.SetComponentData(scenarioEntity, scenarioState);
                    break;

                case ScenarioBootPhase.SpawnGodgame:
                    {
                        var random = Unity.Mathematics.Random.CreateFromIndex(config.GodgameSeed);
                        SpawnGodgameSlice(ref state, em, config, ref random);

                        // Move to next phase
                        scenarioState.BootPhase = config.EnableSpace4x ? ScenarioBootPhase.SpawnSpace4x : ScenarioBootPhase.Done;
                        em.SetComponentData(scenarioEntity, scenarioState);
                    }
                    break;

                case ScenarioBootPhase.SpawnSpace4x:
                    {
                        var spaceRandom = Unity.Mathematics.Random.CreateFromIndex(config.Space4xSeed);
                        SpawnSpace4xSlice(ref state, em, config, ref spaceRandom);

                        // Mark as done
                        scenarioState.BootPhase = ScenarioBootPhase.Done;
                        scenarioState.IsInitialized = true;
                        scenarioState.EnableGodgame = config.EnableGodgame;
                        scenarioState.EnableSpace4x = config.EnableSpace4x;
                        scenarioState.EnableEconomy = config.EnableEconomy;
                        em.SetComponentData(scenarioEntity, scenarioState);

                        // Mark config as complete
                        if (SystemAPI.TryGetSingletonEntity<ScenarioConfig>(out var configEntity))
                        {
                            em.AddComponent<ScenarioCompleteTag>(configEntity);
                        }
                        state.Enabled = false;
                    }
                    break;

                case ScenarioBootPhase.Done:
                    state.Enabled = false;
                    break;
            }
        }

        private static void SpawnGodgameSlice(ref SystemState state, EntityManager em, in ScenarioConfig config, ref Unity.Mathematics.Random random)
        {
            // Spawn flat terrain tile (simple quad)
            var terrainEntity = em.CreateEntity();
            em.AddComponentData(terrainEntity, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 200f // Large flat plane
            });
            // Note: Visual representation handled by presentation layer

            // Create orgs for villages first
            using var villageOrgs = new NativeList<Entity>(config.VillageCount, Allocator.Temp);
            for (int v = 0; v < config.VillageCount; v++)
            {
                var orgEntity = em.CreateEntity();
                em.AddComponent<OrgTag>(orgEntity);
                em.AddComponentData(orgEntity, new OrgId
                {
                    Value = v + 1,
                    Kind = OrgKind.Faction
                });
                em.AddComponentData(orgEntity, new OrgAlignment
                {
                    Moral = random.NextFloat(-0.5f, 0.5f),
                    Order = random.NextFloat(-0.5f, 0.5f),
                    Purity = random.NextFloat(-0.5f, 0.5f)
                });
                em.AddComponentData(orgEntity, new OrgOutlook
                {
                    Primary = (byte)(v % 4), // Cycle through outlooks
                    Secondary = (byte)((v + 1) % 4)
                });
                villageOrgs.Add(orgEntity);
            }

            // Spawn villages
            for (int v = 0; v < config.VillageCount; v++)
            {
                float angle = (float)v / config.VillageCount * math.PI * 2f;
                float radius = 20f + random.NextFloat(0f, 10f) * config.Density;
                float3 villagePos = new float3(
                    math.cos(angle) * radius,
                    0f,
                    math.sin(angle) * radius
                );

                var villageEntity = em.CreateEntity();
                em.AddComponentData(villageEntity, new LocalTransform
                {
                    Position = villagePos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });

                // Basic village components
                em.AddComponent<VillageTagRuntime>(villageEntity);
                em.AddComponentData(villageEntity, new PureDOTS.Runtime.Village.VillageId
                {
                    Value = v + 1
                });
                em.AddComponentData(villageEntity, new VillageAlignment
                {
                    LawChaos = 0f,
                    GoodEvil = 0f,
                    OrderChaos = 0f
                });
                em.AddComponentData(villageEntity, new VillageResources
                {
                    Food = 100f,
                    Wood = 50f,
                    Stone = 30f,
                    Ore = 20f,
                    Metal = 0f,
                    Fuel = 0f
                });
                
                // Assign owner org
                em.AddComponentData(villageEntity, new OwnerOrg
                {
                    OrgEntity = villageOrgs[v]
                });

                // Spawn Storage building near village
                float3 storagePos = villagePos + new float3(2f, 0f, 2f);
                var storageEntity = em.CreateEntity();
                em.AddComponentData(storageEntity, new LocalTransform
                {
                    Position = storagePos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponent<StorageTag>(storageEntity);
                em.AddComponent<RewindableTag>(storageEntity);
                em.AddBuffer<ResourceStack>(storageEntity); // Inventory for storage

                // Spawn Lumberyard facility
                float3 lumberyardPos = villagePos + new float3(-2f, 0f, 2f);
                var lumberyardEntity = em.CreateEntity();
                em.AddComponentData(lumberyardEntity, new LocalTransform
                {
                    Position = lumberyardPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponent<LumberyardTag>(lumberyardEntity);
                em.AddComponentData(lumberyardEntity, new PureDOTS.Runtime.Facility.Facility
                {
                    ArchetypeId = PureDOTS.Runtime.Facility.FacilityArchetypeId.Lumberyard,
                    CurrentRecipeId = 0, // First recipe for Lumberyard
                    WorkProgress = 0f
                });
                em.AddBuffer<ResourceStack>(lumberyardEntity); // Inventory for facility
                em.AddComponent<RewindableTag>(lumberyardEntity);

                // Spawn Smelter facility
                float3 smelterPos = villagePos + new float3(2f, 0f, -2f);
                var smelterEntity = em.CreateEntity();
                em.AddComponentData(smelterEntity, new LocalTransform
                {
                    Position = smelterPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponent<SmelterTag>(smelterEntity);
                em.AddComponentData(smelterEntity, new PureDOTS.Runtime.Facility.Facility
                {
                    ArchetypeId = PureDOTS.Runtime.Facility.FacilityArchetypeId.Smelter,
                    CurrentRecipeId = 1, // First recipe for Smelter
                    WorkProgress = 0f
                });
                em.AddBuffer<ResourceStack>(smelterEntity); // Inventory for facility
                em.AddComponent<RewindableTag>(smelterEntity);

                // Create village group/band
                var villageGroupEntity = em.CreateEntity();
                em.AddComponentData(villageGroupEntity, new LocalTransform
                {
                    Position = villagePos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponent<GroupTag>(villageGroupEntity);
                em.AddComponentData(villageGroupEntity, new GroupIdentity
                {
                    GroupId = v + 1,
                    ParentEntity = villageEntity,
                    LeaderEntity = Entity.Null, // Will be set to first villager
                    FormationTick = 0,
                    Status = GroupStatus.Active
                });
                em.AddComponentData(villageGroupEntity, GroupConfig.Default);
                em.AddBuffer<GroupMember>(villageGroupEntity);

                var groupMemberList = new NativeList<GroupMember>(config.VillagersPerVillage, Allocator.Temp);

                // Spawn villagers for this village
                Entity firstVillager = Entity.Null;
                for (int i = 0; i < config.VillagersPerVillage; i++)
                {
                    float villagerAngle = random.NextFloat(0f, math.PI * 2f);
                    float villagerRadius = random.NextFloat(2f, 8f);
                    float3 villagerPos = villagePos + new float3(
                        math.cos(villagerAngle) * villagerRadius,
                        0f,
                        math.sin(villagerAngle) * villagerRadius
                    );

                    var villagerEntity = CreateVillagerEntity(em, villagerPos, v + 1, i + 1);
                    em.AddComponent<VillagerTag>(villagerEntity); // For VillageVisualSetupSystem
                    
                    if (i == 0)
                    {
                        firstVillager = villagerEntity;
                    }

                    // Add villager to village group
                    groupMemberList.Add(new GroupMember
                    {
                        MemberEntity = villagerEntity,
                        Weight = i == 0 ? 1f : 0.5f, // First villager is leader
                        Role = i == 0 ? GroupRole.Leader : GroupRole.Member,
                        JoinedTick = 0,
                        Flags = GroupMemberFlags.Active
                    });
                }

                // Now append members after all structural changes are done
                var groupMembers = em.GetBuffer<GroupMember>(villageGroupEntity);
                for (int i = 0; i < groupMemberList.Length; i++)
                {
                    groupMembers.Add(groupMemberList[i]);
                }
                groupMemberList.Dispose();

                // Set first villager as leader
                if (firstVillager != Entity.Null)
                {
                    var groupIdentity = em.GetComponentData<GroupIdentity>(villageGroupEntity);
                    groupIdentity.LeaderEntity = firstVillager;
                    em.SetComponentData(villageGroupEntity, groupIdentity);
                }

                // Spawn resource nodes around village
                int nodeCount = math.max(3, (int)(5 * config.Density));
                for (int n = 0; n < nodeCount; n++)
                {
                    float nodeAngle = random.NextFloat(0f, math.PI * 2f);
                    float nodeRadius = random.NextFloat(10f, 20f);
                    float3 nodePos = villagePos + new float3(
                        math.cos(nodeAngle) * nodeRadius,
                        0f,
                        math.sin(nodeAngle) * nodeRadius
                    );

                    // Alternate between tree, stone, ore
                    var nodeType = (n % 3);
                    float nodeRichness = 100f * config.Density;
                    CreateResourceNode(em, nodePos, nodeType, nodeRichness);
                }
            }

            // Spawn bands
            for (int b = 0; b < config.StartingBandCount; b++)
            {
                float angle = random.NextFloat(0f, math.PI * 2f);
                float radius = 30f + random.NextFloat(0f, 15f);
                float3 bandPos = new float3(
                    math.cos(angle) * radius,
                    0f,
                    math.sin(angle) * radius
                );

                // Basic band entity
                var bandEntity = em.CreateEntity();
                em.AddComponentData(bandEntity, new LocalTransform
                {
                    Position = bandPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                em.AddComponent<GroupTag>(bandEntity);
                em.AddComponentData(bandEntity, new GroupIdentity
                {
                    GroupId = b + 1,
                    ParentEntity = Entity.Null,
                    LeaderEntity = Entity.Null,
                    FormationTick = 0,
                    Status = GroupStatus.Active
                });
                em.AddComponentData(bandEntity, GroupConfig.Default);
                em.AddComponentData(bandEntity, new GroupStanceState
                {
                    Stance = GroupStance.Hold,
                    PrimaryTarget = Entity.Null,
                    Aggression = 0f,
                    Discipline = 0.5f
                });
                em.AddComponentData(bandEntity, new FormationState
                {
                    Type = PureDOTS.Runtime.Formation.FormationType.Patrol,
                    AnchorPosition = bandPos,
                    AnchorRotation = quaternion.identity,
                    Spacing = 2f,
                    Scale = 1f,
                    MaxSlots = 8,
                    FilledSlots = 0,
                    IsMoving = false,
                    LastUpdateTick = 0
                });
                em.AddBuffer<GroupMember>(bandEntity);
            }
        }

        private static void SpawnSpace4xSlice(ref SystemState state, EntityManager em, in ScenarioConfig config, ref Unity.Mathematics.Random random)
        {
            // Create orgs for carriers first
            NativeList<Entity> carrierOrgs = new NativeList<Entity>(config.CarrierCount, Allocator.Temp);
            for (int c = 0; c < config.CarrierCount; c++)
            {
                var orgEntity = em.CreateEntity();
                em.AddComponent<OrgTag>(orgEntity);
                em.AddComponentData(orgEntity, new OrgId
                {
                    Value = c + 1,
                    Kind = OrgKind.Faction
                });
                em.AddComponentData(orgEntity, new OrgAlignment
                {
                    Moral = random.NextFloat(-0.5f, 0.5f),
                    Order = random.NextFloat(-0.5f, 0.5f),
                    Purity = random.NextFloat(-0.5f, 0.5f)
                });
                em.AddComponentData(orgEntity, new OrgOutlook
                {
                    Primary = (byte)(c % 4),
                    Secondary = (byte)((c + 1) % 4)
                });
                carrierOrgs.Add(orgEntity);
            }

            // Spawn carriers
            for (int c = 0; c < config.CarrierCount; c++)
            {
                float angle = (float)c / config.CarrierCount * math.PI * 2f;
                float radius = 50f + random.NextFloat(0f, 20f);
                float3 carrierPos = new float3(
                    math.cos(angle) * radius,
                    random.NextFloat(-5f, 5f),
                    math.sin(angle) * radius
                );

                var carrierEntity = em.CreateEntity();
                em.AddComponentData(carrierEntity, new LocalTransform
                {
                    Position = carrierPos,
                    Rotation = quaternion.identity,
                    Scale = 10f // Carriers are large
                });

                // Platform components
                em.AddComponent<PlatformTag>(carrierEntity);
                em.AddComponentData(carrierEntity, new PlatformKind
                {
                    Flags = PlatformFlags.Capital | PlatformFlags.IsCarrier | PlatformFlags.HasHangar
                });
                em.AddComponentData(carrierEntity, new PlatformHullRef
                {
                    HullId = 1 // Stub hull ID
                });
                em.AddComponentData(carrierEntity, new PlatformResources
                {
                    Ore = 0f,
                    RefinedOre = 0f,
                    Fuel = 100f,
                    Supplies = 50f,
                    RawMaterials = 0f,
                    ProcessedMaterials = 0f
                });

                // HangarBay buffer
                int minersPerCarrier = 3; // Default value
                int craftPerCarrier = 2; // Default value
                var hangarBays = em.AddBuffer<HangarBay>(carrierEntity);
                hangarBays.Add(new HangarBay
                {
                    HangarClassId = 1,
                    Capacity = minersPerCarrier + craftPerCarrier,
                    ReservedSlots = 0,
                    OccupiedSlots = 0,
                    LaunchRate = 1f,
                    RecoveryRate = 1f
                });

                // Create hangar assignment buffer (filled after miners are created)
                em.AddBuffer<HangarAssignment>(carrierEntity);
                var minerList = new NativeList<Entity>(minersPerCarrier, Allocator.Temp);

                // Add Facility component for refinery (ore -> refined ore)
                em.AddComponentData(carrierEntity, new PureDOTS.Runtime.Facility.Facility
                {
                    ArchetypeId = PureDOTS.Runtime.Facility.FacilityArchetypeId.Refinery,
                    CurrentRecipeId = 2, // Refinery recipe
                    WorkProgress = 0f
                });
                em.AddBuffer<ResourceStack>(carrierEntity); // Inventory for facility

                // Spawn miners for this carrier
                for (int m = 0; m < minersPerCarrier; m++)
                {
                    float3 minerPos = carrierPos + random.NextFloat3Direction() * random.NextFloat(5f, 10f);
                    var minerEntity = CreateMiningVessel(em, minerPos, carrierEntity, m + 1);
                    minerList.Add(minerEntity);
                }

                // Now safely fill hangar assignments after structural changes
                var hangarAssignments = em.GetBuffer<HangarAssignment>(carrierEntity);
                for (int i = 0; i < minerList.Length; i++)
                {
                    hangarAssignments.Add(new HangarAssignment
                    {
                        SubPlatform = minerList[i],
                        HangarIndex = 0
                    });
                }
                minerList.Dispose();

                // Assign owner org
                em.AddComponentData(carrierEntity, new OwnerOrg
                {
                    OrgEntity = carrierOrgs[c]
                });

                // Add rewind support
                em.AddComponent<RewindableTag>(carrierEntity);
            }

            carrierOrgs.Dispose();

            // Spawn asteroids
            for (int a = 0; a < config.AsteroidCount; a++)
            {
                float angle = random.NextFloat(0f, math.PI * 2f);
                float radius = 60f + random.NextFloat(0f, 30f);
                float3 asteroidPos = new float3(
                    math.cos(angle) * radius,
                    random.NextFloat(-10f, 10f),
                    math.sin(angle) * radius
                );

                var asteroidEntity = em.CreateEntity();
                float3 randomEuler = random.NextFloat3(new float3(0f, 0f, 0f), new float3(360f, 360f, 360f));
                em.AddComponentData(asteroidEntity, new LocalTransform
                {
                    Position = asteroidPos,
                    Rotation = quaternion.Euler(math.radians(randomEuler)),
                    Scale = 1f + random.NextFloat(0.5f, 2f)
                });

                // Add ResourceNodeTag and ResourceDeposit
                em.AddComponent<ResourceNodeTag>(asteroidEntity);
                em.AddComponentData(asteroidEntity, new ResourceDeposit
                {
                    ResourceTypeId = 2, // Ore (assuming 0=wood, 1=stone, 2=ore)
                    CurrentAmount = 200f * config.Density,
                    MaxAmount = 200f * config.Density,
                    RegenPerSecond = 0f
                });

                // Add rewind support
                em.AddComponent<RewindableTag>(asteroidEntity);
            }
        }

        private static Entity CreateMiningVessel(EntityManager em, float3 position, Entity carrierEntity, int minerId)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

#if SPACE4X_AVAILABLE
            // Mining vessel components
            em.AddComponent<MiningVesselTag>(entity);
            em.AddComponentData(entity, new MiningVesselFrameDef
            {
                MaxCargo = 50f,
                MiningRate = 5f
            });
            em.AddComponentData(entity, new CraftFrameRef
            {
                FrameId = 1 // Stub frame ID
            });
            em.AddComponentData(entity, new MiningJob
            {
                Phase = MiningPhase.Idle,
                TargetAsteroid = Entity.Null,
                CarrierEntity = carrierEntity,
                CargoAmount = 0f,
                TargetPosition = float3.zero,
                LastStateChangeTick = 0
            });
#else
            // Space4X types not available - create minimal platform entity instead
            em.AddComponent<PlatformTag>(entity);
            em.AddComponentData(entity, new PlatformKind
            {
                Flags = PlatformFlags.Craft
            });
#endif

            // Add rewind support
            em.AddComponent<RewindableTag>(entity);

            return entity;
        }

        private static Entity CreateVillagerEntity(EntityManager em, float3 position, int villageId, int villagerId)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            em.AddComponentData(entity, new VillagerId
            {
                Value = villagerId,
                FactionId = villageId
            });

            em.AddComponentData(entity, new VillagerNeeds
            {
                Food = 50,
                Rest = 80,
                Sleep = 70,
                GeneralHealth = 100,
                Health = 100f,
                MaxHealth = 100f,
                Hunger = 50f,
                Energy = 80f,
                Morale = 75f,
                Temperature = 20f
            });

            em.AddComponentData(entity, new VillagerJob
            {
                Type = VillagerJob.JobType.Gatherer,
                Phase = VillagerJob.JobPhase.Idle,
                ActiveTicketId = 0,
                Productivity = 1f,
                LastStateChangeTick = 0
            });

            em.AddComponentData(entity, new VillagerAIState
            {
                CurrentState = VillagerAIState.State.Idle,
                CurrentGoal = VillagerAIState.Goal.Work,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            // Add movement component
            em.AddComponentData(entity, new VillagerMovement
            {
                Velocity = float3.zero,
                DesiredVelocity = float3.zero,
                BaseSpeed = 3f,
                CurrentSpeed = 3f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                IsStuck = 0,
                LastMoveTick = 0
            });

            // Add rewind support
            em.AddComponent<RewindableTag>(entity);
            em.AddBuffer<PositionHistorySample>(entity);
            em.AddBuffer<HealthHistorySample>(entity);

            return entity;
        }

        private static void CreateResourceNode(EntityManager em, float3 position, int nodeType, float richness = 100f)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            // Add ResourceNodeTag
            em.AddComponent<ResourceNodeTag>(entity);
            
            // Mark as pickable for hand interactions
            em.AddComponent<PickableTag>(entity);
            em.AddComponent<HeldByPlayer>(entity);
            em.SetComponentEnabled<HeldByPlayer>(entity, false);
            em.AddComponent<MovementSuppressed>(entity);
            em.SetComponentEnabled<MovementSuppressed>(entity, false);
            em.AddComponent<BeingThrown>(entity);
            em.SetComponentEnabled<BeingThrown>(entity, false);

            // Add type-specific tag
            // nodeType: 0=tree, 1=stone, 2=ore
            if (nodeType == 0)
            {
                em.AddComponent<TreeTag>(entity);
            }
            else if (nodeType == 1)
            {
                em.AddComponent<StoneNodeTag>(entity);
            }
            else if (nodeType == 2)
            {
                em.AddComponent<OreNodeTag>(entity);
            }

            // Add ResourceDeposit component
            // ResourceTypeId: 0=wood, 1=stone, 2=ore (assuming catalog order)
            // For simulation, we'll use the nodeType directly as ResourceTypeId index
            em.AddComponentData(entity, new ResourceDeposit
            {
                ResourceTypeId = nodeType, // 0=wood, 1=stone, 2=ore
                CurrentAmount = richness,
                MaxAmount = richness,
                RegenPerSecond = 0f // No regeneration for simulation
            });

            // Add rewind support
            em.AddComponent<RewindableTag>(entity);
        }
    }
}

#endif
