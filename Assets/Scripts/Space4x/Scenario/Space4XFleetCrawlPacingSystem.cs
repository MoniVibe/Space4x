using System;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SystemEnv = System.Environment;

namespace Space4x.Scenario
{
    public struct Space4XFleetCrawlPacingConfig : IComponentData
    {
        public int DifficultyTier;
        public float MetaPressureResist;
        public float MetaDebtControl;

        public float TargetRoomSecondsTier1;
        public float TargetRoomSecondsTier2;
        public float TargetRoomSecondsTier3;
        public float TargetRoomSecondsTier4;
        public float TargetRoomSecondsTier5;

        public float DebtGainTier1;
        public float DebtGainTier2;
        public float DebtGainTier3;
        public float DebtGainTier4;
        public float DebtGainTier5;

        public float GraceSeconds;
        public float LateStartSeconds;
        public float PhaseASlope;
        public float PhaseBSlope;
        public float PhaseCExponent;
        public float PhaseCScale;
        public float PressureCap;

        public float KPressure;
        public float KDebt;
        public float WaveBudgetCap;
        public float BaseEliteChance;
        public float EliteFromPressure;
        public float EliteFromDebt;
        public float HazardFromPressure;
        public float HazardFromDebt;
        public float RewardDecayFromPressure;
    }

    public struct Space4XFleetCrawlPacingRuntimeState : IComponentData
    {
        public uint Tick;
        public float RoomTimeSeconds;
        public float TargetRoomSeconds;
        public float RoomPressure;
        public float RunDebt;
        public float ThreatScalar;
        public float WaveBudgetMultiplier;
        public float EliteChance;
        public float HazardRateMultiplier;
        public float RewardDecayMultiplier;
        public float EnemySpeedMultiplier;
        public float EnemyDamageMultiplier;
        public float EnemyCooldownMultiplier;
    }

