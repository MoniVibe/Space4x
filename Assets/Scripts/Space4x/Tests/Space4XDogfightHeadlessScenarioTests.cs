#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public class Space4XDogfightHeadlessScenarioTests
    {
        private const float FixedDelta = 1f / 60f;
        private const int SimulationTicks = 600;
        private const int RatioPermille = 1000;
        private const int TtfsToleranceTicks = 5;
        private const int RatioTolerancePermille = 10;
        private static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_dogfight_headless");

        [Test]
        public void DogfightScoreSummary_MirrorAndRotationHold()
        {
            var baseline = RunScenario(new ScenarioVariant());
            WriteSummary("baseline", baseline);

            var mirror = RunScenario(new ScenarioVariant { Transform = ScenarioTransform.MirrorX });
            WriteSummary("mirrorX", mirror);

            var rotated = RunScenario(new ScenarioVariant { Transform = ScenarioTransform.Rotate180 });
            WriteSummary("rotate180", rotated);

            Assert.Greater(baseline.CraftCount, 0, "Dogfight scenario must spawn strike craft.");
            Assert.Greater(baseline.EngagementCount, 0, "Dogfight scenario must produce engagements.");

            AssertMetricsClose(baseline, mirror, "mirror");
            AssertMetricsClose(baseline, rotated, "rotate");
        }

        [Test]
        public void DogfightScoreSummary_ProjectileSpeedScaleDoesNotIncreaseTtfs()
        {
            var baseline = RunScenario(new ScenarioVariant());
            WriteSummary("baseline", baseline);

            var scaled = RunScenario(new ScenarioVariant { ProjectileSpeedMultiplier = 1.5f });
            WriteSummary("projSpeedUp", scaled);

            Assert.LessOrEqual(scaled.TtfsMeanTicks, baseline.TtfsMeanTicks + TtfsToleranceTicks,
                "TTFS should not worsen when projectile speed increases.");
        }

        private static void AssertMetricsClose(DogfightScoreSummary baseline, DogfightScoreSummary variant, string label)
        {
            Assert.That(variant.TtfsMeanTicks,
                Is.InRange(baseline.TtfsMeanTicks - TtfsToleranceTicks, baseline.TtfsMeanTicks + TtfsToleranceTicks),
                $"TTFS drift ({label})");
            Assert.That(variant.ConeRatioPermille,
                Is.InRange(baseline.ConeRatioPermille - RatioTolerancePermille, baseline.ConeRatioPermille + RatioTolerancePermille),
                $"Cone ratio drift ({label})");
            Assert.That(variant.BreakoffSuccessPermille,
                Is.InRange(baseline.BreakoffSuccessPermille - RatioTolerancePermille, baseline.BreakoffSuccessPermille + RatioTolerancePermille),
                $"Breakoff success drift ({label})");
            Assert.That(variant.CurvaturePermille,
                Is.InRange(baseline.CurvaturePermille - RatioTolerancePermille, baseline.CurvaturePermille + RatioTolerancePermille),
                $"Curvature drift ({label})");
        }

        private static DogfightScoreSummary RunScenario(ScenarioVariant variant)
        {
            using var world = new World("Space4XDogfightScoreboard");
            var entityManager = world.EntityManager;
            var initGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var fixedGroup = world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();

            var scenarioSystem = world.GetOrCreateSystemManaged<Space4XMiningScenarioSystem>();
            initGroup.AddSystemToUpdateList(scenarioSystem);

            fixedGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XStrikeCraftGuidanceSystem>());
            fixedGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XStrikeCraftMotorSystem>());
            fixedGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XStrikeCraftDogfightTelemetrySystem>());

            simGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XWeaponSystem>());
            simGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XDamageResolutionSystem>());
            simGroup.AddSystemToUpdateList(world.GetOrCreateSystem<Space4XSubsystemStatusSystem>());

            CoreSingletonBootstrapSystem.EnsureSingletons(entityManager);

            var timeEntity = entityManager.CreateEntityQuery(typeof(TimeState)).GetSingletonEntity();
            var rewindEntity = entityManager.CreateEntityQuery(typeof(RewindState)).GetSingletonEntity();

            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick = 0;
            time.FixedDeltaTime = FixedDelta;
            time.DeltaTime = FixedDelta;
            time.DeltaSeconds = FixedDelta;
            time.ElapsedTime = 0f;
            time.WorldSeconds = 0f;
            time.CurrentSpeedMultiplier = 1f;
            time.IsPaused = false;
            entityManager.SetComponentData(timeEntity, time);

            var rewind = entityManager.GetComponentData<RewindState>(rewindEntity);
            rewind.Mode = RewindMode.Record;
            rewind.TickDuration = FixedDelta;
            entityManager.SetComponentData(rewindEntity, rewind);

            world.EntityManager.WorldUnmanaged.Time = new TimeData(0f, FixedDelta);

            var scenarioEntity = entityManager.CreateEntity(typeof(ScenarioInfo));
            var seed = variant.Seed == 0 ? 1337u : variant.Seed;
            entityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = ScenarioId,
                Seed = seed,
                RunTicks = SimulationTicks
            });

            initGroup.Update();

            if (variant.Transform != ScenarioTransform.None)
            {
                ApplyScenarioTransform(entityManager, variant.Transform);
            }

            if (variant.ProjectileSpeedMultiplier > 0f && math.abs(variant.ProjectileSpeedMultiplier - 1f) > 0.001f)
            {
                var tuningEntity = entityManager.CreateEntity(typeof(Space4XWeaponTuningConfig));
                entityManager.SetComponentData(tuningEntity, new Space4XWeaponTuningConfig
                {
                    ProjectileSpeedMultiplier = variant.ProjectileSpeedMultiplier
                });
            }

            var craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StrikeCraftDogfightTag>(),
                ComponentType.ReadOnly<StrikeCraftState>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<VesselMovement>(),
                ComponentType.ReadOnly<HullIntegrity>(),
                ComponentType.ReadOnly<WeaponMount>());

            using var craftEntitiesNative = craftQuery.ToEntityArray(Allocator.Temp);
            var craftEntities = craftEntitiesNative.ToArray();
            Array.Sort(craftEntities, (a, b) => a.Index.CompareTo(b.Index));

            for (int i = 0; i < craftEntities.Length; i++)
            {
                var craft = craftEntities[i];
                if (!entityManager.HasComponent<Space4XEngagement>(craft))
                {
                    entityManager.AddComponentData(craft, new Space4XEngagement
                    {
                        PrimaryTarget = Entity.Null,
                        Phase = EngagementPhase.None,
                        TargetDistance = 0f,
                        EngagementDuration = 0,
                        DamageDealt = 0f,
                        DamageReceived = 0f,
                        FormationBonus = (half)0f,
                        EvasionModifier = (half)0f
                    });
                }

                if (!entityManager.HasComponent<SupplyStatus>(craft))
                {
                    entityManager.AddComponentData(craft, SupplyStatus.DefaultStrikeCraft);
                }
            }

            StepTick(world, entityManager, initGroup, fixedGroup, simGroup, timeEntity);

            var dogfightConfig = StrikeCraftDogfightConfig.Default;
            using (var configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StrikeCraftDogfightConfig>()))
            {
                if (configQuery.TryGetSingleton(out StrikeCraftDogfightConfig config))
                {
                    dogfightConfig = config;
                }
            }

            var collector = new DogfightScoreCollector(entityManager, craftEntities, FixedDelta, dogfightConfig);

            for (int i = 0; i < SimulationTicks; i++)
            {
                StepTick(world, entityManager, initGroup, fixedGroup, simGroup, timeEntity);
                collector.Sample(entityManager.GetComponentData<TimeState>(timeEntity).Tick);
            }

            return collector.BuildSummary(seed);
        }

        private static void StepTick(
            World world,
            EntityManager entityManager,
            InitializationSystemGroup initGroup,
            FixedStepSimulationSystemGroup fixedGroup,
            SimulationSystemGroup simGroup,
            Entity timeEntity)
        {
            var time = entityManager.GetComponentData<TimeState>(timeEntity);
            time.Tick += 1;
            time.DeltaTime = time.FixedDeltaTime;
            time.DeltaSeconds = time.FixedDeltaTime;
            time.ElapsedTime = time.Tick * time.FixedDeltaTime;
            time.WorldSeconds = time.ElapsedTime;
            entityManager.SetComponentData(timeEntity, time);

            world.EntityManager.WorldUnmanaged.Time = new TimeData(time.ElapsedTime, time.FixedDeltaTime);

            initGroup.Update();
            fixedGroup.Update();
            simGroup.Update();
        }

        private static void ApplyScenarioTransform(EntityManager entityManager, ScenarioTransform transform)
        {
            using var entities = entityManager.CreateEntityQuery(ComponentType.ReadWrite<LocalTransform>()).ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var transformData = entityManager.GetComponentData<LocalTransform>(entity);
                transformData.Position = TransformPosition(transformData.Position, transform);
                transformData.Rotation = TransformRotation(transformData.Rotation, transform);
                entityManager.SetComponentData(entity, transformData);

                if (entityManager.HasComponent<VesselMovement>(entity))
                {
                    var movement = entityManager.GetComponentData<VesselMovement>(entity);
                    movement.Velocity = TransformDirection(movement.Velocity, transform);
                    entityManager.SetComponentData(entity, movement);
                }

                if (entityManager.HasComponent<StrikeCraftState>(entity))
                {
                    var craftState = entityManager.GetComponentData<StrikeCraftState>(entity);
                    craftState.TargetPosition = TransformPosition(craftState.TargetPosition, transform);
                    entityManager.SetComponentData(entity, craftState);
                }
            }
        }

        private static float3 TransformPosition(float3 position, ScenarioTransform transform)
        {
            switch (transform)
            {
                case ScenarioTransform.MirrorX:
                    return new float3(-position.x, position.y, position.z);
                case ScenarioTransform.Rotate180:
                    return new float3(-position.x, position.y, -position.z);
                default:
                    return position;
            }
        }

        private static float3 TransformDirection(float3 direction, ScenarioTransform transform)
        {
            switch (transform)
            {
                case ScenarioTransform.MirrorX:
                    return new float3(-direction.x, direction.y, direction.z);
                case ScenarioTransform.Rotate180:
                    return new float3(-direction.x, direction.y, -direction.z);
                default:
                    return direction;
            }
        }

        private static quaternion TransformRotation(quaternion rotation, ScenarioTransform transform)
        {
            if (transform == ScenarioTransform.Rotate180)
            {
                return math.mul(quaternion.RotateY(math.radians(180f)), rotation);
            }

            return rotation;
        }

        private static void WriteSummary(string label, DogfightScoreSummary summary)
        {
            var line = summary.ToLineString();
            var hash = ComputeHash(line);
            TestContext.WriteLine($"{label}: {line} | hash={hash}");
        }

        private static string ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(payload);
            var hashBytes = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        private sealed class DogfightScoreCollector
        {
            private const float BreakoffDistanceEpsilon = 0.1f;

            private readonly EntityManager _entityManager;
            private readonly Entity[] _craftEntities;
            private readonly CraftScoreState[] _states;
            private readonly float _fixedDelta;
            private readonly float _fireConeCos;
            private readonly uint _breakoffDistanceTicks;
            private readonly uint _breakoffRejoinMaxTicks;

            private long _ttfsTotalTicks;
            private int _ttfsCount;
            private long _ttkTotalTicks;
            private int _ttkCount;
            private long _engagedTicks;
            private long _timeInRangeTicks;
            private long _timeInConeTicks;
            private long _targetChangeCount;
            private int _breakoffAttempts;
            private int _breakoffSuccesses;
            private double _curvatureHeadingSum;
            private double _curvatureDistanceSum;
            private double _speedSum;
            private double _headingSum;
            private long _engineDisabledTicks;
            private long _weaponDisabledTicks;
            private double _engineSpeedSum;
            private double _engineHeadingSum;
            private int _sampleTicks;
            private int _engagementCount;

            public DogfightScoreCollector(EntityManager entityManager, Entity[] craftEntities, float fixedDelta, StrikeCraftDogfightConfig config)
            {
                _entityManager = entityManager;
                _craftEntities = craftEntities;
                _states = new CraftScoreState[craftEntities.Length];
                _fixedDelta = fixedDelta;
                _fireConeCos = math.cos(math.radians(config.FireConeDegrees));
                _breakoffDistanceTicks = math.max(10u, config.BreakOffTicks / 3u);
                _breakoffRejoinMaxTicks = config.BreakOffTicks + (uint)math.round(2f / math.max(1e-6f, fixedDelta));
                for (int i = 0; i < craftEntities.Length; i++)
                {
                    _states[i] = new CraftScoreState
                    {
                        Entity = craftEntities[i],
                        LastTarget = Entity.Null,
                        LastPhase = StrikeCraftDogfightPhase.None,
                        HasLastPose = false,
                        EverEngaged = false
                    };
                }
            }

            public void Sample(uint tick)
            {
                _sampleTicks++;
                for (int i = 0; i < _states.Length; i++)
                {
                    var state = _states[i];
                    var entity = state.Entity;
                    if (!_entityManager.Exists(entity))
                    {
                        continue;
                    }

                    var craftState = _entityManager.GetComponentData<StrikeCraftState>(entity);
                    var transform = _entityManager.GetComponentData<LocalTransform>(entity);
                    var movement = _entityManager.GetComponentData<VesselMovement>(entity);
                    var forward = ResolveForward(transform, movement);
                    var position = transform.Position;

                    var hasTarget = craftState.TargetEntity != Entity.Null &&
                                    _entityManager.HasComponent<LocalTransform>(craftState.TargetEntity);
                    var distanceToTarget = 0f;
                    var inCone = false;
                    var inRange = false;
                    if (hasTarget)
                    {
                        var targetTransform = _entityManager.GetComponentData<LocalTransform>(craftState.TargetEntity);
                        var toTarget = targetTransform.Position - position;
                        distanceToTarget = math.length(toTarget);
                        var direction = distanceToTarget > 1e-4f ? toTarget / distanceToTarget : forward;
                        var coneDot = math.dot(forward, direction);
                        inCone = coneDot >= _fireConeCos;
                        var maxRange = ResolveMaxWeaponRange(_entityManager, entity);
                        inRange = maxRange > 0f && distanceToTarget <= maxRange;
                    }

                    if (hasTarget)
                    {
                        _engagedTicks++;
                        state.EverEngaged = true;
                        if (inRange)
                        {
                            _timeInRangeTicks++;
                            if (inCone)
                            {
                                _timeInConeTicks++;
                            }
                        }
                    }

                    if (craftState.TargetEntity != state.LastTarget && craftState.TargetEntity != Entity.Null)
                    {
                        _targetChangeCount++;
                    }

                    UpdateBreakoffState(ref state, craftState.DogfightPhase, hasTarget, distanceToTarget, tick);

                    if (_entityManager.HasComponent<StrikeCraftDogfightMetrics>(entity))
                    {
                        var metrics = _entityManager.GetComponentData<StrikeCraftDogfightMetrics>(entity);
                        if (metrics.FirstFireTick != 0 && metrics.FirstFireTick == tick && metrics.EngagementStartTick > 0)
                        {
                            _ttfsTotalTicks += metrics.FirstFireTick - metrics.EngagementStartTick;
                            _ttfsCount++;
                        }

                        if (metrics.LastKillTick != 0 && metrics.LastKillTick == tick && metrics.EngagementStartTick > 0)
                        {
                            _ttkTotalTicks += metrics.LastKillTick - metrics.EngagementStartTick;
                            _ttkCount++;
                        }
                    }

                    var headingDelta = 0f;
                    var distanceDelta = 0f;
                    if (state.HasLastPose)
                    {
                        var delta = position - state.LastPosition;
                        distanceDelta = math.length(delta);
                        var dot = math.clamp(math.dot(state.LastForward, forward), -1f, 1f);
                        headingDelta = math.acos(dot);
                    }

                    if (hasTarget)
                    {
                        var speed = math.length(movement.Velocity);
                        _speedSum += speed;
                        _headingSum += math.abs(headingDelta);
                        _curvatureHeadingSum += math.abs(headingDelta);
                        _curvatureDistanceSum += distanceDelta;

                        var engineDisabled = IsSubsystemDisabled(_entityManager, entity, SubsystemType.Engines);
                        var weaponDisabled = IsSubsystemDisabled(_entityManager, entity, SubsystemType.Weapons);

                        if (engineDisabled)
                        {
                            _engineDisabledTicks++;
                            _engineSpeedSum += speed;
                            _engineHeadingSum += math.abs(headingDelta);
                        }

                        if (weaponDisabled)
                        {
                            _weaponDisabledTicks++;
                        }
                    }

                    state.HasLastPose = true;
                    state.LastPosition = position;
                    state.LastForward = forward;
                    state.LastTarget = craftState.TargetEntity;
                    state.LastPhase = craftState.DogfightPhase;
                    _states[i] = state;
                }
            }

            public DogfightScoreSummary BuildSummary(uint seed)
            {
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i].EverEngaged)
                    {
                        _engagementCount++;
                    }
                }

                var engagementMeanTicks = QuantizeMean(_engagedTicks, _states.Length);
                var ttfsMeanTicks = QuantizeMean(_ttfsTotalTicks, _ttfsCount);
                var ttkMeanTicks = QuantizeMean(_ttkTotalTicks, _ttkCount);

                var coneRatio = QuantizeRatio(_timeInConeTicks, _timeInRangeTicks);
                var breakoffSuccess = QuantizeRatio(_breakoffSuccesses, _breakoffAttempts);
                var curvature = QuantizeRatio(_curvatureHeadingSum, _curvatureDistanceSum);
                var engineDisabled = QuantizeRatio(_engineDisabledTicks, _engagedTicks);
                var weaponDisabled = QuantizeRatio(_weaponDisabledTicks, _engagedTicks);

                var avgSpeed = _engagedTicks > 0 ? _speedSum / _engagedTicks : 0.0;
                var avgTurn = _engagedTicks > 0 ? _headingSum / _engagedTicks : 0.0;
                var avgEngineSpeed = _engineDisabledTicks > 0 ? _engineSpeedSum / _engineDisabledTicks : 0.0;
                var avgEngineTurn = _engineDisabledTicks > 0 ? _engineHeadingSum / _engineDisabledTicks : 0.0;

                var engineSpeedRatio = QuantizeRatio(avgEngineSpeed, avgSpeed);
                var engineTurnRatio = QuantizeRatio(avgEngineTurn, avgTurn);

                var durationSeconds = _sampleTicks * _fixedDelta;
                var targetChangesPerMinute = durationSeconds > 0f
                    ? (int)math.round((_targetChangeCount * 60f) / durationSeconds)
                    : 0;

                return new DogfightScoreSummary
                {
                    Seed = seed,
                    CraftCount = _states.Length,
                    EngagementCount = _engagementCount,
                    EngagementMeanTicks = engagementMeanTicks,
                    TtfsMeanTicks = ttfsMeanTicks,
                    TtkMeanTicks = ttkMeanTicks,
                    ConeRatioPermille = coneRatio,
                    BreakoffSuccessPermille = breakoffSuccess,
                    CurvaturePermille = curvature,
                    TargetChangesPerMinute = targetChangesPerMinute,
                    EngineDisabledPermille = engineDisabled,
                    WeaponDisabledPermille = weaponDisabled,
                    EngineSpeedRatioPermille = engineSpeedRatio,
                    EngineTurnRatioPermille = engineTurnRatio
                };
            }

            private void UpdateBreakoffState(ref CraftScoreState state, StrikeCraftDogfightPhase phase, bool hasTarget, float distance, uint tick)
            {
                if (phase == StrikeCraftDogfightPhase.BreakOff && state.LastPhase != StrikeCraftDogfightPhase.BreakOff)
                {
                    state.BreakoffActive = true;
                    state.BreakoffStartTick = tick;
                    state.BreakoffStartDistance = hasTarget ? distance : 0f;
                    state.BreakoffDistanceSatisfied = false;
                    _breakoffAttempts++;
                }

                if (!state.BreakoffActive)
                {
                    return;
                }

                var elapsed = tick - state.BreakoffStartTick;
                if (!state.BreakoffDistanceSatisfied && hasTarget && elapsed >= _breakoffDistanceTicks)
                {
                    if (distance > state.BreakoffStartDistance + BreakoffDistanceEpsilon)
                    {
                        state.BreakoffDistanceSatisfied = true;
                    }
                }

                if (phase == StrikeCraftDogfightPhase.Rejoin &&
                    state.BreakoffDistanceSatisfied &&
                    elapsed <= _breakoffRejoinMaxTicks)
                {
                    _breakoffSuccesses++;
                    state.BreakoffActive = false;
                    return;
                }

                if (elapsed > _breakoffRejoinMaxTicks)
                {
                    state.BreakoffActive = false;
                }
            }
        }

        private struct CraftScoreState
        {
            public Entity Entity;
            public Entity LastTarget;
            public StrikeCraftDogfightPhase LastPhase;
            public bool HasLastPose;
            public float3 LastPosition;
            public float3 LastForward;
            public bool BreakoffActive;
            public uint BreakoffStartTick;
            public float BreakoffStartDistance;
            public bool BreakoffDistanceSatisfied;
            public bool EverEngaged;
        }

        private struct DogfightScoreSummary
        {
            public uint Seed;
            public int CraftCount;
            public int EngagementCount;
            public int EngagementMeanTicks;
            public int TtfsMeanTicks;
            public int TtkMeanTicks;
            public int ConeRatioPermille;
            public int BreakoffSuccessPermille;
            public int CurvaturePermille;
            public int TargetChangesPerMinute;
            public int EngineDisabledPermille;
            public int WeaponDisabledPermille;
            public int EngineSpeedRatioPermille;
            public int EngineTurnRatioPermille;

            public string ToLineString()
            {
                return $"DogfightScoreSummary(seed={Seed},craft={CraftCount},engaged={EngagementCount},engagementMeanTicks={EngagementMeanTicks}," +
                       $"ttfsMeanTicks={TtfsMeanTicks},ttkMeanTicks={TtkMeanTicks},coneRatioPermille={ConeRatioPermille}," +
                       $"breakoffSuccessPermille={BreakoffSuccessPermille},curvaturePermille={CurvaturePermille}," +
                       $"targetChangesPerMinute={TargetChangesPerMinute},engineDisabledPermille={EngineDisabledPermille}," +
                       $"weaponDisabledPermille={WeaponDisabledPermille},engineSpeedRatioPermille={EngineSpeedRatioPermille}," +
                       $"engineTurnRatioPermille={EngineTurnRatioPermille})";
            }
        }

        private struct ScenarioVariant
        {
            public ScenarioTransform Transform;
            public float ProjectileSpeedMultiplier;
            public uint Seed;
        }

        private enum ScenarioTransform : byte
        {
            None = 0,
            MirrorX = 1,
            Rotate180 = 2
        }

        private static float3 ResolveForward(in LocalTransform transform, in VesselMovement movement)
        {
            if (math.lengthsq(movement.Velocity) > 0.0001f)
            {
                return math.normalizesafe(movement.Velocity);
            }

            return math.forward(transform.Rotation);
        }

        private static int QuantizeMean(long total, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (int)math.round((float)total / count);
        }

        private static int QuantizeRatio(double numerator, double denominator)
        {
            if (denominator <= 0.0)
            {
                return 0;
            }

            var ratio = numerator / denominator;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                return 0;
            }

            return (int)math.round((float)(ratio * RatioPermille));
        }

        private static float ResolveMaxWeaponRange(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasBuffer<WeaponMount>(entity))
            {
                return 0f;
            }

            var mounts = entityManager.GetBuffer<WeaponMount>(entity);
            if (mounts.Length == 0)
            {
                return 0f;
            }

            var hasSubsystems = entityManager.HasBuffer<SubsystemHealth>(entity);
            var hasDisabled = entityManager.HasBuffer<SubsystemDisabled>(entity);
            DynamicBuffer<SubsystemHealth> subsystems = default;
            DynamicBuffer<SubsystemDisabled> disabled = default;
            if (hasSubsystems)
            {
                subsystems = entityManager.GetBuffer<SubsystemHealth>(entity);
            }
            if (hasDisabled)
            {
                disabled = entityManager.GetBuffer<SubsystemDisabled>(entity);
            }

            var maxRange = 0f;
            for (int i = 0; i < mounts.Length; i++)
            {
                var mount = mounts[i];
                if (mount.IsEnabled == 0)
                {
                    continue;
                }

                if (hasSubsystems)
                {
                    if (hasDisabled)
                    {
                        if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabled))
                        {
                            continue;
                        }
                    }
                    else if (Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Weapons))
                    {
                        continue;
                    }
                }

                maxRange = math.max(maxRange, mount.Weapon.MaxRange);
            }

            return maxRange;
        }

        private static bool IsSubsystemDisabled(EntityManager entityManager, Entity entity, SubsystemType type)
        {
            if (!entityManager.HasBuffer<SubsystemHealth>(entity))
            {
                return false;
            }

            var subsystems = entityManager.GetBuffer<SubsystemHealth>(entity);
            if (entityManager.HasBuffer<SubsystemDisabled>(entity))
            {
                var disabled = entityManager.GetBuffer<SubsystemDisabled>(entity);
                return Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabled, type);
            }

            return Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, type);
        }
    }
}
#endif
