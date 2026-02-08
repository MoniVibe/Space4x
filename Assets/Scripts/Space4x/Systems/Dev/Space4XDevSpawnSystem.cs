using Space4X.Editor.DevMenu;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Systems.Dev
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Processes DevSpawnRequest components and creates fully-configured entities.
    /// Works with the Space4XDevMenuUI to provide runtime entity spawning.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XDevSpawnSystem : ISystem
    {
        private static uint _spawnCounter;
        private NativeHashMap<FixedString64Bytes, Entity> _affiliationEntityCache;

        private struct DevSpawnSnapshot
        {
            public Entity Entity;
            public DevSpawnRequest Request;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DevSpawnRequest>();
            if (!_affiliationEntityCache.IsCreated)
            {
                _affiliationEntityCache = new NativeHashMap<FixedString64Bytes, Entity>(8, Allocator.Persistent);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_affiliationEntityCache.IsCreated)
            {
                _affiliationEntityCache.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            EnsureDogfightConfigSingleton(ref state);

            var requests = new NativeList<DevSpawnSnapshot>(Allocator.Temp);
            foreach (var (request, entity) in SystemAPI.Query<RefRO<DevSpawnRequest>>().WithEntityAccess())
            {
                requests.Add(new DevSpawnSnapshot
                {
                    Entity = entity,
                    Request = request.ValueRO
                });
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var random = new Random((uint)UnityEngine.Time.frameCount + _spawnCounter++);

            for (int i = 0; i < requests.Length; i++)
            {
                var snapshot = requests[i];
                var category = snapshot.Request.Category.ToString();
                var templateId = snapshot.Request.TemplateId.ToString();
                var factionId = snapshot.Request.FactionId.ToString();
                var position = snapshot.Request.Position;

                // Remove the spawn request
                ecb.RemoveComponent<DevSpawnRequest>(snapshot.Entity);

                // Add appropriate components based on category
                switch (category)
                {
                    case "Carriers":
                        SetupCarrier(ref state, ref ecb, snapshot.Entity, templateId, factionId, position, ref random);
                        break;
                    case "Capital Ships":
                        SetupCapitalShip(ref state, ref ecb, snapshot.Entity, templateId, factionId, position, ref random);
                        break;
                    case "Strike Craft":
                        SetupStrikeCraft(ref state, ref ecb, snapshot.Entity, templateId, factionId, position, ref random);
                        break;
                    case "Support Vessels":
                        SetupSupportVessel(ref state, ref ecb, snapshot.Entity, templateId, factionId, position, ref random);
                        break;
                    case "Stations":
                        SetupStation(ref state, ref ecb, snapshot.Entity, templateId, factionId, position, ref random);
                        break;
                    case "Celestial":
                        SetupCelestial(ref ecb, snapshot.Entity, templateId, position, ref random);
                        break;
                    default:
                        UnityDebug.LogWarning($"[DevSpawnSystem] Unknown category: {category}");
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            requests.Dispose();
        }

        private void EnsureDogfightConfigSingleton(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<StrikeCraftDogfightConfig>(out var configEntity))
            {
                configEntity = state.EntityManager.CreateEntity(typeof(StrikeCraftDogfightConfig));
                state.EntityManager.SetComponentData(configEntity, StrikeCraftDogfightConfig.Default);
            }
        }

        private void SetupCarrier(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string templateId, string factionId, float3 position, ref Random random)
        {
            var carrierId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            // Get template values (would come from registry in production)
            float speed = 5f;
            float patrolRadius = 50f;
            float shieldStrength = 1000f;
            float hullPoints = 2000f;
            int hangarCapacity = 24;

            switch (templateId)
            {
                case "carrier_light":
                    speed = 8f; patrolRadius = 40f; shieldStrength = 500f; hullPoints = 1000f; hangarCapacity = 12;
                    break;
                case "carrier_heavy":
                    speed = 3f; patrolRadius = 60f; shieldStrength = 2000f; hullPoints = 5000f; hangarCapacity = 48;
                    break;
            }

            // Core carrier components
            ecb.AddComponent(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes(carrierId),
                AffiliationEntity = Entity.Null,
                Speed = speed,
                PatrolCenter = position,
                PatrolRadius = patrolRadius
            });
            ecb.AddComponent(entity, new PostTransformMatrix
            {
                Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
            });

            ecb.AddComponent(entity, new PatrolBehavior
            {
                CurrentWaypoint = position,
                WaitTime = 2f,
                WaitTimer = 0f
            });

            ecb.AddComponent(entity, new MovementCommand
            {
                TargetPosition = position,
                ArrivalThreshold = 2f
            });

            // Combat components
            ecb.AddComponent(entity, new Space4XShield
            {
                Type = ShieldType.Standard,
                Current = shieldStrength,
                Maximum = shieldStrength,
                RechargeRate = shieldStrength * 0.01f,
                RechargeDelay = 5,
                CurrentDelay = 0,
                EnergyResistance = (half)1f,
                ThermalResistance = (half)1f,
                EMResistance = (half)1f,
                RadiationResistance = (half)1f,
                KineticResistance = (half)1f,
                ExplosiveResistance = (half)1f
            });

            ecb.AddComponent(entity, new HullIntegrity
            {
                Current = hullPoints,
                Max = hullPoints,
                BaseMax = hullPoints
            });
            AddDefaultSubsystems(ref ecb, entity, hullPoints);
            AddDefaultSubsystems(ref ecb, entity, hullPoints);
            AddDefaultSubsystems(ref ecb, entity, hullPoints);
            AddDefaultSubsystems(ref ecb, entity, hullPoints);

            // Hangar
            ecb.AddComponent(entity, new DockingCapacity
            {
                MaxSmallCraft = (byte)hangarCapacity,
                CurrentSmallCraft = 0
            });

            // AI
            ecb.AddComponent(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Patrol,
                TargetEntity = Entity.Null,
                TargetPosition = position
            });

            ecb.AddComponent(entity, new VesselMovement
            {
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                IsMoving = 0
            });

            // Faction
            ecb.AddBuffer<AffiliationTag>(entity);
            AddFactionTag(ref state, ref ecb, entity, factionId);

            // Morale
            ecb.AddComponent(entity, new MoraleState
            {
                Baseline = (half)0.8f,
                Current = (half)0.8f,
                DriftRate = (half)0.01f,
                LastUpdateTick = 0
            });
            ecb.AddBuffer<MoraleModifier>(entity);

            // Resource storage
            var storage = ecb.AddBuffer<ResourceStorage>(entity);
            storage.Add(ResourceStorage.Create(ResourceType.Food, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Water, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Supplies, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Fuel, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));

            UnityDebug.Log($"[DevSpawnSystem] Created carrier {carrierId} at {position}");
        }

        private void SetupCapitalShip(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string templateId, string factionId, float3 position, ref Random random)
        {
            var shipId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            // Template values
            float speed = 7f;
            float shieldStrength = 800f;
            float hullPoints = 1200f;
            float weaponDamage = 60f;
            float weaponRange = 80f;

            switch (templateId)
            {
                case "frigate":
                    speed = 12f; shieldStrength = 200f; hullPoints = 400f; weaponDamage = 25f; weaponRange = 50f;
                    break;
                case "destroyer":
                    speed = 10f; shieldStrength = 400f; hullPoints = 600f; weaponDamage = 40f; weaponRange = 60f;
                    break;
                case "battleship":
                    speed = 4f; shieldStrength = 1500f; hullPoints = 3000f; weaponDamage = 100f; weaponRange = 100f;
                    break;
                case "dreadnought":
                    speed = 2f; shieldStrength = 3000f; hullPoints = 6000f; weaponDamage = 200f; weaponRange = 120f;
                    break;
            }

            // Combat components
            ecb.AddComponent(entity, new Space4XShield
            {
                Type = ShieldType.Standard,
                Current = shieldStrength,
                Maximum = shieldStrength,
                RechargeRate = shieldStrength * 0.02f,
                RechargeDelay = 3,
                CurrentDelay = 0,
                EnergyResistance = (half)1f,
                ThermalResistance = (half)1f,
                EMResistance = (half)1f,
                RadiationResistance = (half)1f,
                KineticResistance = (half)1f,
                ExplosiveResistance = (half)1f
            });

            ecb.AddComponent(entity, new HullIntegrity
            {
                Current = hullPoints,
                Max = hullPoints,
                BaseMax = hullPoints
            });

            // Weapon
            ecb.AddBuffer<WeaponMount>(entity);
            ecb.AppendToBuffer(entity, new WeaponMount
            {
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Large,
                    BaseDamage = weaponDamage,
                    OptimalRange = weaponRange * 0.8f,
                    MaxRange = weaponRange,
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

            // AI
            ecb.AddComponent(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = position
            });

            ecb.AddComponent(entity, new VesselMovement
            {
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                IsMoving = 0
            });

            // Target selection
            ecb.AddComponent(entity, new TargetSelectionProfile
            {
                Strategy = TargetStrategy.Balanced,
                EnabledFactors = TargetFactors.All,
                DistanceWeight = (half)0.4f,
                ThreatWeight = (half)0.4f,
                WeaknessWeight = (half)0.3f,
                ValueWeight = (half)0.3f,
                AllyDefenseWeight = (half)0.3f,
                MaxEngagementRange = weaponRange,
                MinThreatThreshold = (half)0f
            });
            ecb.AddBuffer<TargetCandidate>(entity);

            // Faction
            ecb.AddBuffer<AffiliationTag>(entity);
            AddFactionTag(ref state, ref ecb, entity, factionId);

            UnityDebug.Log($"[DevSpawnSystem] Created capital ship {shipId} ({templateId}) at {position}");
        }

        private void SetupStrikeCraft(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string templateId, string factionId, float3 position, ref Random random)
        {
            var craftId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            // Template values
            float speed = 25f;
            float hullPoints = 50f;
            float weaponDamage = 10f;
            float weaponRange = 20f;
            StrikeCraftRole strikeCraftRole = StrikeCraftRole.Fighter;

            switch (templateId)
            {
                case "fighter":
                    speed = 25f; hullPoints = 50f; weaponDamage = 10f; weaponRange = 20f;
                    strikeCraftRole = StrikeCraftRole.Fighter;
                    break;
                case "bomber":
                    speed = 15f; hullPoints = 80f; weaponDamage = 50f; weaponRange = 15f;
                    strikeCraftRole = StrikeCraftRole.Bomber;
                    break;
                case "interceptor":
                    speed = 30f; hullPoints = 40f; weaponDamage = 15f; weaponRange = 25f;
                    strikeCraftRole = StrikeCraftRole.Interceptor;
                    break;
                case "gunship":
                    speed = 12f; hullPoints = 120f; weaponDamage = 35f; weaponRange = 30f;
                    strikeCraftRole = StrikeCraftRole.Suppression;
                    break;
            }

            // Strike craft specific
            var profile = StrikeCraftProfile.Create(strikeCraftRole, Entity.Null);
            ecb.AddComponent(entity, profile);
            ecb.AddComponent(entity, new StrikeCraftState
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
            ecb.AddComponent<StrikeCraftDogfightTag>(entity);
            ecb.AddComponent(entity, AttackRunConfig.ForRole(strikeCraftRole));
            var pilot = ecb.CreateEntity();
            ecb.AddComponent(pilot, AlignmentTriplet.FromFloats(0f, 0f, 0f));
            var outlookEntries = ecb.AddBuffer<StanceEntry>(pilot);
            outlookEntries.Add(new StanceEntry
            {
                StanceId = StanceId.Neutral,
                Weight = (half)1f
            });
            var outlooks = ecb.AddBuffer<TopStance>(pilot);
            outlooks.Add(new TopStance
            {
                StanceId = StanceId.Neutral,
                Weight = (half)1f
            });
            ecb.AddComponent(entity, new StrikeCraftPilotLink
            {
                Pilot = pilot
            });

            // Combat
            ecb.AddComponent(entity, new HullIntegrity
            {
                Current = hullPoints,
                Max = hullPoints,
                BaseMax = hullPoints
            });

            ecb.AddBuffer<WeaponMount>(entity);
            ecb.AppendToBuffer(entity, new WeaponMount
            {
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = weaponDamage,
                    OptimalRange = weaponRange * 0.8f,
                    MaxRange = weaponRange,
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

            // Recall
            ecb.AddComponent(entity, new RecallThresholds
            {
                AmmoThreshold = (half)0.1f,
                FuelThreshold = (half)0.2f,
                HullThreshold = (half)0.25f,
                Enabled = 1,
                RecallTarget = Entity.Null
            });

            ecb.AddComponent(entity, new VesselResourceLevels
            {
                CurrentAmmo = 100f,
                MaxAmmo = 100f,
                CurrentFuel = 100f,
                MaxFuel = 100f
            });

            // AI
            ecb.AddComponent(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = position
            });

            ecb.AddComponent(entity, new VesselMovement
            {
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                IsMoving = 0
            });

            // Faction
            ecb.AddBuffer<AffiliationTag>(entity);
            AddFactionTag(ref state, ref ecb, entity, factionId);

            UnityDebug.Log($"[DevSpawnSystem] Created strike craft {craftId} ({templateId}) at {position}");
        }

        private void SetupSupportVessel(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string templateId, string factionId, float3 position, ref Random random)
        {
            var vesselId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            float speed = 10f;
            float cargoCapacity = 100f;
            float miningEfficiency = 0.8f;

            switch (templateId)
            {
                case "miner":
                    speed = 10f; cargoCapacity = 100f; miningEfficiency = 0.8f;
                    break;
                case "hauler":
                    speed = 8f; cargoCapacity = 500f; miningEfficiency = 0f;
                    break;
                case "repair_tender":
                    speed = 6f; cargoCapacity = 50f; miningEfficiency = 0f;
                    break;
            }

            if (templateId == "miner")
            {
                ecb.AddComponent(entity, new MiningVessel
                {
                    VesselId = new FixedString64Bytes(vesselId),
                    CarrierEntity = Entity.Null,
                    MiningEfficiency = miningEfficiency,
                    Speed = speed,
                    CargoCapacity = cargoCapacity,
                    CurrentCargo = 0f,
                    CargoResourceType = ResourceType.Minerals
                });

                ecb.AddComponent(entity, new MiningState
                {
                    Phase = MiningPhase.Idle,
                    ActiveTarget = Entity.Null,
                    MiningTimer = 0f,
                    TickInterval = 0.5f,
                    PhaseTimer = 0f
                });

                ecb.AddComponent(entity, new MiningOrder
                {
                    ResourceId = new FixedString64Bytes("space4x.resource.minerals"),
                    Source = MiningOrderSource.Scripted,
                    Status = MiningOrderStatus.Pending
                });
            }

            // AI
            ecb.AddComponent(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = position
            });

            ecb.AddComponent(entity, new VesselMovement
            {
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                IsMoving = 0
            });

            // Faction
            ecb.AddBuffer<AffiliationTag>(entity);
            AddFactionTag(ref state, ref ecb, entity, factionId);

            UnityDebug.Log($"[DevSpawnSystem] Created support vessel {vesselId} ({templateId}) at {position}");
        }

        private void SetupStation(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string templateId, string factionId, float3 position, ref Random random)
        {
            var stationId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            float shieldStrength = 2000f;
            float hullPoints = 10000f;
            int dockingCapacity = 12;

            switch (templateId)
            {
                case "outpost":
                    shieldStrength = 500f; hullPoints = 2000f; dockingCapacity = 4;
                    break;
            }

            ecb.AddComponent(entity, new Space4XShield
            {
                Type = ShieldType.Standard,
                Current = shieldStrength,
                Maximum = shieldStrength,
                RechargeRate = shieldStrength * 0.01f,
                RechargeDelay = 10,
                CurrentDelay = 0,
                EnergyResistance = (half)1f,
                ThermalResistance = (half)1f,
                EMResistance = (half)1f,
                RadiationResistance = (half)1f,
                KineticResistance = (half)1f,
                ExplosiveResistance = (half)1f
            });

            ecb.AddComponent(entity, new HullIntegrity
            {
                Current = hullPoints,
                Max = hullPoints,
                BaseMax = hullPoints
            });

            ecb.AddComponent(entity, new DockingCapacity
            {
                MaxLargeCraft = (byte)dockingCapacity,
                CurrentLargeCraft = 0
            });

            // Faction
            ecb.AddBuffer<AffiliationTag>(entity);
            AddFactionTag(ref state, ref ecb, entity, factionId);

            // Resources
            var storage = ecb.AddBuffer<ResourceStorage>(entity);
            storage.Add(ResourceStorage.Create(ResourceType.Food, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Water, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Supplies, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Fuel, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
            storage.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));

            UnityDebug.Log($"[DevSpawnSystem] Created station {stationId} ({templateId}) at {position}");
        }

        private static void SetupCelestial(ref EntityCommandBuffer ecb, Entity entity, string templateId, float3 position, ref Random random)
        {
            var celestialId = $"DEV_{templateId}_{random.NextUInt(10000)}";

            float resourceAmount = 500f;
            float miningRate = 10f;
            ResourceType resourceType = ResourceType.Minerals;

            switch (templateId)
            {
                case "asteroid_small":
                    resourceAmount = 200f; miningRate = 5f;
                    break;
                case "asteroid_large":
                    resourceAmount = 1000f; miningRate = 15f;
                    break;
                case "asteroid_rare":
                    resourceAmount = 300f; miningRate = 8f; resourceType = ResourceType.RareMetals;
                    break;
            }

            ecb.AddComponent(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes(celestialId),
                ResourceType = resourceType,
                ResourceAmount = resourceAmount,
                MaxResourceAmount = resourceAmount,
                MiningRate = miningRate
            });

            ecb.AddComponent(entity, new ResourceSourceState
            {
                UnitsRemaining = resourceAmount
            });

            ecb.AddComponent(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = miningRate,
                MaxSimultaneousWorkers = 4
            });

            UnityDebug.Log($"[DevSpawnSystem] Created celestial {celestialId} ({templateId}) at {position}");
        }

        private static void AddDefaultSubsystems(ref EntityCommandBuffer ecb, Entity entity, float hullMax)
        {
            var subsystems = ecb.AddBuffer<SubsystemHealth>(entity);
            var engineMax = math.max(5f, hullMax * 0.3f);
            var weaponMax = math.max(5f, hullMax * 0.2f);

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

            ecb.AddBuffer<SubsystemDisabled>(entity);
            ecb.AddBuffer<DamageScarEvent>(entity);
        }

        private void AddFactionTag(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return;
            }

            var affiliationName = new FixedString64Bytes(factionId);
            var affiliationEntity = ResolveAffiliationEntity(ref state, affiliationName);
            if (affiliationEntity == Entity.Null)
            {
                return;
            }

            AffiliationType affType = AffiliationType.Fleet;
            half loyalty = (half)1f;

            if (factionId == "enemy_pirates")
            {
                loyalty = (half)0.5f;
            }
            else if (factionId == "neutral_traders")
            {
                affType = AffiliationType.Faction;
                loyalty = (half)0.7f;
            }

            ecb.AppendToBuffer(entity, new AffiliationTag
            {
                Type = affType,
                Target = affiliationEntity,
                Loyalty = loyalty
            });
        }

        private Entity ResolveAffiliationEntity(ref SystemState state, FixedString64Bytes affiliationName)
        {
            if (affiliationName.Length == 0)
            {
                return Entity.Null;
            }

            if (_affiliationEntityCache.TryGetValue(affiliationName, out var cached))
            {
                if (state.EntityManager.Exists(cached))
                {
                    return cached;
                }

                _affiliationEntityCache.Remove(affiliationName);
            }

            var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AffiliationRelation>());
            using var relations = query.ToComponentDataArray<AffiliationRelation>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < relations.Length; i++)
            {
                if (!relations[i].AffiliationName.Equals(affiliationName))
                {
                    continue;
                }

                var existing = entities[i];
                _affiliationEntityCache[affiliationName] = existing;
                return existing;
            }

            var entity = state.EntityManager.CreateEntity(typeof(AffiliationRelation));
            state.EntityManager.SetComponentData(entity, new AffiliationRelation
            {
                AffiliationName = affiliationName
            });
            _affiliationEntityCache[affiliationName] = entity;
            return entity;
        }
    }
}
