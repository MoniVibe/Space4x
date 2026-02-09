using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    [UpdateBefore(typeof(Space4X.Registry.Space4XAuthoritySeatBootstrapSystem))]
    public partial struct Space4XScenarioSeedSystem : ISystem
    {
        private const string HivemindScenarioId = "space4x_hivemind_swarm_micro";
        private const string InfectedScenarioId = "space4x_infected_swarm_hivemind_micro";
        private const ushort FriendlyFactionId = 1;
        private const ushort HostileFactionId = 2;
        private const int SwarmCount = 1200;
        private Entity _friendlyAffiliationEntity;
        private Entity _hostileAffiliationEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            var scenarioId = scenarioInfo.ScenarioId.ToString();
            if (!IsCentralSeedScenarioId(scenarioId))
            {
                return;
            }

            if (IsScenarioSeeded(ref state, scenarioInfo.ScenarioId))
            {
                return;
            }

            var seed = scenarioInfo.Seed != 0u ? scenarioInfo.Seed : 4242u;
            var random = new Unity.Mathematics.Random(seed ^ 0x9E3779B9u);
            var friendlyFaction = EnsureScenarioAffiliation(ref state, 0);
            var hostileFaction = EnsureScenarioAffiliation(ref state, 1);
            EnsureScenarioFactionRelations(ref state, friendlyFaction, hostileFaction, timeState.Tick);

            var isInfectedScenario = scenarioId.Equals(InfectedScenarioId, System.StringComparison.OrdinalIgnoreCase);
            var heroCount = isInfectedScenario ? 1 : 2;
            var heroPositions = GetHeroPositions(heroCount);
            var hivePosition = new float3(220f, 0f, 0f);

            var hive = SpawnCarrier(
                ref state,
                new FixedString64Bytes("hivemind-core"),
                hivePosition,
                hostileFaction,
                scenarioSide: 1,
                lawfulness: -0.7f,
                hull: HullIntegrity.SuperCarrier,
                shieldCapacity: 900f,
                armorThickness: 65f,
                weaponPrimary: WeaponSize.Capital,
                weaponSecondary: WeaponSize.Large,
                targetProfile: TargetSelectionProfile.Balanced);

            var heroes = new NativeList<Entity>(heroCount, Allocator.Temp);
            for (var i = 0; i < heroPositions.Length; i++)
            {
                var hero = SpawnCarrier(
                    ref state,
                    new FixedString64Bytes($"hero-cruiser-{i + 1}"),
                    heroPositions[i],
                    friendlyFaction,
                    scenarioSide: 0,
                    lawfulness: 0.7f,
                    hull: HullIntegrity.HeavyCarrier,
                    shieldCapacity: 650f,
                    armorThickness: 55f,
                    weaponPrimary: WeaponSize.Capital,
                    weaponSecondary: WeaponSize.Medium,
                    targetProfile: TargetSelectionProfile.NeutralizeThreats);
                heroes.Add(hero);
            }

            var expireTick = scenarioInfo.RunTicks > 0
                ? timeState.Tick + (uint)scenarioInfo.RunTicks
                : 0u;
            for (var i = 0; i < heroes.Length; i++)
            {
                AddIntelFact(ref state, heroes[i], hive, timeState.Tick, expireTick);
            }

            SpawnSwarm(ref state, hive, hivePosition, SwarmCount, ref random);

            heroes.Dispose();
            MarkScenarioSeeded(ref state, scenarioInfo.ScenarioId);
        }

        private static bool IsCentralSeedScenarioId(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return false;
            }

            return scenarioId.Equals(HivemindScenarioId, System.StringComparison.OrdinalIgnoreCase) ||
                   scenarioId.Equals(InfectedScenarioId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static float3[] GetHeroPositions(int heroCount)
        {
            if (heroCount <= 1)
            {
                return new[] { new float3(-160f, 0f, 0f) };
            }

            return new[]
            {
                new float3(-180f, 0f, -22f),
                new float3(-180f, 0f, 22f)
            };
        }

        private static bool IsScenarioSeeded(ref SystemState state, in FixedString64Bytes scenarioId)
        {
            if (!SystemAPI.TryGetSingleton<Space4XScenarioSeeded>(out var seeded))
            {
                return false;
            }

            return seeded.ScenarioId.Equals(scenarioId);
        }

        private static void MarkScenarioSeeded(ref SystemState state, in FixedString64Bytes scenarioId)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XScenarioSeeded>(out var entity))
            {
                state.EntityManager.SetComponentData(entity, new Space4XScenarioSeeded { ScenarioId = scenarioId });
                return;
            }

            entity = state.EntityManager.CreateEntity(typeof(Space4XScenarioSeeded));
            state.EntityManager.SetComponentData(entity, new Space4XScenarioSeeded { ScenarioId = scenarioId });
        }

        private Entity EnsureScenarioAffiliation(ref SystemState state, byte scenarioSide)
        {
            if (scenarioSide == 1)
            {
                if (_hostileAffiliationEntity != Entity.Null && state.EntityManager.Exists(_hostileAffiliationEntity))
                {
                    return _hostileAffiliationEntity;
                }
            }
            else
            {
                if (_friendlyAffiliationEntity != Entity.Null && state.EntityManager.Exists(_friendlyAffiliationEntity))
                {
                    return _friendlyAffiliationEntity;
                }
            }

            var entity = state.EntityManager.CreateEntity(typeof(AffiliationRelation), typeof(Space4XFaction));
            var affiliationName = scenarioSide == 1 ? "Scenario-Hostile" : "Scenario-Friendly";
            state.EntityManager.SetComponentData(entity, new AffiliationRelation
            {
                AffiliationName = new FixedString64Bytes(affiliationName)
            });

            var factionId = scenarioSide == 1 ? HostileFactionId : FriendlyFactionId;
            var outlook = scenarioSide == 1 ? FactionOutlook.Militarist : FactionOutlook.Egalitarian;
            state.EntityManager.SetComponentData(entity, Space4XFaction.Empire(factionId, outlook));

            if (scenarioSide == 1)
            {
                _hostileAffiliationEntity = entity;
            }
            else
            {
                _friendlyAffiliationEntity = entity;
            }

            return entity;
        }

        private static void EnsureScenarioFactionRelations(ref SystemState state, Entity friendlyFaction, Entity hostileFaction, uint tick)
        {
            if (friendlyFaction == Entity.Null || hostileFaction == Entity.Null)
            {
                return;
            }

            EnsureFactionRelation(ref state, friendlyFaction, hostileFaction, -80, tick);
            EnsureFactionRelation(ref state, hostileFaction, friendlyFaction, -80, tick);
        }

        private static void EnsureFactionRelation(ref SystemState state, Entity selfFaction, Entity otherFaction, sbyte score, uint tick)
        {
            if (!state.EntityManager.HasComponent<Space4XFaction>(selfFaction) ||
                !state.EntityManager.HasComponent<Space4XFaction>(otherFaction))
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<FactionRelationEntry>(selfFaction))
            {
                state.EntityManager.AddBuffer<FactionRelationEntry>(selfFaction);
            }

            var otherProfile = state.EntityManager.GetComponentData<Space4XFaction>(otherFaction);
            var buffer = state.EntityManager.GetBuffer<FactionRelationEntry>(selfFaction);

            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (entry.Relation.OtherFaction == otherFaction || entry.Relation.OtherFactionId == otherProfile.FactionId)
                {
                    entry.Relation.OtherFaction = otherFaction;
                    entry.Relation.OtherFactionId = otherProfile.FactionId;
                    entry.Relation.Score = score;
                    entry.Relation.LastInteractionTick = tick;
                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new FactionRelationEntry
            {
                Relation = new FactionRelation
                {
                    OtherFaction = otherFaction,
                    OtherFactionId = otherProfile.FactionId,
                    Score = score,
                    Trust = (half)0f,
                    Fear = (half)0.5f,
                    Respect = (half)0f,
                    TradeVolume = 0f,
                    RecentCombats = 0,
                    LastInteractionTick = tick
                }
            });
        }

        private static void AddScenarioAffiliation(ref SystemState state, Entity entity, Entity affiliationEntity)
        {
            if (affiliationEntity == Entity.Null)
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<AffiliationTag>(entity))
            {
                state.EntityManager.AddBuffer<AffiliationTag>(entity);
            }

            var buffer = state.EntityManager.GetBuffer<AffiliationTag>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Target == affiliationEntity)
                {
                    return;
                }
            }

            buffer.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = affiliationEntity,
                Loyalty = (half)1f
            });
        }

        private Entity SpawnCarrier(
            ref SystemState state,
            FixedString64Bytes carrierId,
            float3 position,
            Entity affiliationEntity,
            byte scenarioSide,
            float lawfulness,
            HullIntegrity hull,
            float shieldCapacity,
            float armorThickness,
            WeaponSize weaponPrimary,
            WeaponSize weaponSecondary,
            TargetSelectionProfile targetProfile)
        {
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            state.EntityManager.AddComponentData(entity, new PostTransformMatrix
            {
                Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
            });
            state.EntityManager.AddComponent<SpatialIndexedTag>(entity);
            state.EntityManager.AddComponent<CommunicationModuleTag>(entity);
            state.EntityManager.AddComponentData(entity, MediumContext.Vacuum);
            state.EntityManager.AddComponent<CarrierTag>(entity);
            state.EntityManager.AddComponentData(entity, new CarrierHullId { HullId = new FixedString64Bytes("cv-mule") });

            var speed = scenarioSide == 1 ? 3.2f : 3.8f;
            var acceleration = 0.4f;
            var deceleration = 0.6f;
            var turnSpeed = 0.28f;
            var slowdown = 20f;
            var arrival = 3f;

            state.EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = carrierId,
                AffiliationEntity = affiliationEntity,
                Speed = speed,
                Acceleration = acceleration,
                Deceleration = deceleration,
                TurnSpeed = turnSpeed,
                SlowdownDistance = slowdown,
                ArrivalDistance = arrival,
                PatrolCenter = position,
                PatrolRadius = 60f
            });

            state.EntityManager.AddComponentData(entity, new ScenarioSide { Side = scenarioSide });
            state.EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
            AddScenarioAffiliation(ref state, entity, affiliationEntity);

            var disposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            if (scenarioSide == 1)
            {
                disposition |= EntityDispositionFlags.Hostile;
            }
            state.EntityManager.AddComponentData(entity, new EntityDisposition { Flags = disposition });

            state.EntityManager.AddComponentData(entity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 3f,
                WaitTimer = 0f
            });
            state.EntityManager.AddComponentData(entity, new MovementCommand
            {
                TargetPosition = position,
                ArrivalThreshold = 2f
            });
            state.EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                Acceleration = acceleration,
                Deceleration = deceleration,
                TurnSpeed = turnSpeed,
                SlowdownDistance = slowdown,
                ArrivalDistance = arrival,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });
            state.EntityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });
            state.EntityManager.AddComponentData(entity, new EntityIntent
            {
                Mode = IntentMode.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 0,
                Priority = InterruptPriority.Low,
                IsValid = 0
            });
            state.EntityManager.AddBuffer<Interrupt>(entity);

            state.EntityManager.AddComponentData(entity, new VesselPhysicalProperties
            {
                Radius = 2.6f,
                BaseMass = 120f,
                HullDensity = 1.2f,
                CargoMassPerUnit = 0.02f,
                Restitution = 0.08f,
                TangentialDamping = 0.25f
            });

            state.EntityManager.AddComponentData(entity, hull);
            EnsureDefaultSubsystems(ref state, entity, hull.Max);
            state.EntityManager.AddComponentData(entity, Space4XShield.Standard(shieldCapacity));
            state.EntityManager.AddComponentData(entity, Space4XArmor.Standard(armorThickness));
            state.EntityManager.AddComponentData(entity, new Space4XEngagement
            {
                PrimaryTarget = Entity.Null,
                Phase = EngagementPhase.None,
                TargetDistance = 0f,
                EngagementDuration = 0u,
                DamageDealt = 0f,
                DamageReceived = 0f,
                FormationBonus = (half)0f,
                EvasionModifier = (half)0f
            });

            state.EntityManager.AddBuffer<DamageEvent>(entity);

            var weapons = state.EntityManager.AddBuffer<WeaponMount>(entity);
            AddWeapon(weapons, Space4XWeapon.Laser(weaponPrimary));
            AddWeapon(weapons, Space4XWeapon.Kinetic(weaponSecondary));

            state.EntityManager.AddComponentData(entity, targetProfile);
            state.EntityManager.AddComponentData(entity, new TargetPriority
            {
                CurrentTarget = Entity.Null,
                CurrentScore = 0f,
                LastEvaluationTick = 0u,
                EngagementDuration = 0f,
                ForceReevaluate = 1
            });
            state.EntityManager.AddBuffer<TargetCandidate>(entity);
            state.EntityManager.AddBuffer<DamageHistory>(entity);

            state.EntityManager.AddComponentData(entity, new CaptainOrder
            {
                Type = CaptainOrderType.None,
                Status = CaptainOrderStatus.None,
                Priority = 0,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                IssuedTick = 0u,
                TimeoutTick = 0u,
                IssuingAuthority = Entity.Null
            });
            state.EntityManager.AddComponentData(entity, CaptainState.Default);
            state.EntityManager.AddComponentData(entity, CaptainReadiness.Standard);
            state.EntityManager.AddBuffer<PlatformCrewMember>(entity);
            state.EntityManager.AddComponentData(entity, SupplyStatus.DefaultCarrier);

            return entity;
        }

        private static void AddWeapon(DynamicBuffer<WeaponMount> buffer, Space4XWeapon weapon)
        {
            buffer.Add(new WeaponMount
            {
                Weapon = weapon,
                CurrentTarget = Entity.Null,
                FireArcCenterOffsetDeg = 0f,
                IsEnabled = 1,
                ShotsFired = 0,
                ShotsHit = 0,
                SourceModule = Entity.Null,
                CoolingRating = (half)1f,
                Heat01 = 0f,
                HeatCapacity = 100f,
                HeatDissipation = 4f,
                HeatPerShot = 2f
            });
        }

        private static void EnsureDefaultSubsystems(ref SystemState state, Entity entity, float hullMax)
        {
            var subsystems = state.EntityManager.HasBuffer<SubsystemHealth>(entity)
                ? state.EntityManager.GetBuffer<SubsystemHealth>(entity)
                : state.EntityManager.AddBuffer<SubsystemHealth>(entity);

            if (subsystems.Length == 0)
            {
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
            }

            if (!state.EntityManager.HasBuffer<SubsystemDisabled>(entity))
            {
                state.EntityManager.AddBuffer<SubsystemDisabled>(entity);
            }

            if (!state.EntityManager.HasBuffer<DamageScarEvent>(entity))
            {
                state.EntityManager.AddBuffer<DamageScarEvent>(entity);
            }
        }

        private static void AddIntelFact(ref SystemState state, Entity hero, Entity target, uint tick, uint expireTick)
        {
            if (!state.EntityManager.HasBuffer<IntelTargetFact>(hero))
            {
                state.EntityManager.AddBuffer<IntelTargetFact>(hero);
            }

            var buffer = state.EntityManager.GetBuffer<IntelTargetFact>(hero);
            buffer.Add(new IntelTargetFact
            {
                Target = target,
                Type = IntelFactType.CommandNode,
                Confidence = (half)0.9f,
                Weight = (half)1.0f,
                LastSeenTick = tick,
                ExpireTick = expireTick
            });
        }

        private static void SpawnSwarm(ref SystemState state, Entity carrierEntity, float3 carrierPosition, int count, ref Unity.Mathematics.Random random)
        {
            var role = StrikeCraftRole.Fighter;
            var craftSpeed = 12f;
            var weaponDamage = 10f;
            var weaponRange = 20f;
            var hull = HullIntegrity.LightCraft;

            var minRadius = 50f;
            var maxRadius = 160f;
            var heightRange = 12f;

            for (int i = 0; i < count; i++)
            {
                var radius = random.NextFloat(minRadius, maxRadius);
                var angle = random.NextFloat(0f, math.PI * 2f);
                var height = random.NextFloat(-heightRange, heightRange);
                var offset = new float3(radius * math.cos(angle), height, radius * math.sin(angle));
                var position = carrierPosition + offset;

                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                state.EntityManager.AddComponent<CommunicationModuleTag>(entity);
                state.EntityManager.AddComponentData(entity, MediumContext.Vacuum);
                state.EntityManager.AddComponentData(entity, hull);
                EnsureDefaultSubsystems(ref state, entity, hull.Max);

                var weaponBuffer = state.EntityManager.AddBuffer<WeaponMount>(entity);
                weaponBuffer.Add(new WeaponMount
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

                state.EntityManager.AddComponentData(entity, StrikeCraftProfile.Create(role, carrierEntity));
                state.EntityManager.AddComponentData(entity, new StrikeCraftState
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
                state.EntityManager.AddComponent<StrikeCraftDogfightTag>(entity);
                state.EntityManager.AddComponentData(entity, AttackRunConfig.ForRole(role));
                state.EntityManager.AddComponentData(entity, StrikeCraftExperience.Rookie);
                state.EntityManager.AddComponentData(entity, new ScenarioSide { Side = 1 });
                state.EntityManager.AddComponentData(entity, new VesselMovement
                {
                    BaseSpeed = craftSpeed,
                    CurrentSpeed = 0f,
                    Velocity = float3.zero,
                    TurnSpeed = 4.5f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });
                state.EntityManager.AddComponentData(entity, SupplyStatus.DefaultStrikeCraft);
                state.EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(-0.7f, 0f, 0f));
                AddScenarioAffiliation(ref state, entity, carrierEntity != Entity.Null
                    ? state.EntityManager.GetComponentData<Carrier>(carrierEntity).AffiliationEntity
                    : Entity.Null);

                state.EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military | EntityDispositionFlags.Hostile
                });

                var pilot = CreatePilotEntity(ref state, -0.7f);
                state.EntityManager.AddComponentData(entity, new StrikeCraftPilotLink
                {
                    Pilot = pilot
                });
            }
        }

        private static Entity CreatePilotEntity(ref SystemState state, float lawfulness)
        {
            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var pilot = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(pilot, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));

            state.EntityManager.AddBuffer<StanceEntry>(pilot);
            state.EntityManager.AddBuffer<TopStance>(pilot);

            var stanceEntries = state.EntityManager.GetBuffer<StanceEntry>(pilot);
            var topStances = state.EntityManager.GetBuffer<TopStance>(pilot);
            var stanceId = config.NeutralStance;
            if (lawfulness >= config.LoyalistLawThreshold)
            {
                stanceId = config.FriendlyStance;
            }
            else if (lawfulness <= config.MutinousLawThreshold)
            {
                stanceId = config.HostileStance;
            }

            stanceEntries.Add(new StanceEntry
            {
                StanceId = stanceId,
                Weight = (half)1f
            });
            topStances.Add(new TopStance
            {
                StanceId = stanceId,
                Weight = (half)1f
            });

            state.EntityManager.AddComponentData(pilot, new BehaviorDispositionSeedRequest
            {
                Seed = 0u,
                SeedSalt = 0u
            });

            return pilot;
        }
    }

    public struct Space4XScenarioSeeded : IComponentData
    {
        public FixedString64Bytes ScenarioId;
    }
}
