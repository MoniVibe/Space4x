using System;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
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

    public struct Space4XFleetcrawlSeeded : IComponentData { public FixedString64Bytes ScenarioId; }
    public struct Space4XRunPlayerTag : IComponentData { }
    public struct PlayerFlagshipTag : IComponentData { }
    public struct Space4XRunEnemyTag : IComponentData { public int RoomIndex; public int WaveIndex; }
    public struct Space4XRunEnemyDestroyedCounted : IComponentData { }
    public struct Space4XRunChainLightningSource : IComponentData { }
    public struct Space4XRunDamageEventCursor : IComponentData { public int ProcessedCount; }
    public struct RunCurrency : IComponentData { public int Value; }
    public struct Space4XRunBoonSet : IComponentData { public byte ChainLightning; public byte MissileVolley; public byte BoostCooldown; public byte ShieldRegen; }

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

        public static void SpawnStrikeWing(ref SystemState state, float3 anchor, byte side, int count, bool playerTag, int roomIndex, int waveIndex)
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
                AddWeapon(weapons, Space4XWeapon.Laser(WeaponSize.Small));
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

            var directorEntity = state.EntityManager.CreateEntity(typeof(Space4XFleetcrawlDirectorState), typeof(RunCurrency), typeof(Space4XRunBoonSet));
            var rooms = state.EntityManager.AddBuffer<Space4XFleetcrawlRoom>(directorEntity);
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

            state.EntityManager.SetComponentData(directorEntity, new Space4XFleetcrawlDirectorState { Initialized = 0, CurrentRoomIndex = -1, StableDigest = 1u });
            state.EntityManager.SetComponentData(directorEntity, new RunCurrency { Value = 0 });
            state.EntityManager.SetComponentData(directorEntity, new Space4XRunBoonSet());

            var seed = state.EntityManager.CreateEntity(typeof(Space4XFleetcrawlSeeded));
            state.EntityManager.SetComponentData(seed, new Space4XFleetcrawlSeeded { ScenarioId = info.ScenarioId });
            Debug.Log($"[Fleetcrawl] Seeded run. rooms={rooms.Length} short_mode={(shortMode ? 1 : 0)}.");
        }

        private static uint ToTicks(float sec, float dt) => (uint)math.max(1f, math.round(math.max(0.01f, sec) / math.max(1e-6f, dt)));

        private static float ResolveFixedDelta(in TimeState time)
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
            foreach (var (hull, entity) in SystemAPI.Query<RefRO<HullIntegrity>>().WithAll<Space4XRunEnemyTag>().WithNone<Space4XRunEnemyDestroyedCounted>().WithEntityAccess())
            {
                if (hull.ValueRO.Current > 0f)
                {
                    continue;
                }

                director.EnemiesDestroyedInRoom++;
                ecb.AddComponent<Space4XRunEnemyDestroyedCounted>(entity);
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

        private static void StartRoom(ref SystemState state, ref Space4XFleetcrawlDirectorState director, DynamicBuffer<Space4XFleetcrawlRoom> rooms, int roomIndex, uint tick, float dt)
        {
            var room = rooms[roomIndex];
            director.Initialized = 1;
            director.CurrentRoomIndex = roomIndex;
            director.RoomStartTick = tick;
            director.RoomEndTick = tick + math.max(1u, room.DurationTicks);
            director.WavesSpawnedInRoom = 0;
            director.EnemiesSpawnedInRoom = 0;
            director.EnemiesDestroyedInRoom = 0;
            director.DamageSnapshotAtRoomStart = SumPlayerDamage();
            director.NextWaveTick = director.RoomEndTick;

            if (room.Kind == Space4XFleetcrawlRoomKind.Combat || room.Kind == Space4XFleetcrawlRoomKind.Boss)
            {
                SpawnWave(ref state, ref director, room, roomIndex, 1, tick);
                director.NextWaveTick = tick + math.max(1u, room.WaveIntervalTicks);
            }

            Debug.Log($"[Fleetcrawl] ROOM_START index={roomIndex} kind={room.Kind} duration_s={(director.RoomEndTick - director.RoomStartTick) * dt:0.0} waves={room.PlannedWaves}.");
        }

        private static void SpawnWave(ref SystemState state, ref Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, int roomIndex, int waveIndex, uint tick)
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

        private static void FinalizeRoom(ref SystemState state, ref Space4XFleetcrawlDirectorState director, in Space4XFleetcrawlRoom room, float dt)
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
                    for (var i = 0; i < weapons.Length; i++)
                    {
                        var mount = weapons[i];
                        mount.Weapon.BaseDamage *= 1f + room.RewardPomPct;
                        mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(mount.Weapon.CooldownTicks * (1f - room.RewardPomPct * 0.75f)));
                        weapons[i] = mount;
                    }
                }
            }

            if (room.OfferBoon != 0)
            {
                ApplyBoon(ref state, director.CurrentRoomIndex);
            }

            var dps = math.max(0f, (SumPlayerDamage() - director.DamageSnapshotAtRoomStart) / math.max(1e-6f, (director.RoomEndTick - director.RoomStartTick) * dt));
            var digestRoom = math.hash(new uint4((uint)director.CurrentRoomIndex, (uint)director.WavesSpawnedInRoom, (uint)director.EnemiesDestroyedInRoom, (uint)math.max(0, currency.Value)));
            director.StableDigest = math.hash(new uint2(director.StableDigest ^ digestRoom, (uint)math.round(dps * 100f)));
            Debug.Log($"[Fleetcrawl] ROOM_END index={director.CurrentRoomIndex} kind={room.Kind} waves={director.WavesSpawnedInRoom} killed={director.EnemiesDestroyedInRoom}/{director.EnemiesSpawnedInRoom} currency={currency.Value} dps={dps:0.0} digest={director.StableDigest}.");
        }

        private static void ApplyBoon(ref SystemState state, int roomIndex)
        {
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            var boonSet = state.EntityManager.GetComponentData<Space4XRunBoonSet>(directorEntity);
            var picked = (Space4XFleetcrawlBoonType)(roomIndex % 4);
            var offerB = (Space4XFleetcrawlBoonType)((roomIndex + 1) % 4);
            var offerC = (Space4XFleetcrawlBoonType)((roomIndex + 2) % 4);

            switch (picked)
            {
                case Space4XFleetcrawlBoonType.ChainLightning:
                    if (boonSet.ChainLightning == 0)
                    {
                        boonSet.ChainLightning = 1;
                        foreach (var (_, entity) in SystemAPI.Query<RefRO<Space4XRunPlayerTag>>().WithEntityAccess())
                        {
                            if (!state.EntityManager.HasComponent<Space4XRunChainLightningSource>(entity))
                            {
                                state.EntityManager.AddComponent<Space4XRunChainLightningSource>(entity);
                            }
                        }
                    }
                    break;
                case Space4XFleetcrawlBoonType.MissileVolley:
                    if (boonSet.MissileVolley == 0)
                    {
                        boonSet.MissileVolley = 1;
                        foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
                        {
                            var append = new NativeList<WeaponMount>(Allocator.Temp);
                            for (var i = 0; i < weapons.Length; i++)
                            {
                                var mount = weapons[i];
                                if (mount.Weapon.Type != WeaponType.Missile)
                                {
                                    continue;
                                }
                                mount.Weapon.BaseDamage *= 0.6f;
                                mount.Weapon.CooldownTicks = (ushort)math.max(1, mount.Weapon.CooldownTicks + 1);
                                append.Add(mount);
                            }
                            for (var i = 0; i < append.Length; i++)
                            {
                                weapons.Add(append[i]);
                            }
                            append.Dispose();
                        }
                    }
                    break;
                case Space4XFleetcrawlBoonType.BoostCooldown:
                    boonSet.BoostCooldown = (byte)math.min(255, boonSet.BoostCooldown + 1);
                    foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
                    {
                        for (var i = 0; i < weapons.Length; i++)
                        {
                            var mount = weapons[i];
                            mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(mount.Weapon.CooldownTicks * 0.82f));
                            weapons[i] = mount;
                        }
                    }
                    break;
                case Space4XFleetcrawlBoonType.ShieldRegen:
                    boonSet.ShieldRegen = (byte)math.min(255, boonSet.ShieldRegen + 1);
                    foreach (var shield in SystemAPI.Query<RefRW<Space4XShield>>().WithAll<Space4XRunPlayerTag>())
                    {
                        var shieldData = shield.ValueRO;
                        shieldData.RechargeRate *= 1.35f;
                        shieldData.Maximum *= 1.08f;
                        shieldData.Current = math.min(shieldData.Maximum, shieldData.Current + shieldData.Maximum * 0.08f);
                        shield.ValueRW = shieldData;
                    }
                    break;
            }

            state.EntityManager.SetComponentData(directorEntity, boonSet);
            Debug.Log($"[Fleetcrawl] BOON room={roomIndex} picked={picked} offers=[{picked},{offerB},{offerC}].");
        }

        private static float SumPlayerDamage()
        {
            var total = 0f;
            foreach (var engagement in SystemAPI.Query<RefRO<Space4XEngagement>>().WithAll<Space4XRunPlayerTag>())
            {
                total += engagement.ValueRO.DamageDealt;
            }
            return total;
        }

        private static float ResolveFixedDelta(in TimeState time)
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
