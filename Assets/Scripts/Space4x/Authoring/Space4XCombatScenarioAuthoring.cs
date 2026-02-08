using System;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Spatial;
using Space4X.Editor.DevMenu;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Authoring component for creating a combat scenario scene with carriers, strike craft, and combat ships.
    /// Designed for testing AI behaviors, formation systems, and combat resolution.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PureDotsConfigAuthoring))]
    [RequireComponent(typeof(SpatialPartitionAuthoring))]
    [MovedFrom(true, "Space4X.Authoring", null, "Space4XCombatDemoAuthoring")]
    public sealed class Space4XCombatScenarioAuthoring : MonoBehaviour
    {
        [Header("Player Fleet")]
        [SerializeField]
        private FleetConfiguration playerFleet = new FleetConfiguration
        {
            fleetName = "Alpha Fleet",
            factionId = "player",
            centerPosition = new float3(-50, 0, 0),
            carriers = 1,
            frigates = 2,
            destroyers = 1,
            fightersPerCarrier = 8,
            bombersPerCarrier = 4
        };

        [Header("Enemy Fleet")]
        [SerializeField]
        private FleetConfiguration enemyFleet = new FleetConfiguration
        {
            fleetName = "Pirate Raiders",
            factionId = "enemy",
            centerPosition = new float3(50, 0, 0),
            carriers = 1,
            frigates = 1,
            destroyers = 0,
            fightersPerCarrier = 6,
            bombersPerCarrier = 2
        };

        [Header("Environment")]
        [SerializeField]
        private EnvironmentConfiguration environment = new EnvironmentConfiguration
        {
            asteroidCount = 10,
            asteroidSpread = 100f,
            asteroidCenter = float3.zero
        };

        [Header("Spawn Settings")]
        [SerializeField] private bool spawnPlayerFleet = true;
        [SerializeField] private bool spawnEnemyFleet = true;
        [SerializeField] private bool spawnEnvironment = true;

        [Header("Debug")]
        [SerializeField] private bool enableAttackMoveDebugLines = true;
        [SerializeField] private bool disableDepthBobbing = false;

        [Serializable]
        public struct FleetConfiguration
        {
            public string fleetName;
            public string factionId;
            public float3 centerPosition;

            [Header("Capital Ships")]
            [Range(0, 5)] public int carriers;
            [Range(0, 10)] public int frigates;
            [Range(0, 5)] public int destroyers;
            [Range(0, 3)] public int cruisers;
            [Range(0, 2)] public int battleships;

            [Header("Strike Craft")]
            [Range(0, 24)] public int fightersPerCarrier;
            [Range(0, 12)] public int bombersPerCarrier;
        }

        [Serializable]
        public struct EnvironmentConfiguration
        {
            [Range(0, 50)] public int asteroidCount;
            [Min(10f)] public float asteroidSpread;
            public float3 asteroidCenter;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XCombatScenarioAuthoring>
        {
            private uint _entityCounter;

            public override void Bake(Space4XCombatScenarioAuthoring authoring)
            {
                _entityCounter = 0;

                // Create config singleton
                var configEntity = GetEntity(TransformUsageFlags.None);
                AddComponent(configEntity, new CombatScenarioConfig
                {
                    PlayerFleetSpawned = authoring.spawnPlayerFleet ? (byte)1 : (byte)0,
                    EnemyFleetSpawned = authoring.spawnEnemyFleet ? (byte)1 : (byte)0
                });
                AddComponent(configEntity, new Space4XPresentationDebugConfig
                {
                    EnableAttackMoveDebugLines = authoring.enableAttackMoveDebugLines ? (byte)1 : (byte)0,
                    DisableDepthBobbing = authoring.disableDepthBobbing ? (byte)1 : (byte)0
                });

                if (authoring.spawnPlayerFleet)
                {
                    BakeFleet(authoring.playerFleet, isPlayer: true);
                }

                if (authoring.spawnEnemyFleet)
                {
                    BakeFleet(authoring.enemyFleet, isPlayer: false);
                }

                if (authoring.spawnEnvironment)
                {
                    BakeEnvironment(authoring.environment);
                }

                UnityDebug.Log($"[CombatScenarioAuthoring] Baked combat scenario with {_entityCounter} entities");
            }

            private void BakeFleet(FleetConfiguration fleet, bool isPlayer)
            {
                var random = new Unity.Mathematics.Random((uint)(fleet.fleetName.GetHashCode() + 1));
                var fleetEntities = new NativeList<Entity>(Allocator.Temp);
                
                // Create fleet entity
                var fleetEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(fleetEntity, new Space4XFleet
                {
                    FleetId = new FixedString64Bytes(fleet.fleetName),
                    ShipCount = 0,
                    Posture = isPlayer ? Space4XFleetPosture.Patrol : Space4XFleetPosture.Engaging,
                    TaskForce = 0
                });

                // Spawn carriers
                for (int i = 0; i < fleet.carriers; i++)
                {
                    float3 offset = new float3(random.NextFloat(-10, 10), 0, random.NextFloat(-10, 10));
                    var carrierEntity = SpawnCarrier(fleet.centerPosition + offset, fleet.factionId, i, fleetEntity);
                    fleetEntities.Add(carrierEntity);

                    // Spawn strike craft for this carrier
                    SpawnStrikeCraft(carrierEntity, fleet.centerPosition + offset, fleet.factionId, 
                        fleet.fightersPerCarrier, fleet.bombersPerCarrier, ref random);
                }

                // Spawn escorts
                for (int i = 0; i < fleet.frigates; i++)
                {
                    float3 offset = new float3(random.NextFloat(-20, 20), 0, random.NextFloat(-20, 20));
                    fleetEntities.Add(SpawnEscort(fleet.centerPosition + offset, fleet.factionId, "frigate", fleetEntity));
                }

                for (int i = 0; i < fleet.destroyers; i++)
                {
                    float3 offset = new float3(random.NextFloat(-25, 25), 0, random.NextFloat(-25, 25));
                    fleetEntities.Add(SpawnEscort(fleet.centerPosition + offset, fleet.factionId, "destroyer", fleetEntity));
                }

                for (int i = 0; i < fleet.cruisers; i++)
                {
                    float3 offset = new float3(random.NextFloat(-30, 30), 0, random.NextFloat(-30, 30));
                    fleetEntities.Add(SpawnEscort(fleet.centerPosition + offset, fleet.factionId, "cruiser", fleetEntity));
                }

                for (int i = 0; i < fleet.battleships; i++)
                {
                    float3 offset = new float3(random.NextFloat(-15, 15), 0, random.NextFloat(-15, 15));
                    fleetEntities.Add(SpawnEscort(fleet.centerPosition + offset, fleet.factionId, "battleship", fleetEntity));
                }

                fleetEntities.Dispose();
            }

            private Entity SpawnCarrier(float3 position, string factionId, int index, Entity fleetEntity)
            {
                var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                _entityCounter++;

                AddComponent(entity, LocalTransform.FromPosition(position));
                AddComponent<SpatialIndexedTag>(entity);

                var carrierId = $"CARRIER_{factionId}_{index}";

                AddComponent(entity, new Carrier
                {
                    CarrierId = new FixedString64Bytes(carrierId),
                    AffiliationEntity = Entity.Null,
                    Speed = 5f,
                    PatrolCenter = position,
                    PatrolRadius = 30f
                });

                AddComponent(entity, new PatrolBehavior
                {
                    CurrentWaypoint = position,
                    WaitTime = 3f
                });

                AddComponent(entity, new MovementCommand
                {
                    TargetPosition = position,
                    ArrivalThreshold = 2f
                });

                // Combat
                AddComponent(entity, new Space4XShield
                {
                    Type = ShieldType.Standard,
                    Current = 1000f,
                    Maximum = 1000f,
                    RechargeRate = 10f,
                    RechargeDelay = 5,
                    CurrentDelay = 0,
                    EnergyResistance = (half)1f,
                    ThermalResistance = (half)1f,
                    EMResistance = (half)1f,
                    RadiationResistance = (half)1f,
                    KineticResistance = (half)1f,
                    ExplosiveResistance = (half)1f
                });

                AddComponent(entity, new HullIntegrity
                {
                    Current = 2000f,
                    Max = 2000f,
                    BaseMax = 2000f
                });

                // Hangar
                AddComponent(entity, new DockingCapacity
                {
                    MaxSmallCraft = 24,
                    CurrentSmallCraft = 0
                });

                // AI
                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.Patrol,
                    TargetEntity = Entity.Null
                });

                AddComponent(entity, new VesselMovement
                {
                    BaseSpeed = 5f,
                    CurrentSpeed = 0f
                });

                // Fleet membership
                AddComponent(entity, new FormationAssignment
                {
                    FormationLeader = fleetEntity,
                    SlotIndex = 0,
                    CurrentOffset = float3.zero,
                    TargetPosition = float3.zero,
                    FormationTightness = (half)0.8f,
                    AssignedTick = 0
                });

                // Captain
                AddComponent(entity, new CaptainState
                {
                    Autonomy = CaptainAutonomy.Tactical,
                    IsReady = 1,
                    Confidence = (half)0.8f,
                    RiskTolerance = (half)0.5f,
                    LastEvaluationTick = 0,
                    SuccessCount = 0,
                    FailureCount = 0
                });
                AddComponent(entity, CaptainReadiness.Standard);
                AddComponent(entity, new CaptainOrder
                {
                    Type = CaptainOrderType.None,
                    Status = CaptainOrderStatus.None,
                    Priority = 0,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    IssuedTick = 0,
                    TimeoutTick = 0,
                    IssuingAuthority = Entity.Null
                });

                // Morale
                AddComponent(entity, new MoraleState
                {
                    Current = (half)0.8f,
                    Baseline = (half)0.75f,
                    DriftRate = (half)0.01f,
                    LastUpdateTick = 0
                });
                AddBuffer<MoraleModifier>(entity);

                // Affiliation
                var affiliationBuffer = AddBuffer<AffiliationTag>(entity);
                affiliationBuffer.Add(new AffiliationTag
                {
                    Type = AffiliationType.Fleet,
                    Target = fleetEntity,
                    Loyalty = (half)0.9f
                });

                // Resources
                var storage = AddBuffer<ResourceStorage>(entity);
                storage.Add(ResourceStorage.Create(ResourceType.Food, 10000f));
                storage.Add(ResourceStorage.Create(ResourceType.Water, 10000f));
                storage.Add(ResourceStorage.Create(ResourceType.Supplies, 10000f));
                storage.Add(ResourceStorage.Create(ResourceType.Fuel, 10000f));
                storage.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
                storage.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));

                var lawfulness = factionId == "enemy" ? -0.6f : 0.6f;
                AddCarrierCrew(entity, lawfulness);

                return entity;
            }

            private void SpawnStrikeCraft(Entity carrierEntity, float3 carrierPos, string factionId, int fighters, int bombers, ref Unity.Mathematics.Random random)
            {
                // Fighters
                for (int i = 0; i < fighters; i++)
                {
                    float3 offset = new float3(random.NextFloat(-5, 5), 0, random.NextFloat(-5, 5));
                    SpawnSingleStrikeCraft(carrierPos + offset, factionId, "fighter", carrierEntity, i);
                }

                // Bombers
                for (int i = 0; i < bombers; i++)
                {
                    float3 offset = new float3(random.NextFloat(-5, 5), 0, random.NextFloat(-5, 5));
                    SpawnSingleStrikeCraft(carrierPos + offset, factionId, "bomber", carrierEntity, fighters + i);
                }
            }

            private Entity SpawnSingleStrikeCraft(float3 position, string factionId, string craftType, Entity carrierEntity, int index)
            {
                var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                _entityCounter++;

                AddComponent(entity, LocalTransform.FromPosition(position));
                AddComponent<SpatialIndexedTag>(entity);

                float hull = craftType == "fighter" ? 50f : 80f;
                float damage = craftType == "fighter" ? 10f : 50f;
                StrikeCraftRole role = craftType == "bomber" ? StrikeCraftRole.Bomber : StrikeCraftRole.Fighter;

                AddComponent(entity, StrikeCraftProfile.Create(role, carrierEntity));
                AddComponent(entity, new StrikeCraftState
                {
                    CurrentState = StrikeCraftState.State.Approaching,
                    TargetEntity = Entity.Null,
                    TargetPosition = position,
                    Experience = 0f,
                    StateStartTick = 0,
                    KamikazeActive = 0,
                    KamikazeStartTick = 0,
                    DogfightPhase = StrikeCraftDogfightPhase.Approach,
                    DogfightPhaseStartTick = 0,
                    DogfightLastFireTick = 0,
                    DogfightWingLeader = Entity.Null
                });
                AddComponent<StrikeCraftDogfightTag>(entity);

                var pilot = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(pilot, AlignmentTriplet.FromFloats(0f, 0f, 0f));
                AddComponent(pilot, new BehaviorDispositionSeedRequest
                {
                    Seed = 0u,
                    SeedSalt = (uint)(index + 1)
                });
                var stanceEntries = AddBuffer<StanceEntry>(pilot);
                stanceEntries.Add(new StanceEntry
                {
                    StanceId = StanceId.Neutral,
                    Weight = (half)1f
                });
                var topStances = AddBuffer<TopStance>(pilot);
                topStances.Add(new TopStance
                {
                    StanceId = StanceId.Neutral,
                    Weight = (half)1f
                });
                AddComponent(entity, new StrikeCraftPilotLink
                {
                    Pilot = pilot
                });

                AddComponent(entity, AttackRunConfig.ForRole(role));

                AddComponent(entity, new HullIntegrity
                {
                    Current = hull,
                    Max = hull,
                    BaseMax = hull
                });
                var subsystems = AddBuffer<SubsystemHealth>(entity);
                var engineMax = math.max(5f, hull * 0.3f);
                var weaponMax = math.max(5f, hull * 0.2f);
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Engines,
                    Current = engineMax,
                    Max = engineMax,
                    RegenPerTick = math.max(0.01f, engineMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Weapons,
                    Current = weaponMax,
                    Max = weaponMax,
                    RegenPerTick = math.max(0.01f, weaponMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
                AddBuffer<SubsystemDisabled>(entity);
                AddBuffer<DamageScarEvent>(entity);

                var weaponBuffer = AddBuffer<WeaponMount>(entity);
                weaponBuffer.Add(new WeaponMount
                {
                    Weapon = new Space4XWeapon
                    {
                        Type = WeaponType.Laser,
                        Size = WeaponSize.Small,
                        BaseDamage = damage,
                        OptimalRange = 16f,
                        MaxRange = 20f,
                        BaseAccuracy = (half)0.85f,
                        CooldownTicks = 1,
                        CurrentCooldown = 0,
                        AmmoPerShot = 0,
                        ShieldModifier = (half)1f,
                        ArmorPenetration = (half)0.3f
                    },
                    CurrentTarget = Entity.Null,
                    IsEnabled = 1
                });

                AddComponent(entity, new RecallThresholds
                {
                    AmmoThreshold = (half)0.1f,
                    FuelThreshold = (half)0.2f,
                    HullThreshold = (half)0.25f,
                    Enabled = 1,
                    RecallTarget = carrierEntity
                });

                AddComponent(entity, new VesselResourceLevels
                {
                    CurrentAmmo = 100f,
                    MaxAmmo = 100f,
                    CurrentFuel = 100f,
                    MaxFuel = 100f
                });

                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.None
                });

                float craftSpeed = craftType == "bomber" ? 9f : 12f;
                AddComponent(entity, new VesselMovement
                {
                    BaseSpeed = craftSpeed,
                    CurrentSpeed = 0f
                });

                // Affiliation
                var affiliationBuffer = AddBuffer<AffiliationTag>(entity);
                affiliationBuffer.Add(new AffiliationTag
                {
                    Type = AffiliationType.Fleet,
                    Target = carrierEntity,
                    Loyalty = (half)0.95f
                });

                return entity;
            }

            private void AddCarrierCrew(Entity carrierEntity, float lawfulness)
            {
                var crew = AddBuffer<PlatformCrewMember>(carrierEntity);
                var config = StrikeCraftPilotProfileConfig.Default;

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)90,
                            Tactics = (half)70,
                            Logistics = (half)60,
                            Diplomacy = (half)60,
                            Engineering = (half)40,
                            Resolve = (half)85
                        },
                        BehaviorDisposition.FromValues(0.8f, 0.6f, 0.8f, 0.4f, 0.45f, 0.7f)),
                    RoleId = 0
                });

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)75,
                            Tactics = (half)55,
                            Logistics = (half)80,
                            Diplomacy = (half)50,
                            Engineering = (half)45,
                            Resolve = (half)70
                        },
                        BehaviorDisposition.FromValues(0.75f, 0.6f, 0.7f, 0.45f, 0.4f, 0.7f)),
                    RoleId = 0
                });

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)65,
                            Tactics = (half)80,
                            Logistics = (half)50,
                            Diplomacy = (half)45,
                            Engineering = (half)40,
                            Resolve = (half)60
                        },
                        BehaviorDisposition.FromValues(0.65f, 0.55f, 0.7f, 0.5f, 0.45f, 0.6f)),
                    RoleId = 0
                });
            }

            private Entity CreateCrewEntity(
                float lawfulness,
                in StrikeCraftPilotProfileConfig config,
                in IndividualStats stats,
                in BehaviorDisposition disposition)
            {
                var crew = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(crew, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
                AddComponent(crew, stats);
                AddComponent(crew, disposition);

                var StanceId = ResolveStanceId(config, lawfulness);
                var stanceEntries = AddBuffer<StanceEntry>(crew);
                var topStances = AddBuffer<TopStance>(crew);
                stanceEntries.Add(new StanceEntry
                {
                    StanceId = StanceId,
                    Weight = (half)1f
                });
                topStances.Add(new TopStance
                {
                    StanceId = StanceId,
                    Weight = (half)1f
                });

                return crew;
            }

            private static StanceId ResolveStanceId(in StrikeCraftPilotProfileConfig config, float lawfulness)
            {
                if (lawfulness >= config.LoyalistLawThreshold)
                {
                    return config.FriendlyStance;
                }

                if (lawfulness <= config.MutinousLawThreshold)
                {
                    return config.HostileStance;
                }

                return config.NeutralStance;
            }

            private Entity SpawnEscort(float3 position, string factionId, string shipType, Entity fleetEntity)
            {
                var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                _entityCounter++;

                AddComponent(entity, LocalTransform.FromPosition(position));
                AddComponent<SpatialIndexedTag>(entity);

                float speed, shield, hull, damage, range;
                switch (shipType)
                {
                    case "frigate":
                        speed = 12f; shield = 200f; hull = 400f; damage = 25f; range = 50f;
                        break;
                    case "destroyer":
                        speed = 10f; shield = 400f; hull = 600f; damage = 40f; range = 60f;
                        break;
                    case "cruiser":
                        speed = 7f; shield = 800f; hull = 1200f; damage = 60f; range = 80f;
                        break;
                    case "battleship":
                    default:
                        speed = 4f; shield = 1500f; hull = 3000f; damage = 100f; range = 100f;
                        break;
                }

                AddComponent(entity, new Space4XShield
                {
                    Type = ShieldType.Standard,
                    Current = shield,
                    Maximum = shield,
                    RechargeRate = shield * 0.02f,
                    RechargeDelay = 3,
                    CurrentDelay = 0,
                    EnergyResistance = (half)1f,
                    ThermalResistance = (half)1f,
                    EMResistance = (half)1f,
                    RadiationResistance = (half)1f,
                    KineticResistance = (half)1f,
                    ExplosiveResistance = (half)1f
                });

                AddComponent(entity, new HullIntegrity
                {
                    Current = hull,
                    Max = hull,
                    BaseMax = hull
                });

                var weaponBuffer = AddBuffer<WeaponMount>(entity);
                weaponBuffer.Add(new WeaponMount
                {
                    Weapon = new Space4XWeapon
                    {
                        Type = WeaponType.Laser,
                        Size = WeaponSize.Large,
                        BaseDamage = damage,
                        OptimalRange = range * 0.8f,
                        MaxRange = range,
                        BaseAccuracy = (half)0.8f,
                        CooldownTicks = 1,
                        CurrentCooldown = 0,
                        AmmoPerShot = 0,
                        ShieldModifier = (half)1f,
                        ArmorPenetration = (half)0.5f
                    },
                    CurrentTarget = Entity.Null,
                    IsEnabled = 1
                });

                AddComponent(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.Escort
                });

                AddComponent(entity, new VesselMovement
                {
                    BaseSpeed = speed,
                    CurrentSpeed = 0f
                });

                AddComponent(entity, new TargetSelectionProfile
                {
                    Strategy = TargetStrategy.Balanced,
                    EnabledFactors = TargetFactors.All,
                    DistanceWeight = (half)0.4f,
                    ThreatWeight = (half)0.4f,
                    WeaknessWeight = (half)0.3f,
                    ValueWeight = (half)0.3f,
                    AllyDefenseWeight = (half)0.3f,
                    MaxEngagementRange = range,
                    MinThreatThreshold = (half)0f
                });
                AddBuffer<TargetCandidate>(entity);

                AddComponent(entity, new FormationAssignment
                {
                    FormationLeader = fleetEntity,
                    SlotIndex = (byte)_entityCounter,
                    CurrentOffset = float3.zero,
                    TargetPosition = float3.zero,
                    FormationTightness = (half)0.8f,
                    AssignedTick = 0
                });

                // Affiliation
                var affiliationBuffer = AddBuffer<AffiliationTag>(entity);
                affiliationBuffer.Add(new AffiliationTag
                {
                    Type = AffiliationType.Fleet,
                    Target = fleetEntity,
                    Loyalty = (half)0.85f
                });

                return entity;
            }

            private void BakeEnvironment(EnvironmentConfiguration env)
            {
                var random = new Unity.Mathematics.Random(12345);

                for (int i = 0; i < env.asteroidCount; i++)
                {
                    float3 offset = random.NextFloat3(-env.asteroidSpread, env.asteroidSpread);
                    offset.y = 0;
                    float3 position = env.asteroidCenter + offset;

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    _entityCounter++;

                    AddComponent(entity, LocalTransform.FromPosition(position));
                    AddComponent<SpatialIndexedTag>(entity);

                    float resourceAmount = random.NextFloat(200, 1000);
                    float miningRate = random.NextFloat(5, 15);

                    AddComponent(entity, new Asteroid
                    {
                        AsteroidId = new FixedString64Bytes($"ASTEROID_{i}"),
                        ResourceType = i % 4 == 0 ? ResourceType.RareMetals : ResourceType.Minerals,
                        ResourceAmount = resourceAmount,
                        MaxResourceAmount = resourceAmount,
                        MiningRate = miningRate
                    });

                    AddComponent(entity, new ResourceSourceState
                    {
                        UnitsRemaining = resourceAmount
                    });

                    AddComponent(entity, new ResourceSourceConfig
                    {
                        GatherRatePerWorker = miningRate,
                        MaxSimultaneousWorkers = 4
                    });

                    AddComponent(entity, new Space4XAsteroidCenter
                    {
                        Position = position
                    });
                }
            }
        }
    }

    /// <summary>
    /// Config singleton for combat scenario.
    /// </summary>
    [MovedFrom(true, "Space4X.Authoring", null, "CombatDemoConfig")]
    public struct CombatScenarioConfig : IComponentData
    {
        public byte PlayerFleetSpawned;
        public byte EnemyFleetSpawned;
    }
}



