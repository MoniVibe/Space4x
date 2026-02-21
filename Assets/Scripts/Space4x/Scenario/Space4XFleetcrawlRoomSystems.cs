using System;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4x.Scenario
{
    public enum Space4XFleetcrawlRoomKind : byte { Combat = 0, Relief = 1, Boss = 2 }
    public enum Space4XFleetcrawlBoonType : byte { ChainLightning = 0, MissileVolley = 1, BoostCooldown = 2, ShieldRegen = 3 }
    public enum Space4XRunGateKind : byte { Boon = 0, Blueprint = 1, Relief = 2 }
    public enum Space4XRunRewardKind : byte { None = 0, Boon = 1, ModuleBlueprint = 2, ManufacturerUnlock = 3, PartUnlock = 4, Currency = 5, Heal = 6, Reroll = 7 }
    public enum Space4XRunPerkOpKind : byte { AddStat = 0, MulStat = 1, AddTag = 2, RemoveTag = 3, ConvertDamage = 4, ReplaceAttackFamily = 5 }
    public enum Space4XRunBlueprintKind : byte { Weapon = 0, Reactor = 1, Hangar = 2 }
    public enum Space4XRunStatKind : byte { Damage = 0, Cooldown = 1, ShieldRegen = 2 }
    public enum Space4XRunStatTarget : byte { AllWeapons = 0, BeamWeapons = 1, Drones = 2 }

    public struct Space4XFleetcrawlSeeded : IComponentData { public FixedString64Bytes ScenarioId; }
    public struct Space4XRunPlayerTag : IComponentData { }
    public struct PlayerFlagshipTag : IComponentData { }
    public struct Space4XRunDroneTag : IComponentData { }
    public struct Space4XRunEnemyTag : IComponentData { public int RoomIndex; public int WaveIndex; }
    public struct Space4XRunEnemyDestroyedCounted : IComponentData { }
    public struct Space4XRunChainLightningSource : IComponentData { }
    public struct Space4XRunDamageEventCursor : IComponentData { public int ProcessedCount; }
    public struct RunCurrency : IComponentData { public int Value; }
    public struct Space4XRunRerollTokens : IComponentData { public int Value; }
    public struct Space4XRunReactiveModifiers : IComponentData { public float DamageMul; public float CooldownMul; }
    public struct Space4XRunBoonSet : IComponentData { public byte ChainLightning; public byte MissileVolley; public byte BoostCooldown; public byte ShieldRegen; }

    [InternalBufferCapacity(8)]
    public struct Space4XRunPerkOp : IBufferElementData
    {
        public FixedString64Bytes PerkId;
        public Space4XRunPerkOpKind Kind;
        public Space4XDamageType FromDamageType;
        public Space4XDamageType ToDamageType;
        public WeaponType FromWeaponType;
        public WeaponType ToWeaponType;
        public Space4XRunStatKind Stat;
        public Space4XRunStatTarget Target;
        public float Value;
        public byte Stacks;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XRunInstalledBlueprint : IBufferElementData
    {
        public FixedString64Bytes BlueprintId;
        public FixedString64Bytes BaseModuleId;
        public FixedString64Bytes ManufacturerId;
        public FixedString64Bytes PartA;
        public FixedString64Bytes PartB;
        public Space4XRunBlueprintKind Kind;
        public byte Version;
    }

    [InternalBufferCapacity(4)]
    public struct Space4XRunUnlockedManufacturer : IBufferElementData
    {
        public FixedString64Bytes ManufacturerId;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XRunUnlockedPart : IBufferElementData
    {
        public FixedString64Bytes PartId;
    }

    [InternalBufferCapacity(16)]
    public struct Space4XRunGateRewardRecord : IBufferElementData
    {
        public int RoomIndex;
        public Space4XRunGateKind GateKind;
        public Space4XRunRewardKind RewardKind;
        public FixedString64Bytes RewardId;
    }

    public struct Space4XFleetcrawlDirectorState : IComponentData
    {
        public byte Initialized;
        public int CurrentRoomIndex;
        public uint RoomStartTick;
        public uint RoomEndTick;
        public uint NextWaveTick;
        public int WavesSpawnedInRoom;
        public int EnemiesSpawnedInRoom;
        public int EnemiesDestroyedInRoom;
        public float DamageSnapshotAtRoomStart;
        public uint StableDigest;
        public uint Seed;
        public byte RunCompleted;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XFleetcrawlRoom : IBufferElementData
    {
        public Space4XFleetcrawlRoomKind Kind;
        public uint DurationTicks;
        public uint WaveIntervalTicks;
        public int PlannedWaves;
        public int RewardCurrency;
        public float RewardHealRatio;
        public float RewardPomPct;
        public float RewardMaxHullFlat;
        public byte OfferBoon;
    }

    internal static class Space4XFleetcrawlSpawnUtil
    {
        public static Entity SpawnCarrier(ref SystemState state, float3 position, byte side, in FixedString64Bytes id, bool playerTag, int roomIndex, int waveIndex)
        {
            var em = state.EntityManager;
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.AddComponentData(e, new PostTransformMatrix { Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f)) });
            em.AddComponent<SpatialIndexedTag>(e);
            em.AddComponent<CommunicationModuleTag>(e);
            em.AddComponentData(e, MediumContext.Vacuum);
            em.AddComponent<CarrierTag>(e);
            em.AddComponentData(e, new CarrierHullId { HullId = new FixedString64Bytes("cv-mule") });
            em.AddComponentData(e, new Carrier
            {
                CarrierId = id,
                AffiliationEntity = Entity.Null,
                Speed = side == 1 ? 3.2f : 4f,
                Acceleration = 0.42f,
                Deceleration = 0.62f,
                TurnSpeed = 0.3f,
                SlowdownDistance = 20f,
                ArrivalDistance = 3f,
                PatrolCenter = position,
                PatrolRadius = 60f
            });
            em.AddComponentData(e, new Space4XFleet { FleetId = id, ShipCount = 1, Posture = Space4XFleetPosture.Engaging, TaskForce = 0 });
            em.AddComponentData(e, new ScenarioSide { Side = side });
            em.AddComponentData(e, AlignmentTriplet.FromFloats(side == 1 ? -0.7f : 0.7f, 0f, 0f));
            var disposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            if (side == 1) disposition |= EntityDispositionFlags.Hostile;
            em.AddComponentData(e, new EntityDisposition { Flags = disposition });
            em.AddComponentData(e, new PatrolBehavior { CurrentWaypoint = position, WaitTime = 2f, WaitTimer = 0f });
            em.AddComponentData(e, new MovementCommand { TargetPosition = position, ArrivalThreshold = 2f });
            em.AddComponentData(e, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = side == 1 ? 3.2f : 4f,
                CurrentSpeed = 0f,
                Acceleration = 0.42f,
                Deceleration = 0.62f,
                TurnSpeed = 0.3f,
                SlowdownDistance = 20f,
                ArrivalDistance = 3f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });
            em.AddComponentData(e, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Patrol,
                TargetEntity = Entity.Null,
                TargetPosition = position,
                StateTimer = 0f,
                StateStartTick = 0
            });
            em.AddComponentData(e, new EntityIntent { Mode = IntentMode.Idle, TargetEntity = Entity.Null, TargetPosition = position, TriggeringInterrupt = InterruptType.None, IntentSetTick = 0, Priority = InterruptPriority.Low, IsValid = 0 });
            em.AddBuffer<Interrupt>(e);

            var hull = HullIntegrity.HeavyCarrier;
            em.AddComponentData(e, hull);
            em.AddComponentData(e, Space4XShield.Standard(side == 1 ? 620f : 680f));
            em.AddComponentData(e, Space4XArmor.Standard(side == 1 ? 52f : 58f));
            em.AddComponentData(e, SupplyStatus.DefaultCarrier);
            EnsureSubsystems(ref state, e, hull.Max);
            em.AddComponentData(e, new Space4XEngagement
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
            em.AddBuffer<DamageEvent>(e);
            var weapons = em.AddBuffer<WeaponMount>(e);
            AddWeapon(weapons, Space4XWeapon.Laser(side == 1 ? WeaponSize.Medium : WeaponSize.Large));
            AddWeapon(weapons, side == 1 ? Space4XWeapon.Kinetic(WeaponSize.Medium) : Space4XWeapon.Missile(WeaponSize.Medium));
            em.AddComponentData(e, side == 1 ? TargetSelectionProfile.NeutralizeThreats : TargetSelectionProfile.Balanced);
            em.AddComponentData(e, new TargetPriority { CurrentTarget = Entity.Null, CurrentScore = 0f, LastEvaluationTick = 0, EngagementDuration = 0f, ForceReevaluate = 1 });
            em.AddBuffer<TargetCandidate>(e);
            em.AddBuffer<DamageHistory>(e);

            if (playerTag) em.AddComponent<Space4XRunPlayerTag>(e);
            if (playerTag && side == 0) em.AddComponent<PlayerFlagshipTag>(e);
            if (side == 1)
            {
                em.AddComponentData(e, new Space4XRunEnemyTag { RoomIndex = roomIndex, WaveIndex = waveIndex });
                em.AddComponentData(e, new Space4XRunDamageEventCursor { ProcessedCount = 0 });
            }

            return e;
        }

        public static void SpawnStrikeWing(ref SystemState state, float3 anchor, byte side, int count, bool playerTag, int roomIndex, int waveIndex, bool droneTag = false)
        {
            var em = state.EntityManager;
            for (var i = 0; i < count; i++)
            {
                var angle = (i / math.max(1f, count)) * (math.PI * 2f);
                var radius = 14f + (i % 5) * 3f;
                var pos = anchor + new float3(math.cos(angle) * radius, ((i % 4) - 1.5f) * 0.8f, math.sin(angle) * radius);
                var e = em.CreateEntity();
                em.AddComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
                em.AddComponent<CommunicationModuleTag>(e);
                em.AddComponentData(e, MediumContext.Vacuum);
                var hull = side == 1 ? HullIntegrity.Create(48f, 0.08f) : HullIntegrity.Create(58f, 0.12f);
                em.AddComponentData(e, hull);
                em.AddComponentData(e, Space4XShield.Standard(side == 1 ? 35f : 45f));
                em.AddComponentData(e, Space4XArmor.Standard(side == 1 ? 6f : 8f));
                em.AddComponentData(e, SupplyStatus.DefaultStrikeCraft);
                EnsureSubsystems(ref state, e, hull.Max);
                var weapons = em.AddBuffer<WeaponMount>(e);
                AddWeapon(weapons, side == 1 ? Space4XWeapon.Laser(WeaponSize.Small) : Space4XWeapon.Kinetic(WeaponSize.Small));
                em.AddComponentData(e, StrikeCraftProfile.Create(StrikeCraftRole.Fighter, Entity.Null));
                em.AddComponentData(e, new StrikeCraftState
                {
                    CurrentState = StrikeCraftState.State.Approaching,
                    TargetEntity = Entity.Null,
                    TargetPosition = pos,
                    Experience = 0f,
                    StateStartTick = 0,
                    KamikazeActive = 0,
                    KamikazeStartTick = 0,
                    DogfightPhase = StrikeCraftDogfightPhase.Approach,
                    DogfightPhaseStartTick = 0,
                    DogfightLastFireTick = 0,
                    DogfightWingLeader = Entity.Null
                });
                em.AddComponent<StrikeCraftDogfightTag>(e);
                em.AddComponentData(e, AttackRunConfig.ForRole(StrikeCraftRole.Fighter));
                em.AddComponentData(e, StrikeCraftExperience.Rookie);
                em.AddComponentData(e, new ScenarioSide { Side = side });
                em.AddComponentData(e, AlignmentTriplet.FromFloats(side == 1 ? -0.7f : 0.7f, 0f, 0f));
                var disp = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                if (side == 1) disp |= EntityDispositionFlags.Hostile;
                em.AddComponentData(e, new EntityDisposition { Flags = disp });
                em.AddComponentData(e, new VesselMovement { BaseSpeed = side == 1 ? 12f : 13f, CurrentSpeed = 0f, Velocity = float3.zero, TurnSpeed = 4.5f, DesiredRotation = quaternion.identity, IsMoving = 0, LastMoveTick = 0 });
                em.AddComponentData(e, new Space4XEngagement { PrimaryTarget = Entity.Null, Phase = EngagementPhase.None, TargetDistance = 0f, EngagementDuration = 0u, DamageDealt = 0f, DamageReceived = 0f, FormationBonus = (half)0f, EvasionModifier = (half)0f });
                em.AddBuffer<DamageEvent>(e);
                em.AddComponentData(e, side == 1 ? TargetSelectionProfile.NeutralizeThreats : TargetSelectionProfile.Balanced);
                em.AddComponentData(e, new TargetPriority { CurrentTarget = Entity.Null, CurrentScore = 0f, LastEvaluationTick = 0, EngagementDuration = 0f, ForceReevaluate = 1 });
                em.AddBuffer<TargetCandidate>(e);
                em.AddBuffer<DamageHistory>(e);
                if (playerTag) em.AddComponent<Space4XRunPlayerTag>(e);
                if (playerTag && droneTag) em.AddComponent<Space4XRunDroneTag>(e);
                if (side == 1)
                {
                    em.AddComponentData(e, new Space4XRunEnemyTag { RoomIndex = roomIndex, WaveIndex = waveIndex });
                    em.AddComponentData(e, new Space4XRunDamageEventCursor { ProcessedCount = 0 });
                }
            }
        }

        private static void AddWeapon(DynamicBuffer<WeaponMount> buffer, in Space4XWeapon weapon)
        {
            buffer.Add(new WeaponMount
            {
                Weapon = weapon,
                CurrentTarget = Entity.Null,
                FireArcCenterOffsetDeg = (half)0f,
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

        private static void EnsureSubsystems(ref SystemState state, Entity entity, float hullMax)
        {
            var em = state.EntityManager;
            var subsystems = em.HasBuffer<SubsystemHealth>(entity) ? em.GetBuffer<SubsystemHealth>(entity) : em.AddBuffer<SubsystemHealth>(entity);
            if (subsystems.Length == 0)
            {
                var e = math.max(5f, hullMax * 0.3f);
                var w = math.max(5f, hullMax * 0.2f);
                subsystems.Add(new SubsystemHealth { Type = SubsystemType.Engines, Current = e, Max = e, RegenPerTick = math.max(0.01f, e * 0.005f), Flags = SubsystemFlags.None });
                subsystems.Add(new SubsystemHealth { Type = SubsystemType.Weapons, Current = w, Max = w, RegenPerTick = math.max(0.01f, w * 0.005f), Flags = SubsystemFlags.None });
            }
            if (!em.HasBuffer<SubsystemDisabled>(entity)) em.AddBuffer<SubsystemDisabled>(entity);
            if (!em.HasBuffer<DamageScarEvent>(entity)) em.AddBuffer<DamageScarEvent>(entity);
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningScenarioSystem))]
    public partial struct Space4XFleetcrawlBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var info = SystemAPI.GetSingleton<ScenarioInfo>();
            var scenarioId = info.ScenarioId.ToString();
            if (string.IsNullOrWhiteSpace(scenarioId) || !scenarioId.StartsWith("space4x_fleetcrawl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<Space4XFleetcrawlSeeded>(out _))
            {
                return;
            }

            var dt = math.max(1e-6f, ResolveFixedDelta(SystemAPI.GetSingleton<TimeState>()));
            var shortMode = scenarioId.Contains("micro", StringComparison.OrdinalIgnoreCase) || IsTruthy(Environment.GetEnvironmentVariable("SPACE4X_FLEETCRAWL_SHORT"));
            var combatDur = shortMode ? 45f : 300f;
            var reliefDur = shortMode ? 20f : 45f;
            var bossDur = shortMode ? 75f : 420f;
            var waveInt = shortMode ? 10f : 45f;

            var directorEntity = state.EntityManager.CreateEntity(
                typeof(Space4XFleetcrawlDirectorState),
                typeof(RunCurrency),
                typeof(Space4XRunRerollTokens),
                typeof(Space4XRunReactiveModifiers),
                typeof(Space4XRunBoonSet));
            var rooms = state.EntityManager.AddBuffer<Space4XFleetcrawlRoom>(directorEntity);
            state.EntityManager.AddBuffer<Space4XRunPerkOp>(directorEntity);
            state.EntityManager.AddBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            state.EntityManager.AddBuffer<Space4XRunUnlockedManufacturer>(directorEntity);
            state.EntityManager.AddBuffer<Space4XRunUnlockedPart>(directorEntity);
            state.EntityManager.AddBuffer<Space4XRunGateRewardRecord>(directorEntity);
            rooms.Add(new Space4XFleetcrawlRoom { Kind = Space4XFleetcrawlRoomKind.Combat, DurationTicks = ToTicks(combatDur, dt), WaveIntervalTicks = ToTicks(waveInt, dt), PlannedWaves = 4, RewardCurrency = 80, RewardHealRatio = 0.06f, RewardPomPct = 0.05f, RewardMaxHullFlat = 20f, OfferBoon = 1 });
            rooms.Add(new Space4XFleetcrawlRoom { Kind = Space4XFleetcrawlRoomKind.Relief, DurationTicks = ToTicks(reliefDur, dt), WaveIntervalTicks = 0, PlannedWaves = 0, RewardCurrency = 40, RewardHealRatio = 0.08f, RewardPomPct = 0f, RewardMaxHullFlat = 0f, OfferBoon = 0 });
            rooms.Add(new Space4XFleetcrawlRoom { Kind = Space4XFleetcrawlRoomKind.Combat, DurationTicks = ToTicks(combatDur, dt), WaveIntervalTicks = ToTicks(waveInt, dt), PlannedWaves = 5, RewardCurrency = 120, RewardHealRatio = 0.05f, RewardPomPct = 0.08f, RewardMaxHullFlat = 30f, OfferBoon = 1 });
            rooms.Add(new Space4XFleetcrawlRoom { Kind = Space4XFleetcrawlRoomKind.Boss, DurationTicks = ToTicks(bossDur, dt), WaveIntervalTicks = ToTicks(waveInt, dt), PlannedWaves = 6, RewardCurrency = 220, RewardHealRatio = 0.12f, RewardPomPct = 0.1f, RewardMaxHullFlat = 60f, OfferBoon = 1 });
            rooms.Add(new Space4XFleetcrawlRoom { Kind = Space4XFleetcrawlRoomKind.Relief, DurationTicks = ToTicks(reliefDur, dt), WaveIntervalTicks = 0, PlannedWaves = 0, RewardCurrency = 60, RewardHealRatio = 0.1f, RewardPomPct = 0f, RewardMaxHullFlat = 0f, OfferBoon = 0 });

            var flagship = Space4XFleetcrawlSpawnUtil.SpawnCarrier(ref state, new float3(-120f, 0f, 0f), 0, new FixedString64Bytes("player-flagship"), true, -1, -1);
            if (!state.EntityManager.HasComponent<PlayerFlagshipTag>(flagship))
            {
                state.EntityManager.AddComponent<PlayerFlagshipTag>(flagship);
            }

            Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, new float3(-120f, 0f, 0f), 0, 6, true, -1, -1);

            var runSeed = info.Seed == 0u ? 9017u : info.Seed;
            state.EntityManager.SetComponentData(directorEntity, new Space4XFleetcrawlDirectorState { Initialized = 0, CurrentRoomIndex = -1, StableDigest = math.hash(new uint2(1u, runSeed)), Seed = runSeed });
            state.EntityManager.SetComponentData(directorEntity, new RunCurrency { Value = 0 });
            state.EntityManager.SetComponentData(directorEntity, new Space4XRunRerollTokens { Value = 0 });
            state.EntityManager.SetComponentData(directorEntity, new Space4XRunReactiveModifiers { DamageMul = 1f, CooldownMul = 1f });
            state.EntityManager.SetComponentData(directorEntity, new Space4XRunBoonSet());

            var seed = state.EntityManager.CreateEntity(typeof(Space4XFleetcrawlSeeded));
            state.EntityManager.SetComponentData(seed, new Space4XFleetcrawlSeeded { ScenarioId = info.ScenarioId });
            Debug.Log($"[Fleetcrawl] Seeded run. rooms={rooms.Length} short_mode={(shortMode ? 1 : 0)} seed={runSeed}.");
        }

        private static uint ToTicks(float sec, float dt) => (uint)math.max(1f, math.round(math.max(0.01f, sec) / math.max(1e-6f, dt)));

        private float ResolveFixedDelta(in TimeState time)
        {
            if (time.FixedDeltaTime > 0f)
            {
                return time.FixedDeltaTime;
            }

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState) && tickTimeState.FixedDeltaTime > 0f)
            {
                return tickTimeState.FixedDeltaTime;
            }

            return 1f / 60f;
        }

        private static bool IsTruthy(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XCombatTelemetrySystem))]
    public partial struct Space4XFleetcrawlRoomDirectorSystem : ISystem
    {
        private const int StrikeCraftKillBounty = 3;
        private const int CarrierKillBounty = 12;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<Space4XFleetcrawlRoom>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused || !SystemAPI.TryGetSingletonEntity<Space4XFleetcrawlDirectorState>(out var directorEntity))
            {
                return;
            }

            var director = state.EntityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            if (director.RunCompleted != 0)
            {
                return;
            }

            var rooms = state.EntityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
            if (rooms.Length == 0)
            {
                return;
            }

            var dt = ResolveFixedDelta(time);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var bountyEarned = 0;
            foreach (var (hull, entity) in SystemAPI.Query<RefRO<HullIntegrity>>().WithAll<Space4XRunEnemyTag>().WithNone<Space4XRunEnemyDestroyedCounted>().WithEntityAccess())
            {
                if (hull.ValueRO.Current > 0f)
                {
                    continue;
                }

                director.EnemiesDestroyedInRoom++;
                bountyEarned += SystemAPI.HasComponent<CarrierTag>(entity) ? CarrierKillBounty : StrikeCraftKillBounty;
                ecb.AddComponent<Space4XRunEnemyDestroyedCounted>(entity);
            }

            if (bountyEarned > 0)
            {
                var currency = state.EntityManager.GetComponentData<RunCurrency>(directorEntity);
                currency.Value += bountyEarned;
                state.EntityManager.SetComponentData(directorEntity, currency);
            }

            if (director.Initialized == 0)
            {
                StartRoom(ref state, ref director, rooms, 0, time.Tick, dt);
                state.EntityManager.SetComponentData(directorEntity, director);
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            var room = rooms[director.CurrentRoomIndex];
            if ((room.Kind == Space4XFleetcrawlRoomKind.Combat || room.Kind == Space4XFleetcrawlRoomKind.Boss) &&
                time.Tick >= director.NextWaveTick &&
                time.Tick < director.RoomEndTick &&
                director.WavesSpawnedInRoom < room.PlannedWaves)
            {
                SpawnWave(ref state, ref director, room, director.CurrentRoomIndex, director.WavesSpawnedInRoom + 1, time.Tick);
                director.NextWaveTick = time.Tick + math.max(1u, room.WaveIntervalTicks);
            }

            if (time.Tick >= director.RoomEndTick)
            {
                FinalizeRoom(ref state, ref director, room, dt);
                foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XRunEnemyTag>>().WithEntityAccess())
                {
                    ecb.DestroyEntity(entity);
                }

                var nextRoom = director.CurrentRoomIndex + 1;
                if (nextRoom >= rooms.Length)
                {
                    director.RunCompleted = 1;
                    Debug.Log($"[Fleetcrawl] Run complete. currency={state.EntityManager.GetComponentData<RunCurrency>(directorEntity).Value} digest={director.StableDigest}.");
                }
                else
                {
                    StartRoom(ref state, ref director, rooms, nextRoom, time.Tick, dt);
                }
            }

            state.EntityManager.SetComponentData(directorEntity, director);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void StartRoom(ref SystemState state, ref Space4XFleetcrawlDirectorState director, DynamicBuffer<Space4XFleetcrawlRoom> rooms, int roomIndex, uint tick, float dt)
        {
            var room = rooms[roomIndex];
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            if (state.EntityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity))
            {
                state.EntityManager.RemoveComponent<Space4XRunPendingGatePick>(directorEntity);
            }
            if (state.EntityManager.HasComponent<Space4XRunPendingBoonPick>(directorEntity))
            {
                state.EntityManager.RemoveComponent<Space4XRunPendingBoonPick>(directorEntity);
            }

            director.Initialized = 1;
            director.CurrentRoomIndex = roomIndex;
            director.RoomStartTick = tick;
            director.RoomEndTick = tick + math.max(1u, room.DurationTicks);
            director.WavesSpawnedInRoom = 0;
            director.EnemiesSpawnedInRoom = 0;
            director.EnemiesDestroyedInRoom = 0;
            director.DamageSnapshotAtRoomStart = SumPlayerDamage(ref state);
            director.NextWaveTick = director.RoomEndTick;

            if (room.Kind == Space4XFleetcrawlRoomKind.Combat || room.Kind == Space4XFleetcrawlRoomKind.Boss)
            {
                SpawnWave(ref state, ref director, room, roomIndex, 1, tick);
                director.NextWaveTick = tick + math.max(1u, room.WaveIntervalTicks);
            }

            Debug.Log($"[Fleetcrawl] ROOM_START index={roomIndex} kind={room.Kind} duration_s={(director.RoomEndTick - director.RoomStartTick) * dt:0.0} waves={room.PlannedWaves}.");
        }

        private void SpawnWave(ref SystemState state, ref Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int roomIndex, int waveIndex, uint tick)
        {
            var anchor = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerFlagshipTag>())
            {
                anchor = transform.ValueRO.Position;
                break;
            }

            var carrierCount = 0;
            var strikeCount = 0;
            if (room.Kind == Space4XFleetcrawlRoomKind.Boss)
            {
                if (waveIndex == 1)
                {
                    Space4XFleetcrawlSpawnUtil.SpawnCarrier(ref state, anchor + new float3(90f, 0f, 0f), 1, new FixedString64Bytes("boss-core"), false, roomIndex, waveIndex);
                    carrierCount++;
                    Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(90f, 0f, 0f), 1, 10, false, roomIndex, waveIndex);
                    strikeCount += 10;
                }
                else
                {
                    Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(70f, 0f, 0f), 1, 6, false, roomIndex, waveIndex);
                    strikeCount += 6;
                }
            }
            else
            {
                if (waveIndex <= 2)
                {
                    Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(60f + waveIndex * 8f, 0f, 0f), 1, 12, false, roomIndex, waveIndex);
                    strikeCount += 12;
                }
                else if (waveIndex <= 4)
                {
                    Space4XFleetcrawlSpawnUtil.SpawnCarrier(ref state, anchor + new float3(85f, 0f, waveIndex * 8f), 1, new FixedString64Bytes($"elite-{roomIndex}-{waveIndex}"), false, roomIndex, waveIndex);
                    carrierCount++;
                    Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(78f, 0f, waveIndex * 8f), 1, 8, false, roomIndex, waveIndex);
                    strikeCount += 8;
                }
                else
                {
                    Space4XFleetcrawlSpawnUtil.SpawnCarrier(ref state, anchor + new float3(95f, 0f, 0f), 1, new FixedString64Bytes($"miniboss-{roomIndex}-{waveIndex}"), false, roomIndex, waveIndex);
                    carrierCount++;
                    Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(95f, 0f, 0f), 1, 12, false, roomIndex, waveIndex);
                    strikeCount += 12;
                }
            }

            director.WavesSpawnedInRoom++;
            director.EnemiesSpawnedInRoom += carrierCount + strikeCount;
            director.StableDigest = math.hash(new uint2(director.StableDigest, (uint)(roomIndex * 257 + waveIndex * 31 + director.EnemiesSpawnedInRoom)));
            Debug.Log($"[Fleetcrawl] WAVE room={roomIndex} wave={waveIndex} tick={tick} carriers={carrierCount} strike={strikeCount}.");
        }

        private void FinalizeRoom(ref SystemState state, ref Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, float dt)
        {
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            var currency = state.EntityManager.GetComponentData<RunCurrency>(directorEntity);
            currency.Value += room.RewardCurrency;
            state.EntityManager.SetComponentData(directorEntity, currency);

            foreach (var hull in SystemAPI.Query<RefRW<HullIntegrity>>().WithAll<Space4XRunPlayerTag>())
            {
                var hullData = hull.ValueRO;
                if (room.RewardMaxHullFlat > 0f)
                {
                    hullData.Max += room.RewardMaxHullFlat;
                    hullData.BaseMax += room.RewardMaxHullFlat;
                    hullData.Current += room.RewardMaxHullFlat;
                }
                if (room.RewardHealRatio > 0f)
                {
                    hullData.Current = math.min(hullData.Max, hullData.Current + hullData.Max * room.RewardHealRatio);
                }
                hull.ValueRW = hullData;
            }

            if (room.RewardPomPct > 0f)
            {
                foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
                {
                    var weaponBuffer = weapons;
                    for (var i = 0; i < weaponBuffer.Length; i++)
                    {
                        var mount = weaponBuffer[i];
                        mount.Weapon.BaseDamage *= 1f + room.RewardPomPct;
                        mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(mount.Weapon.CooldownTicks * (1f - room.RewardPomPct * 0.75f)));
                        weaponBuffer[i] = mount;
                    }
                }
            }

            var gateSummary = ResolveRoomGates(ref state, ref director, room);
            var buildDigest = ComputeBuildDigest(state.EntityManager, directorEntity, director.Seed);
            var dps = math.max(0f, (SumPlayerDamage(ref state) - director.DamageSnapshotAtRoomStart) / math.max(1e-6f, (director.RoomEndTick - director.RoomStartTick) * dt));
            var digestRoom = math.hash(new uint4((uint)director.CurrentRoomIndex, (uint)director.WavesSpawnedInRoom, (uint)director.EnemiesDestroyedInRoom, (uint)math.max(0, currency.Value)));
            director.StableDigest = math.hash(new uint4(director.StableDigest ^ digestRoom, (uint)math.round(dps * 100f), buildDigest, director.Seed));
            var perks = state.EntityManager.GetBuffer<Space4XRunPerkOp>(directorEntity);
            var installed = state.EntityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            Debug.Log($"[Fleetcrawl] ROOM_END index={director.CurrentRoomIndex} kind={room.Kind} waves={director.WavesSpawnedInRoom} killed={director.EnemiesDestroyedInRoom}/{director.EnemiesSpawnedInRoom} gates={gateSummary} perks={perks.Length} blueprints={FormatInstalledBlueprints(installed)} currency={currency.Value} dps={dps:0.0} digest={director.StableDigest}.");
        }

        private struct RewardOffer
        {
            public Space4XRunRewardKind Kind;
            public Space4XRunBlueprintKind BlueprintKind;
            public FixedString64Bytes RewardId;
            public FixedString64Bytes ManufacturerId;
            public FixedString64Bytes PartA;
            public FixedString64Bytes PartB;
            public FixedString64Bytes BaseModuleId;
        }

        public static class Space4XFleetcrawlBuildDeterminism
        {
            public static uint SimulateDigest(uint seed, int roomCount)
            {
                var roomTotal = math.max(0, roomCount);
                var runSeed = seed == 0u ? 9017u : seed;
                var digest = DeterministicMix(runSeed, 0x9E3779B9u, 0x85EBCA6Bu, 0xC2B2AE35u);
                var boonBits = 0u;
                var blueprintBits = 0u;

                for (var roomIndex = 0; roomIndex < roomTotal; roomIndex++)
                {
                    var roomKind = ResolveDeterministicRoomKind(roomIndex);
                    var gateCount = Space4XFleetcrawlUiBridge.ResolveGateCount(roomKind);
                    var gateOrdinal = Space4XFleetcrawlUiBridge.ResolveAutoGateOrdinal(runSeed, roomIndex, gateCount);
                    var gateKind = ResolveGateKindForDeterminism(roomIndex, gateOrdinal);
                    var pick = PickOfferIndex(runSeed, roomIndex, gateKind, 3);
                    if (gateKind == Space4XRunGateKind.Boon)
                    {
                        boonBits |= 1u << ((roomIndex + pick) % 4);
                    }
                    else if (gateKind == Space4XRunGateKind.Blueprint)
                    {
                        blueprintBits |= 1u << ((roomIndex * 3 + pick) % 4);
                    }

                    digest = DeterministicMix(digest, (uint)gateKind, (uint)pick, (uint)((roomIndex + 1) * 17 + gateOrdinal));
                }

                return DeterministicMix(digest, boonBits, blueprintBits, (uint)roomTotal);
            }
        }

        private FixedString512Bytes ResolveRoomGates(ref SystemState state, ref Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room)
        {
            var summary = new FixedString512Bytes();
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            var records = state.EntityManager.GetBuffer<Space4XRunGateRewardRecord>(directorEntity);
            var gateCount = Space4XFleetcrawlUiBridge.ResolveGateCount(room.Kind);
            var gateOrdinal = Space4XFleetcrawlUiBridge.ResolveAutoGateOrdinal(director.Seed, director.CurrentRoomIndex, gateCount);
            var gateSource = "auto";
            if (state.EntityManager.HasComponent<Space4XRunPendingGatePick>(directorEntity))
            {
                var pending = state.EntityManager.GetComponentData<Space4XRunPendingGatePick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.GateOrdinal >= 0 && pending.GateOrdinal < gateCount)
                {
                    gateOrdinal = pending.GateOrdinal;
                    gateSource = "manual";
                }

                state.EntityManager.RemoveComponent<Space4XRunPendingGatePick>(directorEntity);
            }

            var gateKind = ResolveGateKind(room, gateOrdinal);
            ResolveOffers(director.Seed, director.CurrentRoomIndex, gateKind, out var offerA, out var offerB, out var offerC);
            var pick = PickOfferIndex(director.Seed, director.CurrentRoomIndex, gateKind, 3);
            var pickSource = "auto";
            if (gateKind == Space4XRunGateKind.Boon && state.EntityManager.HasComponent<Space4XRunPendingBoonPick>(directorEntity))
            {
                var pending = state.EntityManager.GetComponentData<Space4XRunPendingBoonPick>(directorEntity);
                if (pending.RoomIndex == director.CurrentRoomIndex && pending.OfferIndex >= 0 && pending.OfferIndex < 3)
                {
                    pick = pending.OfferIndex;
                    pickSource = "manual";
                }

                state.EntityManager.RemoveComponent<Space4XRunPendingBoonPick>(directorEntity);
            }
            else if (state.EntityManager.HasComponent<Space4XRunPendingBoonPick>(directorEntity))
            {
                var staleBoonPick = state.EntityManager.GetComponentData<Space4XRunPendingBoonPick>(directorEntity);
                if (staleBoonPick.RoomIndex <= director.CurrentRoomIndex)
                {
                    state.EntityManager.RemoveComponent<Space4XRunPendingBoonPick>(directorEntity);
                }
            }

            var picked = pick switch
            {
                0 => offerA,
                1 => offerB,
                _ => offerC
            };

            Debug.Log($"[Fleetcrawl] GATE_CHOICE room={director.CurrentRoomIndex} gate_ordinal={gateOrdinal}/{gateCount} gate={gateKind} source={gateSource}.");
            Debug.Log($"[Fleetcrawl] GATE_OFFER room={director.CurrentRoomIndex} gate={gateKind} offers=[{offerA.RewardId},{offerB.RewardId},{offerC.RewardId}] pick={pick}:{picked.RewardId} source={pickSource}.");
            ApplyOffer(ref state, directorEntity, picked, director.CurrentRoomIndex);
            records.Add(new Space4XRunGateRewardRecord
            {
                RoomIndex = director.CurrentRoomIndex,
                GateKind = gateKind,
                RewardKind = picked.Kind,
                RewardId = picked.RewardId
            });

            summary.Append(gateKind.ToString());
            summary.Append(":");
            summary.Append(picked.RewardId);
            return summary;
        }

        private static Space4XRunGateKind ResolveGateKind(in Space4XFleetcrawlRoom room, int gateOrdinal)
        {
            if (room.Kind == Space4XFleetcrawlRoomKind.Relief)
            {
                return gateOrdinal == 0 ? Space4XRunGateKind.Boon : Space4XRunGateKind.Blueprint;
            }

            return gateOrdinal switch
            {
                0 => Space4XRunGateKind.Boon,
                1 => Space4XRunGateKind.Blueprint,
                _ => Space4XRunGateKind.Relief
            };
        }

        private static Space4XRunGateKind ResolveGateKindForDeterminism(int roomIndex, int gateOrdinal)
        {
            if (ResolveDeterministicRoomKind(roomIndex) == Space4XFleetcrawlRoomKind.Relief)
            {
                return gateOrdinal == 0 ? Space4XRunGateKind.Boon : Space4XRunGateKind.Blueprint;
            }

            return gateOrdinal switch
            {
                0 => Space4XRunGateKind.Boon,
                1 => Space4XRunGateKind.Blueprint,
                _ => Space4XRunGateKind.Relief
            };
        }

        private static Space4XFleetcrawlRoomKind ResolveDeterministicRoomKind(int roomIndex)
        {
            var pattern = roomIndex % 5;
            return pattern switch
            {
                1 => Space4XFleetcrawlRoomKind.Relief,
                3 => Space4XFleetcrawlRoomKind.Boss,
                4 => Space4XFleetcrawlRoomKind.Relief,
                _ => Space4XFleetcrawlRoomKind.Combat
            };
        }

        private static void ResolveOffers(uint seed, int roomIndex, Space4XRunGateKind gateKind, out RewardOffer offerA, out RewardOffer offerB, out RewardOffer offerC)
        {
            var start = (int)(DeterministicMix(seed, (uint)(roomIndex + 1), (uint)gateKind + 17u, 0xA5A5A5A5u) % 4u);
            switch (gateKind)
            {
                case Space4XRunGateKind.Boon:
                    offerA = GetBoonOffer(start % 4);
                    offerB = GetBoonOffer((start + 1) % 4);
                    offerC = GetBoonOffer((start + 2) % 4);
                    break;
                case Space4XRunGateKind.Blueprint:
                    offerA = GetBlueprintOffer(start % 4);
                    offerB = GetBlueprintOffer((start + 1) % 4);
                    offerC = GetBlueprintOffer((start + 2) % 4);
                    break;
                default:
                    offerA = GetReliefOffer(0);
                    offerB = GetReliefOffer(1);
                    offerC = GetReliefOffer(2);
                    break;
            }
        }

        private static RewardOffer GetBoonOffer(int index)
        {
            return index switch
            {
                0 => new RewardOffer { Kind = Space4XRunRewardKind.Boon, RewardId = new FixedString64Bytes("perk_convert_kinetic_to_beam_100") },
                1 => new RewardOffer { Kind = Space4XRunRewardKind.Boon, RewardId = new FixedString64Bytes("perk_drones_use_beam") },
                2 => new RewardOffer { Kind = Space4XRunRewardKind.Boon, RewardId = new FixedString64Bytes("perk_beam_chain_small") },
                _ => new RewardOffer { Kind = Space4XRunRewardKind.Boon, RewardId = new FixedString64Bytes("perk_beam_damage_mult_small") }
            };
        }

        private static RewardOffer GetBlueprintOffer(int index)
        {
            return index switch
            {
                0 => new RewardOffer
                {
                    Kind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Weapon,
                    RewardId = new FixedString64Bytes("weapon_laser_prismworks_coreA_lensBeam"),
                    BaseModuleId = new FixedString64Bytes("weapon_laser"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("coreA"),
                    PartB = new FixedString64Bytes("lensBeam")
                },
                1 => new RewardOffer
                {
                    Kind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Weapon,
                    RewardId = new FixedString64Bytes("weapon_kinetic_baseline_coreB_barrelKinetic"),
                    BaseModuleId = new FixedString64Bytes("weapon_kinetic"),
                    ManufacturerId = new FixedString64Bytes("baseline"),
                    PartA = new FixedString64Bytes("coreB"),
                    PartB = new FixedString64Bytes("barrelKinetic")
                },
                2 => new RewardOffer
                {
                    Kind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Reactor,
                    RewardId = new FixedString64Bytes("reactor_prismworks_coreA_coolingStable"),
                    BaseModuleId = new FixedString64Bytes("reactor"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("coreA"),
                    PartB = new FixedString64Bytes("coolingStable")
                },
                _ => new RewardOffer
                {
                    Kind = Space4XRunRewardKind.ModuleBlueprint,
                    BlueprintKind = Space4XRunBlueprintKind.Hangar,
                    RewardId = new FixedString64Bytes("hangar_prismworks_guidanceDroneLink_lensBeam"),
                    BaseModuleId = new FixedString64Bytes("hangar"),
                    ManufacturerId = new FixedString64Bytes("prismworks"),
                    PartA = new FixedString64Bytes("guidanceDroneLink"),
                    PartB = new FixedString64Bytes("lensBeam")
                }
            };
        }

        private static RewardOffer GetReliefOffer(int index)
        {
            return index switch
            {
                0 => new RewardOffer { Kind = Space4XRunRewardKind.Currency, RewardId = new FixedString64Bytes("relief_currency_cache") },
                1 => new RewardOffer { Kind = Space4XRunRewardKind.Heal, RewardId = new FixedString64Bytes("relief_hull_patch") },
                _ => new RewardOffer { Kind = Space4XRunRewardKind.Reroll, RewardId = new FixedString64Bytes("relief_reroll_token") }
            };
        }

        private static int PickOfferIndex(uint seed, int roomIndex, Space4XRunGateKind gateKind, int offerCount)
        {
            if (offerCount <= 1)
            {
                return 0;
            }

            var hash = DeterministicMix(seed, (uint)(roomIndex + 1), (uint)gateKind + 101u, 0xC3A5C85Cu);
            return (int)(hash % (uint)offerCount);
        }

        private void ApplyOffer(ref SystemState state, Entity directorEntity, in RewardOffer offer, int roomIndex)
        {
            switch (offer.Kind)
            {
                case Space4XRunRewardKind.Boon:
                    ApplyPerkReward(ref state, directorEntity, offer.RewardId, roomIndex);
                    break;
                case Space4XRunRewardKind.ModuleBlueprint:
                    ApplyBlueprintReward(ref state, directorEntity, offer, roomIndex);
                    break;
                case Space4XRunRewardKind.Currency:
                    {
                        var currency = state.EntityManager.GetComponentData<RunCurrency>(directorEntity);
                        currency.Value += 35;
                        state.EntityManager.SetComponentData(directorEntity, currency);
                        break;
                    }
                case Space4XRunRewardKind.Heal:
                    HealPlayers(ref state, 0.08f);
                    break;
                case Space4XRunRewardKind.Reroll:
                    {
                        var reroll = state.EntityManager.GetComponentData<Space4XRunRerollTokens>(directorEntity);
                        reroll.Value += 1;
                        state.EntityManager.SetComponentData(directorEntity, reroll);
                        break;
                    }
            }
        }

        private void ApplyPerkReward(ref SystemState state, Entity directorEntity, in FixedString64Bytes perkId, int roomIndex)
        {
            var perkOps = state.EntityManager.GetBuffer<Space4XRunPerkOp>(directorEntity);
            if (perkId.Equals(new FixedString64Bytes("perk_convert_kinetic_to_beam_100")))
            {
                if (!HasPerkOp(perkOps, perkId))
                {
                    perkOps.Add(new Space4XRunPerkOp
                    {
                        PerkId = perkId,
                        Kind = Space4XRunPerkOpKind.ConvertDamage,
                        FromDamageType = Space4XDamageType.Kinetic,
                        ToDamageType = Space4XDamageType.Energy,
                        FromWeaponType = WeaponType.Kinetic,
                        ToWeaponType = WeaponType.Laser,
                        Stat = Space4XRunStatKind.Damage,
                        Target = Space4XRunStatTarget.AllWeapons,
                        Value = 1f,
                        Stacks = 1
                    });
                }
                ConvertPlayerKineticToBeam(ref state);
            }
            else if (perkId.Equals(new FixedString64Bytes("perk_drones_use_beam")))
            {
                if (!HasPerkOp(perkOps, perkId))
                {
                    perkOps.Add(new Space4XRunPerkOp
                    {
                        PerkId = perkId,
                        Kind = Space4XRunPerkOpKind.ReplaceAttackFamily,
                        FromDamageType = Space4XDamageType.Kinetic,
                        ToDamageType = Space4XDamageType.Energy,
                        FromWeaponType = WeaponType.Kinetic,
                        ToWeaponType = WeaponType.Laser,
                        Stat = Space4XRunStatKind.Damage,
                        Target = Space4XRunStatTarget.Drones,
                        Value = 1f,
                        Stacks = 1
                    });
                }
                ConvertDroneWeaponsToBeam(ref state);
            }
            else if (perkId.Equals(new FixedString64Bytes("perk_beam_chain_small")))
            {
                if (!HasPerkOp(perkOps, perkId))
                {
                    perkOps.Add(new Space4XRunPerkOp
                    {
                        PerkId = perkId,
                        Kind = Space4XRunPerkOpKind.AddTag,
                        Stat = Space4XRunStatKind.Damage,
                        Target = Space4XRunStatTarget.BeamWeapons,
                        Value = 0.25f,
                        Stacks = 1
                    });
                    foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XRunPlayerTag>>().WithEntityAccess())
                    {
                        if (!state.EntityManager.HasComponent<Space4XRunChainLightningSource>(entity))
                        {
                            state.EntityManager.AddComponent<Space4XRunChainLightningSource>(entity);
                        }
                    }
                }
            }
            else
            {
                var index = FindPerkOpIndex(perkOps, perkId);
                if (index < 0)
                {
                    perkOps.Add(new Space4XRunPerkOp
                    {
                        PerkId = perkId,
                        Kind = Space4XRunPerkOpKind.MulStat,
                        Stat = Space4XRunStatKind.Damage,
                        Target = Space4XRunStatTarget.BeamWeapons,
                        Value = 1.12f,
                        Stacks = 1
                    });
                }
                else
                {
                    var op = perkOps[index];
                    op.Stacks = (byte)math.min(255, op.Stacks + 1);
                    op.Value *= 1.05f;
                    perkOps[index] = op;
                }
                ApplyBeamDamageMultiplier(ref state, 1.12f);
            }

            Debug.Log($"[Fleetcrawl] GATE room={roomIndex} gate=Boon picked={perkId}.");
        }

        private void ApplyBlueprintReward(ref SystemState state, Entity directorEntity, in RewardOffer offer, int roomIndex)
        {
            var installed = state.EntityManager.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            var replaced = false;
            for (var i = 0; i < installed.Length; i++)
            {
                if (installed[i].Kind != offer.BlueprintKind)
                {
                    continue;
                }

                installed[i] = new Space4XRunInstalledBlueprint
                {
                    BlueprintId = offer.RewardId,
                    BaseModuleId = offer.BaseModuleId,
                    ManufacturerId = offer.ManufacturerId,
                    PartA = offer.PartA,
                    PartB = offer.PartB,
                    Kind = offer.BlueprintKind,
                    Version = (byte)(installed[i].Version + 1)
                };
                replaced = true;
                break;
            }

            if (!replaced)
            {
                installed.Add(new Space4XRunInstalledBlueprint
                {
                    BlueprintId = offer.RewardId,
                    BaseModuleId = offer.BaseModuleId,
                    ManufacturerId = offer.ManufacturerId,
                    PartA = offer.PartA,
                    PartB = offer.PartB,
                    Kind = offer.BlueprintKind,
                    Version = 1
                });
            }

            RegisterUnlocks(state.EntityManager, directorEntity, offer);

            switch (offer.BlueprintKind)
            {
                case Space4XRunBlueprintKind.Weapon:
                    InstallWeaponBlueprint(ref state, offer.RewardId);
                    break;
                case Space4XRunBlueprintKind.Reactor:
                    ApplyReactorBlueprint(ref state, directorEntity);
                    break;
                case Space4XRunBlueprintKind.Hangar:
                    SpawnHangarDronesFromBlueprint(ref state, directorEntity, roomIndex);
                    break;
            }

            Debug.Log($"[Fleetcrawl] GATE room={roomIndex} gate=Blueprint picked={offer.RewardId}.");
        }

        private static void RegisterUnlocks(EntityManager em, Entity directorEntity, in RewardOffer offer)
        {
            var manufacturers = em.GetBuffer<Space4XRunUnlockedManufacturer>(directorEntity);
            var parts = em.GetBuffer<Space4XRunUnlockedPart>(directorEntity);
            if (!ContainsManufacturer(manufacturers, offer.ManufacturerId))
            {
                manufacturers.Add(new Space4XRunUnlockedManufacturer { ManufacturerId = offer.ManufacturerId });
            }
            if (!ContainsPart(parts, offer.PartA))
            {
                parts.Add(new Space4XRunUnlockedPart { PartId = offer.PartA });
            }
            if (!ContainsPart(parts, offer.PartB))
            {
                parts.Add(new Space4XRunUnlockedPart { PartId = offer.PartB });
            }
        }

        private static bool ContainsManufacturer(DynamicBuffer<Space4XRunUnlockedManufacturer> buffer, in FixedString64Bytes id)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ManufacturerId.Equals(id))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsPart(DynamicBuffer<Space4XRunUnlockedPart> buffer, in FixedString64Bytes id)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].PartId.Equals(id))
                {
                    return true;
                }
            }
            return false;
        }

        private void InstallWeaponBlueprint(ref SystemState state, in FixedString64Bytes blueprintId)
        {
            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<PlayerFlagshipTag>())
            {
                var weaponBuffer = weapons;
                if (weaponBuffer.Length == 0)
                {
                    continue;
                }

                var mount = weaponBuffer[0];
                if (blueprintId.Equals(new FixedString64Bytes("weapon_kinetic_baseline_coreB_barrelKinetic")))
                {
                    mount.Weapon = Space4XWeapon.Kinetic(WeaponSize.Large);
                    mount.Weapon.BaseDamage *= 1.25f;
                    mount.Weapon.CooldownTicks = (ushort)math.max(1, mount.Weapon.CooldownTicks - 2);
                }
                else
                {
                    mount.Weapon = Space4XWeapon.Laser(WeaponSize.Large);
                    mount.Weapon.BaseDamage *= 1.15f;
                    mount.Weapon.CooldownTicks = (ushort)math.max(1, mount.Weapon.CooldownTicks - 1);
                }
                weaponBuffer[0] = mount;
            }
        }

        private void ApplyReactorBlueprint(ref SystemState state, Entity directorEntity)
        {
            var modifiers = state.EntityManager.GetComponentData<Space4XRunReactiveModifiers>(directorEntity);
            modifiers.DamageMul *= 1.08f;
            modifiers.CooldownMul *= 0.9f;
            state.EntityManager.SetComponentData(directorEntity, modifiers);

            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
            {
                var weaponBuffer = weapons;
                for (var i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];
                    mount.Weapon.BaseDamage *= 1.08f;
                    mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(mount.Weapon.CooldownTicks * 0.9f));
                    mount.HeatDissipation *= 1.12f;
                    weaponBuffer[i] = mount;
                }
            }
        }

        private void SpawnHangarDronesFromBlueprint(ref SystemState state, Entity directorEntity, int roomIndex)
        {
            var anchor = new float3(-120f, 0f, 0f);
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerFlagshipTag>())
            {
                anchor = transform.ValueRO.Position;
                break;
            }

            Space4XFleetcrawlSpawnUtil.SpawnStrikeWing(ref state, anchor + new float3(-8f, 0f, 12f), 0, 4, true, roomIndex, 0, true);
            var perkOps = state.EntityManager.GetBuffer<Space4XRunPerkOp>(directorEntity);
            if (HasPerkOp(perkOps, new FixedString64Bytes("perk_drones_use_beam")) ||
                HasPerkOp(perkOps, new FixedString64Bytes("perk_convert_kinetic_to_beam_100")))
            {
                ConvertDroneWeaponsToBeam(ref state);
            }
        }

        private static bool HasPerkOp(DynamicBuffer<Space4XRunPerkOp> perkOps, in FixedString64Bytes perkId)
        {
            return FindPerkOpIndex(perkOps, perkId) >= 0;
        }

        private static int FindPerkOpIndex(DynamicBuffer<Space4XRunPerkOp> perkOps, in FixedString64Bytes perkId)
        {
            for (var i = 0; i < perkOps.Length; i++)
            {
                if (perkOps[i].PerkId.Equals(perkId))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ConvertPlayerKineticToBeam(ref SystemState state)
        {
            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
            {
                var weaponBuffer = weapons;
                for (var i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];
                    if (mount.Weapon.DamageType != Space4XDamageType.Kinetic && mount.Weapon.Type != WeaponType.Kinetic)
                    {
                        continue;
                    }

                    ConvertMountToBeam(ref mount, 1.05f);
                    weaponBuffer[i] = mount;
                }
            }
        }

        private void ConvertDroneWeaponsToBeam(ref SystemState state)
        {
            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag, Space4XRunDroneTag>())
            {
                var weaponBuffer = weapons;
                for (var i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];
                    ConvertMountToBeam(ref mount, 1.02f);
                    weaponBuffer[i] = mount;
                }
            }
        }

        private void ApplyBeamDamageMultiplier(ref SystemState state, float multiplier)
        {
            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
            {
                var weaponBuffer = weapons;
                for (var i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];
                    if (mount.Weapon.Delivery != WeaponDelivery.Beam && mount.Weapon.DamageType != Space4XDamageType.Energy)
                    {
                        continue;
                    }

                    mount.Weapon.BaseDamage *= multiplier;
                    weaponBuffer[i] = mount;
                }
            }
        }

        private static void ConvertMountToBeam(ref WeaponMount mount, float damageScale)
        {
            var w = mount.Weapon;
            w.Type = WeaponType.Laser;
            w.DamageType = Space4XDamageType.Energy;
            w.Family = WeaponFamily.Energy;
            w.Delivery = WeaponDelivery.Beam;
            w.ShieldModifier = (half)math.max((float)w.ShieldModifier, 1.25f);
            w.ArmorPenetration = (half)math.min((float)w.ArmorPenetration, 0.45f);
            w.BaseDamage *= damageScale;
            mount.Weapon = w;
        }

        private void HealPlayers(ref SystemState state, float ratio)
        {
            foreach (var hull in SystemAPI.Query<RefRW<HullIntegrity>>().WithAll<Space4XRunPlayerTag>())
            {
                var hullData = hull.ValueRO;
                hullData.Current = math.min(hullData.Max, hullData.Current + hullData.Max * math.max(0f, ratio));
                hull.ValueRW = hullData;
            }
        }

        private static string FormatInstalledBlueprints(DynamicBuffer<Space4XRunInstalledBlueprint> installed)
        {
            if (installed.Length == 0)
            {
                return "<none>";
            }

            var result = string.Empty;
            for (var i = 0; i < installed.Length; i++)
            {
                if (i > 0)
                {
                    result += ",";
                }
                result += installed[i].BlueprintId.ToString();
            }

            return result;
        }

        private static uint ComputeBuildDigest(EntityManager em, Entity directorEntity, uint seed)
        {
            var digest = math.hash(new uint2(seed == 0u ? 9017u : seed, 0xB5297A4Du));
            var perkOps = em.GetBuffer<Space4XRunPerkOp>(directorEntity);
            for (var i = 0; i < perkOps.Length; i++)
            {
                var op = perkOps[i];
                digest = math.hash(new uint4(
                    digest,
                    HashFixedString(op.PerkId),
                    (uint)op.Kind + ((uint)op.Stacks << 8),
                    (uint)math.round(math.abs(op.Value) * 1000f)));
            }

            var installed = em.GetBuffer<Space4XRunInstalledBlueprint>(directorEntity);
            for (var i = 0; i < installed.Length; i++)
            {
                var bp = installed[i];
                digest = math.hash(new uint4(
                    digest ^ (uint)bp.Kind,
                    HashFixedString(bp.BlueprintId),
                    HashFixedString(bp.ManufacturerId),
                    HashFixedString(bp.PartA) ^ HashFixedString(bp.PartB)));
            }

            return digest;
        }

        private static uint DeterministicMix(uint a, uint b, uint c, uint d)
        {
            var hash = 2166136261u;
            hash ^= a;
            hash *= 16777619u;
            hash ^= b;
            hash *= 16777619u;
            hash ^= c;
            hash *= 16777619u;
            hash ^= d;
            hash *= 16777619u;
            return hash;
        }

        private static uint HashFixedString(in FixedString64Bytes value)
        {
            var hash = 2166136261u;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash;
        }

        private float SumPlayerDamage(ref SystemState state)
        {
            var total = 0f;
            foreach (var engagement in SystemAPI.Query<RefRO<Space4XEngagement>>().WithAll<Space4XRunPlayerTag>())
            {
                total += engagement.ValueRO.DamageDealt;
            }
            return total;
        }

        private float ResolveFixedDelta(in TimeState time)
        {
            if (time.FixedDeltaTime > 0f)
            {
                return time.FixedDeltaTime;
            }

            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState) && tickTimeState.FixedDeltaTime > 0f)
            {
                return tickTimeState.FixedDeltaTime;
            }

            return 1f / 60f;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XDamageResolutionSystem))]
    public partial struct Space4XRunChainLightningSystem : ISystem
    {
        private ComponentLookup<Space4XRunChainLightningSource> _chainLookup;
        private ComponentLookup<Space4XRunPlayerTag> _playerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XRunChainLightningSource>();
            state.RequireForUpdate<DamageEvent>();
            _chainLookup = state.GetComponentLookup<Space4XRunChainLightningSource>(true);
            _playerLookup = state.GetComponentLookup<Space4XRunPlayerTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _chainLookup.Update(ref state);
            _playerLookup.Update(ref state);

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (events, hull, side, cursor) in SystemAPI.Query<DynamicBuffer<DamageEvent>, RefRW<HullIntegrity>, RefRO<ScenarioSide>, RefRW<Space4XRunDamageEventCursor>>().WithAll<Space4XRunEnemyTag>())
            {
                if (side.ValueRO.Side != 1 || hull.ValueRO.Current <= 0f)
                {
                    cursor.ValueRW.ProcessedCount = events.Length;
                    continue;
                }

                var from = math.clamp(cursor.ValueRO.ProcessedCount, 0, events.Length);
                var to = events.Length;
                var hullData = hull.ValueRO;
                for (var i = from; i < to; i++)
                {
                    var evt = events[i];
                    if (evt.Source == Entity.Null || !_playerLookup.HasComponent(evt.Source) || !_chainLookup.HasComponent(evt.Source))
                    {
                        continue;
                    }

                    var extraDamage = math.max(0f, evt.HullDamage * 0.25f + evt.ShieldDamage * 0.1f);
                    if (extraDamage <= 0f)
                    {
                        continue;
                    }

                    hullData.Current = math.max(0f, hullData.Current - extraDamage);
                    events.Add(new DamageEvent
                    {
                        Source = Entity.Null,
                        WeaponType = WeaponType.Ion,
                        RawDamage = extraDamage,
                        ShieldDamage = 0f,
                        ArmorDamage = 0f,
                        HullDamage = extraDamage,
                        Tick = tick,
                        IsCritical = 0
                    });
                }

                hull.ValueRW = hullData;
                cursor.ValueRW.ProcessedCount = events.Length;
            }
        }
    }
}