    public struct Space4XFleetCrawlPacingHostileBaseline : IComponentData
    {
        public float CarrierSpeed;
        public float CarrierAcceleration;
        public float CarrierDeceleration;
        public float CarrierTurnSpeed;
        public float MovementBaseSpeed;
        public float MovementAcceleration;
        public float MovementDeceleration;
        public float MovementTurnSpeed;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XFleetCrawlWeaponBaseline : IBufferElementData
    {
        public float BaseDamage;
        public ushort CooldownTicks;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XScenarioActionSystem))]
    public partial struct Space4XFleetCrawlPacingSystem : ISystem
    {
        private const string EnabledEnv = "SPACE4X_FLEETCRAWL_PACING";
        private const string ConfigPathEnv = "SPACE4X_FLEETCRAWL_PACING_PATH";
        private const string DifficultyEnv = "SPACE4X_FLEETCRAWL_DIFFICULTY";
        private const string MetaPressureResistEnv = "SPACE4X_FLEETCRAWL_META_PRESSURE_RESIST";
        private const string MetaDebtControlEnv = "SPACE4X_FLEETCRAWL_META_DEBT_CONTROL";
        private const string DefaultConfigRelativePath = "Docs/Simulation/Space4X_FleetCrawl_Pacing_Model_v0.json";

        private byte _initialized;
        private uint _nextLogTick;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            var scenarioId = scenarioInfo.ScenarioId.ToString();
            if (!IsFleetCrawlScenario(scenarioId))
            {
                return;
            }

            if (_initialized == 0)
            {
                Initialize(ref state);
                _initialized = 1;
            }

            if (!SystemAPI.TryGetSingleton<Space4XFleetCrawlPacingConfig>(out var config))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var fixedDt = math.max(1e-5f, time.FixedDeltaTime);
            var roomTimeSeconds = math.max(0f, (time.Tick - runtime.StartTick) * fixedDt);

            ResolveDifficultyProfile(in config, out var targetSeconds, out var debtGainPerSecond);

            var roomPressure = ComputeRoomPressure(in config, roomTimeSeconds);
            var overTargetSeconds = math.max(0f, roomTimeSeconds - targetSeconds);
            var runDebt = math.max(0f, overTargetSeconds * debtGainPerSecond * (1f - config.MetaDebtControl));
            var threatScalar = 1f + roomPressure * config.KPressure + runDebt * config.KDebt;
            var waveBudgetMult = math.clamp(threatScalar, 1f, math.max(1f, config.WaveBudgetCap));
            var eliteChance = math.clamp(config.BaseEliteChance + roomPressure * config.EliteFromPressure + runDebt * config.EliteFromDebt, 0f, 1f);
            var hazardRateMult = 1f + roomPressure * config.HazardFromPressure + runDebt * config.HazardFromDebt;
            var rewardDecayMult = 1f / (1f + math.max(0f, config.RewardDecayFromPressure) * roomPressure);

            // Threat is intentionally bounded for v0 so pressure reads strong without instant stat spikes.
            var enemySpeedMult = math.min(2.5f, 1f + (threatScalar - 1f) * 0.35f);
            var enemyDamageMult = math.min(2.5f, 1f + (threatScalar - 1f) * 0.25f);
            var enemyCooldownMult = math.max(0.45f, 1f - (threatScalar - 1f) * 0.12f);

            EnsureHostileBaselines(ref state);
            ApplyHostilePacing(ref state, enemySpeedMult, enemyDamageMult, enemyCooldownMult);
            UpdateRuntimeState(ref state, time.Tick, roomTimeSeconds, targetSeconds, roomPressure, runDebt, threatScalar,
                waveBudgetMult, eliteChance, hazardRateMult, rewardDecayMult, enemySpeedMult, enemyDamageMult, enemyCooldownMult);

            var logIntervalTicks = (uint)math.max(1, math.round(10f / fixedDt));
            if (time.Tick >= _nextLogTick)
            {
                _nextLogTick = time.Tick + logIntervalTicks;
                Debug.Log($"[Space4XFleetCrawlPacing] t={roomTimeSeconds:0.0}s pressure={roomPressure:0.###} debt={runDebt:0.###} threat={threatScalar:0.###} " +
                          $"wave={waveBudgetMult:0.###} elite={eliteChance:0.###} speedX={enemySpeedMult:0.###} dmgX={enemyDamageMult:0.###}");
            }
        }

        private static bool IsEnabled()
        {
            var value = SystemEnv.GetEnvironmentVariable(EnabledEnv);
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFleetCrawlScenario(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return false;
            }

            return scenarioId.StartsWith("space4x_fleetcrawl", StringComparison.OrdinalIgnoreCase);
        }

        private void Initialize(ref SystemState state)
        {
            var em = state.EntityManager;
            var configEntity = SystemAPI.TryGetSingletonEntity<Space4XFleetCrawlPacingConfig>(out var existing)
                ? existing
                : em.CreateEntity();

            if (!em.HasComponent<Space4XFleetCrawlPacingConfig>(configEntity))
            {
                em.AddComponent<Space4XFleetCrawlPacingConfig>(configEntity);
            }

            if (!em.HasComponent<Space4XFleetCrawlPacingRuntimeState>(configEntity))
            {
                em.AddComponent<Space4XFleetCrawlPacingRuntimeState>(configEntity);
            }

            var config = BuildDefaultConfig();
            TryApplyJsonOverrides(ref config);
            ApplyRuntimeOverrides(ref config);

            em.SetComponentData(configEntity, config);
            em.SetComponentData(configEntity, new Space4XFleetCrawlPacingRuntimeState
            {
                Tick = 0u,
                RoomTimeSeconds = 0f,
                TargetRoomSeconds = ResolveTargetRoomSeconds(in config, config.DifficultyTier),
                RoomPressure = 0f,
                RunDebt = 0f,
                ThreatScalar = 1f,
                WaveBudgetMultiplier = 1f,
                EliteChance = config.BaseEliteChance,
                HazardRateMultiplier = 1f,
                RewardDecayMultiplier = 1f,
                EnemySpeedMultiplier = 1f,
                EnemyDamageMultiplier = 1f,
                EnemyCooldownMultiplier = 1f
            });
        }

        private static Space4XFleetCrawlPacingConfig BuildDefaultConfig()
        {
            return new Space4XFleetCrawlPacingConfig
            {
                DifficultyTier = math.clamp(Space4XRunStartSelection.Difficulty, 1, 5),
                MetaPressureResist = 0f,
                MetaDebtControl = 0f,

                TargetRoomSecondsTier1 = 2160f,
                TargetRoomSecondsTier2 = 1920f,
                TargetRoomSecondsTier3 = 1800f,
                TargetRoomSecondsTier4 = 1620f,
                TargetRoomSecondsTier5 = 1440f,

                DebtGainTier1 = 0.00045f,
                DebtGainTier2 = 0.00055f,
                DebtGainTier3 = 0.00065f,
                DebtGainTier4 = 0.00080f,
                DebtGainTier5 = 0.00100f,

                GraceSeconds = 480f,
                LateStartSeconds = 1800f,
                PhaseASlope = 0.00035f,
                PhaseBSlope = 0.00095f,
                PhaseCExponent = 1.45f,
                PhaseCScale = 0.65f,
                PressureCap = 6f,

                KPressure = 0.22f,
                KDebt = 0.18f,
                WaveBudgetCap = 4.5f,
                BaseEliteChance = 0.05f,
                EliteFromPressure = 0.04f,
                EliteFromDebt = 0.03f,
                HazardFromPressure = 0.12f,
                HazardFromDebt = 0.08f,
                RewardDecayFromPressure = 0.09f
            };
        }

        private static void TryApplyJsonOverrides(ref Space4XFleetCrawlPacingConfig config)
        {
            var path = ResolveConfigPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[Space4XFleetCrawlPacing] Config file not found. Using defaults. path='{path}'");
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var root = JsonUtility.FromJson<PacingModelRoot>(json);
                if (root == null)
                {
                    Debug.LogWarning($"[Space4XFleetCrawlPacing] Failed to parse config JSON. Using defaults. path='{path}'");
                    return;
                }

                ApplyDifficultyProfiles(ref config, root.difficultyProfiles);
                ApplyPressureCurve(ref config, root.pressureCurve);
                ApplyThreatMapping(ref config, root.threatMapping);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XFleetCrawlPacing] Failed reading config. Using defaults. path='{path}' error={ex.Message}");
            }
        }

        private static string ResolveConfigPath()
        {
            var envPath = SystemEnv.GetEnvironmentVariable(ConfigPathEnv);
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return Path.IsPathRooted(envPath)
                    ? Path.GetFullPath(envPath)
                    : Path.GetFullPath(Path.Combine(GetProjectRoot(), envPath));
            }

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), DefaultConfigRelativePath));
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void ApplyRuntimeOverrides(ref Space4XFleetCrawlPacingConfig config)
        {
            if (TryParseIntEnv(DifficultyEnv, out var difficultyTier))
            {
                config.DifficultyTier = math.clamp(difficultyTier, 1, 5);
            }
            else
            {
                config.DifficultyTier = math.clamp(config.DifficultyTier, 1, 5);
            }

            if (TryParseFloatEnv(MetaPressureResistEnv, out var metaPressure))
            {
                config.MetaPressureResist = math.clamp(metaPressure, 0f, 0.95f);
            }

            if (TryParseFloatEnv(MetaDebtControlEnv, out var metaDebt))
            {
                config.MetaDebtControl = math.clamp(metaDebt, 0f, 0.95f);
            }
        }

        private static bool TryParseIntEnv(string key, out int value)
        {
            value = 0;
            var raw = SystemEnv.GetEnvironmentVariable(key);
            return !string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out value);
        }

        private static bool TryParseFloatEnv(string key, out float value)
        {
            value = 0f;
            var raw = SystemEnv.GetEnvironmentVariable(key);
            return !string.IsNullOrWhiteSpace(raw) &&
                   float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static void ApplyDifficultyProfiles(ref Space4XFleetCrawlPacingConfig config, DifficultyProfile[] profiles)
        {
            if (profiles == null || profiles.Length == 0)
            {
                return;
            }

            for (var i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                var tier = math.clamp(p.difficultyTier, 1, 5);
                var target = math.max(1f, p.targetRoomSeconds);
                var debtGain = math.max(0f, p.debtGainPerSecond);
                switch (tier)
                {
                    case 1:
                        config.TargetRoomSecondsTier1 = target;
                        config.DebtGainTier1 = debtGain;
                        break;
                    case 2:
                        config.TargetRoomSecondsTier2 = target;
                        config.DebtGainTier2 = debtGain;
                        break;
                    case 3:
                        config.TargetRoomSecondsTier3 = target;
                        config.DebtGainTier3 = debtGain;
                        break;
                    case 4:
                        config.TargetRoomSecondsTier4 = target;
                        config.DebtGainTier4 = debtGain;
                        break;
                    case 5:
                        config.TargetRoomSecondsTier5 = target;
                        config.DebtGainTier5 = debtGain;
                        break;
                }
            }
        }

        private static void ApplyPressureCurve(ref Space4XFleetCrawlPacingConfig config, PressureCurve curve)
        {
            if (curve == null)
            {
                return;
            }

            config.GraceSeconds = math.max(0f, curve.graceSeconds);
            config.LateStartSeconds = math.max(config.GraceSeconds, curve.lateStartSeconds);
            config.PhaseASlope = math.max(0f, curve.phaseASlope);
            config.PhaseBSlope = math.max(0f, curve.phaseBSlope);
            config.PhaseCExponent = math.max(0.1f, curve.phaseCExponent);
            config.PhaseCScale = math.max(0f, curve.phaseCScale);
            config.PressureCap = math.max(0.01f, curve.pressureCap);
        }

        private static void ApplyThreatMapping(ref Space4XFleetCrawlPacingConfig config, ThreatMapping mapping)
        {
            if (mapping == null)
            {
                return;
            }

            config.KPressure = math.max(0f, mapping.kPressure);
            config.KDebt = math.max(0f, mapping.kDebt);
            config.WaveBudgetCap = math.max(1f, mapping.waveBudgetCap);
            config.BaseEliteChance = math.max(0f, mapping.baseEliteChance);
            config.EliteFromPressure = math.max(0f, mapping.eliteFromPressure);
            config.EliteFromDebt = math.max(0f, mapping.eliteFromDebt);
            config.HazardFromPressure = math.max(0f, mapping.hazardFromPressure);
            config.HazardFromDebt = math.max(0f, mapping.hazardFromDebt);
            config.RewardDecayFromPressure = math.max(0f, mapping.rewardDecayFromPressure);
        }

        private static float ComputeRoomPressure(in Space4XFleetCrawlPacingConfig config, float roomTimeSeconds)
        {
            var phaseA = math.min(roomTimeSeconds, config.GraceSeconds) * config.PhaseASlope;
            var phaseB = math.max(0f, roomTimeSeconds - config.GraceSeconds) * config.PhaseBSlope;
            var lateT = math.max(0f, roomTimeSeconds - config.LateStartSeconds);
            var phaseC = math.pow(lateT / 600f, config.PhaseCExponent) * config.PhaseCScale;
            var raw = phaseA + phaseB + phaseC;
            return math.clamp(raw * (1f - config.MetaPressureResist), 0f, config.PressureCap);
        }

        private static float ResolveTargetRoomSeconds(in Space4XFleetCrawlPacingConfig config, int difficultyTier)
        {
            var tier = math.clamp(difficultyTier, 1, 5);
            return tier switch
            {
                1 => config.TargetRoomSecondsTier1,
                2 => config.TargetRoomSecondsTier2,
                3 => config.TargetRoomSecondsTier3,
                4 => config.TargetRoomSecondsTier4,
                _ => config.TargetRoomSecondsTier5
            };
        }

        private static float ResolveDebtGainPerSecond(in Space4XFleetCrawlPacingConfig config, int difficultyTier)
        {
            var tier = math.clamp(difficultyTier, 1, 5);
            return tier switch
            {
                1 => config.DebtGainTier1,
                2 => config.DebtGainTier2,
                3 => config.DebtGainTier3,
                4 => config.DebtGainTier4,
                _ => config.DebtGainTier5
            };
        }

        private static void ResolveDifficultyProfile(in Space4XFleetCrawlPacingConfig config, out float targetRoomSeconds, out float debtGainPerSecond)
        {
            targetRoomSeconds = ResolveTargetRoomSeconds(in config, config.DifficultyTier);
            debtGainPerSecond = ResolveDebtGainPerSecond(in config, config.DifficultyTier);
        }

        private void EnsureHostileBaselines(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (side, carrier, movement, entity) in SystemAPI
                         .Query<RefRO<ScenarioSide>, RefRO<Carrier>, RefRO<VesselMovement>>()
                         .WithNone<Prefab>()
                         .WithEntityAccess())
            {
                if (side.ValueRO.Side != 1)
                {
                    continue;
                }

                if (!em.HasComponent<Space4XFleetCrawlPacingHostileBaseline>(entity))
                {
                    ecb.AddComponent(entity, new Space4XFleetCrawlPacingHostileBaseline
                    {
                        CarrierSpeed = carrier.ValueRO.Speed,
                        CarrierAcceleration = carrier.ValueRO.Acceleration,
                        CarrierDeceleration = carrier.ValueRO.Deceleration,
                        CarrierTurnSpeed = carrier.ValueRO.TurnSpeed,
                        MovementBaseSpeed = movement.ValueRO.BaseSpeed,
                        MovementAcceleration = movement.ValueRO.Acceleration,
                        MovementDeceleration = movement.ValueRO.Deceleration,
                        MovementTurnSpeed = movement.ValueRO.TurnSpeed
                    });
                }

                if (!em.HasBuffer<Space4XFleetCrawlWeaponBaseline>(entity) && em.HasBuffer<WeaponMount>(entity))
                {
                    var mounts = em.GetBuffer<WeaponMount>(entity);
                    var baseline = ecb.AddBuffer<Space4XFleetCrawlWeaponBaseline>(entity);
                    for (var i = 0; i < mounts.Length; i++)
                    {
                        baseline.Add(new Space4XFleetCrawlWeaponBaseline
                        {
                            BaseDamage = mounts[i].Weapon.BaseDamage,
                            CooldownTicks = mounts[i].Weapon.CooldownTicks
                        });
                    }
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ApplyHostilePacing(ref SystemState state, float enemySpeedMult, float enemyDamageMult, float enemyCooldownMult)
        {
            var em = state.EntityManager;
            foreach (var (side, carrier, movement, entity) in SystemAPI
                         .Query<RefRO<ScenarioSide>, RefRW<Carrier>, RefRW<VesselMovement>>()
                         .WithNone<Prefab>()
                         .WithEntityAccess())
            {
                if (side.ValueRO.Side != 1 || !em.HasComponent<Space4XFleetCrawlPacingHostileBaseline>(entity))
                {
                    continue;
                }

                var baseline = em.GetComponentData<Space4XFleetCrawlPacingHostileBaseline>(entity);
                var carrierData = carrier.ValueRO;
                carrierData.Speed = baseline.CarrierSpeed * enemySpeedMult;
                carrierData.Acceleration = baseline.CarrierAcceleration * enemySpeedMult;
                carrierData.Deceleration = baseline.CarrierDeceleration * enemySpeedMult;
                carrierData.TurnSpeed = baseline.CarrierTurnSpeed * math.lerp(1f, enemySpeedMult, 0.6f);
                carrier.ValueRW = carrierData;

                var movementData = movement.ValueRO;
                movementData.BaseSpeed = baseline.MovementBaseSpeed * enemySpeedMult;
                movementData.Acceleration = baseline.MovementAcceleration * enemySpeedMult;
                movementData.Deceleration = baseline.MovementDeceleration * enemySpeedMult;
                movementData.TurnSpeed = baseline.MovementTurnSpeed * math.lerp(1f, enemySpeedMult, 0.6f);
                movement.ValueRW = movementData;

                if (!em.HasBuffer<WeaponMount>(entity) || !em.HasBuffer<Space4XFleetCrawlWeaponBaseline>(entity))
                {
                    continue;
                }

                var mounts = em.GetBuffer<WeaponMount>(entity);
                var weaponBaseline = em.GetBuffer<Space4XFleetCrawlWeaponBaseline>(entity);
                if (weaponBaseline.Length != mounts.Length)
                {
                    weaponBaseline.Clear();
                    for (var i = 0; i < mounts.Length; i++)
                    {
                        weaponBaseline.Add(new Space4XFleetCrawlWeaponBaseline
                        {
                            BaseDamage = mounts[i].Weapon.BaseDamage,
                            CooldownTicks = mounts[i].Weapon.CooldownTicks
                        });
                    }
                }

                var count = math.min(mounts.Length, weaponBaseline.Length);
                for (var i = 0; i < count; i++)
                {
                    var mount = mounts[i];
                    var baselineWeapon = weaponBaseline[i];
                    mount.Weapon.BaseDamage = math.max(0.01f, baselineWeapon.BaseDamage * enemyDamageMult);
                    mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(baselineWeapon.CooldownTicks * enemyCooldownMult));
                    mounts[i] = mount;
                }
            }
        }

        private void UpdateRuntimeState(
            ref SystemState state,
            uint tick,
            float roomTimeSeconds,
            float targetSeconds,
            float roomPressure,
            float runDebt,
            float threatScalar,
            float waveBudgetMult,
            float eliteChance,
            float hazardRateMult,
            float rewardDecayMult,
            float enemySpeedMult,
            float enemyDamageMult,
            float enemyCooldownMult)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFleetCrawlPacingRuntimeState>(out var entity))
            {
                return;
            }

            state.EntityManager.SetComponentData(entity, new Space4XFleetCrawlPacingRuntimeState
            {
                Tick = tick,
                RoomTimeSeconds = roomTimeSeconds,
                TargetRoomSeconds = targetSeconds,
                RoomPressure = roomPressure,
                RunDebt = runDebt,
                ThreatScalar = threatScalar,
                WaveBudgetMultiplier = waveBudgetMult,
                EliteChance = eliteChance,
                HazardRateMultiplier = hazardRateMult,
                RewardDecayMultiplier = rewardDecayMult,
                EnemySpeedMultiplier = enemySpeedMult,
                EnemyDamageMultiplier = enemyDamageMult,
                EnemyCooldownMultiplier = enemyCooldownMult
            });
        }

        [Serializable]
        private sealed class PacingModelRoot
        {
            public DifficultyProfile[] difficultyProfiles;
            public PressureCurve pressureCurve;
            public ThreatMapping threatMapping;
        }

        [Serializable]
        private sealed class DifficultyProfile
        {
            public int difficultyTier;
            public float targetRoomSeconds;
            public float debtGainPerSecond;
        }

        [Serializable]
        private sealed class PressureCurve
        {
            public float graceSeconds;
            public float lateStartSeconds;
            public float phaseASlope;
            public float phaseBSlope;
            public float phaseCExponent;
            public float phaseCScale;
            public float pressureCap;
        }

        [Serializable]
        private sealed class ThreatMapping
        {
            public float kPressure;
            public float kDebt;
            public float waveBudgetCap;
            public float baseEliteChance;
            public float eliteFromPressure;
            public float eliteFromDebt;
            public float hazardFromPressure;
            public float hazardFromDebt;
            public float rewardDecayFromPressure;
        }
    }
}
