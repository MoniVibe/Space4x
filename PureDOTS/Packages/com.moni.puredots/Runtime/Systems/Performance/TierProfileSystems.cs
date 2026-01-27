using System;
using System.Globalization;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Performance
{
    /// <summary>
    /// Ensures TierProfileSettings exists and applies the active profile to the shared cadence + universal budgets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct TierProfileBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Ensure singleton exists early so downstream systems can require it.
            if (!SystemAPI.HasSingleton<TierProfileSettings>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, TierProfileSettings.CreateDefaults(TierProfileId.Mid));
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // No-op; settings are updated by authoring or runtime config in the future.
        }
    }

    /// <summary>
    /// Applies environment/CLI overrides for headless performance profiles.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TierProfileBootstrapSystem))]
    public partial struct TierProfileEnvOverrideSystem : ISystem
    {
        private const string TierProfileEnv = "PUREDOTS_TIER_PROFILE";
        private const string TierProfileArg = "--tier-profile";
        private const string HeadlessTierProfileEnv = "PUREDOTS_HEADLESS_TIER_PROFILE";
        private const string HeadlessTierProfileArg = "--headless-tier-profile";
        private const string HeadlessCadenceMultEnv = "PUREDOTS_HEADLESS_CADENCE_MULT";
        private const string HeadlessCadenceMultArg = "--headless-cadence-mult";

        private bool _applied;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TierProfileSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_applied)
            {
                state.Enabled = false;
                return;
            }

            var entity = SystemAPI.GetSingletonEntity<TierProfileSettings>();
            var settings = SystemAPI.GetSingleton<TierProfileSettings>();
            var changed = false;

            if (TryResolveTierProfile(out var profileOverride))
            {
                settings = TierProfileSettings.CreateDefaults(profileOverride);
                changed = true;
            }

            if (TryResolveCadenceMultiplier(out var cadenceMult))
            {
                settings = ApplyCadenceScale(settings, cadenceMult);
                changed = true;
            }

            if (changed)
            {
                settings.Version = settings.Version == 0 ? 1u : settings.Version + 1u;
                state.EntityManager.SetComponentData(entity, settings);
            }

            _applied = true;
            state.Enabled = false;
        }

        private static bool TryResolveTierProfile(out TierProfileId profile)
        {
            profile = TierProfileId.Mid;

            var env = global::System.Environment.GetEnvironmentVariable(HeadlessTierProfileEnv);
            if (TryParseTierProfile(env, out profile))
            {
                return true;
            }

            env = global::System.Environment.GetEnvironmentVariable(TierProfileEnv);
            if (TryParseTierProfile(env, out profile))
            {
                return true;
            }

            var args = global::System.Environment.GetCommandLineArgs();
            if (TryGetArgValue(args, HeadlessTierProfileArg, out var argValue) && TryParseTierProfile(argValue, out profile))
            {
                return true;
            }

            if (TryGetArgValue(args, TierProfileArg, out argValue) && TryParseTierProfile(argValue, out profile))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveCadenceMultiplier(out float multiplier)
        {
            multiplier = 1f;
            var raw = global::System.Environment.GetEnvironmentVariable(HeadlessCadenceMultEnv);
            if (TryParseMultiplier(raw, out multiplier))
            {
                multiplier = Math.Max(1f, multiplier);
                return true;
            }

            var args = global::System.Environment.GetCommandLineArgs();
            if (TryGetArgValue(args, HeadlessCadenceMultArg, out var argValue) && TryParseMultiplier(argValue, out multiplier))
            {
                multiplier = Math.Max(1f, multiplier);
                return true;
            }

            return false;
        }

        private static bool TryParseTierProfile(string raw, out TierProfileId profile)
        {
            profile = TierProfileId.Mid;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "laptop":
                    profile = TierProfileId.Laptop;
                    return true;
                case "mid":
                    profile = TierProfileId.Mid;
                    return true;
                case "high":
                    profile = TierProfileId.High;
                    return true;
                case "cinematic":
                    profile = TierProfileId.Cinematic;
                    return true;
                case "debug":
                    profile = TierProfileId.Debug;
                    return true;
            }

            return false;
        }

        private static bool TryParseMultiplier(string raw, out float value)
        {
            value = 1f;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static TierProfileSettings ApplyCadenceScale(TierProfileSettings settings, float multiplier)
        {
            if (multiplier <= 1f)
            {
                return settings;
            }

            settings.Tier0SensorCadenceTicks = ScaleCadence(settings.Tier0SensorCadenceTicks, multiplier);
            settings.Tier1SensorCadenceTicks = ScaleCadence(settings.Tier1SensorCadenceTicks, multiplier);
            settings.Tier2SensorCadenceTicks = ScaleCadence(settings.Tier2SensorCadenceTicks, multiplier);
            settings.Tier3SensorCadenceTicks = ScaleCadence(settings.Tier3SensorCadenceTicks, multiplier);

            settings.Tier0EvaluationCadenceTicks = ScaleCadence(settings.Tier0EvaluationCadenceTicks, multiplier);
            settings.Tier1EvaluationCadenceTicks = ScaleCadence(settings.Tier1EvaluationCadenceTicks, multiplier);
            settings.Tier2EvaluationCadenceTicks = ScaleCadence(settings.Tier2EvaluationCadenceTicks, multiplier);
            settings.Tier3EvaluationCadenceTicks = ScaleCadence(settings.Tier3EvaluationCadenceTicks, multiplier);

            settings.Tier0ResolutionCadenceTicks = ScaleCadence(settings.Tier0ResolutionCadenceTicks, multiplier);
            settings.Tier1ResolutionCadenceTicks = ScaleCadence(settings.Tier1ResolutionCadenceTicks, multiplier);
            settings.Tier2ResolutionCadenceTicks = ScaleCadence(settings.Tier2ResolutionCadenceTicks, multiplier);
            settings.Tier3ResolutionCadenceTicks = ScaleCadence(settings.Tier3ResolutionCadenceTicks, multiplier);

            settings.TierHysteresisTicks = (uint)ScaleCadence((int)settings.TierHysteresisTicks, multiplier);

            return settings;
        }

        private static int ScaleCadence(int value, float multiplier)
        {
            if (value <= 0)
            {
                return value;
            }

            var scaled = (int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
            return Math.Max(1, scaled);
        }

        private static bool TryGetArgValue(string[] args, string key, out string value)
        {
            value = null;
            if (args == null || args.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (arg.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        value = args[i + 1];
                        return !string.IsNullOrWhiteSpace(value);
                    }

                    return false;
                }

                if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(key.Length + 1);
                    return !string.IsNullOrWhiteSpace(value);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Applies TierProfileSettings into MindCadenceSettings + UniversalPerformanceBudget.
    /// This makes the policy knobs authoritative without duplicating systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MindCadenceBootstrapSystem))]
    public partial struct TierProfileApplySystem : ISystem
    {
        private uint _lastAppliedVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TierProfileSettings>();
            state.RequireForUpdate<MindCadenceSettings>();
            state.RequireForUpdate<UniversalPerformanceBudget>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var profile = SystemAPI.GetSingleton<TierProfileSettings>();
            if (profile.Version == _lastAppliedVersion)
            {
                return;
            }

            // Drive cadence from Tier0 (full) defaults. Per-entity tier can further gate.
            var cadence = SystemAPI.GetSingletonRW<MindCadenceSettings>();
            cadence.ValueRW.SensorCadenceTicks = profile.Tier0SensorCadenceTicks;
            cadence.ValueRW.EvaluationCadenceTicks = profile.Tier0EvaluationCadenceTicks;
            cadence.ValueRW.ResolutionCadenceTicks = profile.Tier0ResolutionCadenceTicks;

            // Aggregate universal budgets: Tier0 + Tier1 are the main consumers, Tier2 is best-effort, Tier3 is 0.
            var budget = SystemAPI.GetSingletonRW<UniversalPerformanceBudget>();
            budget.ValueRW.MaxPerceptionChecksPerTick =
                profile.Tier0MaxPerceptionChecksPerTick + profile.Tier1MaxPerceptionChecksPerTick + profile.Tier2MaxPerceptionChecksPerTick;
            budget.ValueRW.MaxTacticalDecisionsPerTick =
                profile.Tier0MaxTacticalDecisionsPerTick + profile.Tier1MaxTacticalDecisionsPerTick + profile.Tier2MaxTacticalDecisionsPerTick;

            _lastAppliedVersion = profile.Version;
        }
    }

    /// <summary>
    /// Ensures AIFidelityTier exists for entities that participate in AI/perception pipelines.
    /// Structural change is restricted to initialization.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct AIFidelityTierEnsureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<SenseCapability>>()
                         .WithNone<AIFidelityTier>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new AIFidelityTier
                {
                    Tier = AILODTier.Tier1_Reduced,
                    LastChangeTick = tick,
                    ReasonMask = 0
                });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<AIBehaviourArchetype>>()
                         .WithNone<AIFidelityTier>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new AIFidelityTier
                {
                    Tier = AILODTier.Tier1_Reduced,
                    LastChangeTick = tick,
                    ReasonMask = 0
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Assigns AI tiers using low-cost interest heuristics (no distance queries yet).
    /// Tier0 = has a presentation companion (usually near player); otherwise Tier1.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PerceptionSystemGroup))]
    public partial struct AIInterestTierAssignmentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TierProfileSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var profile = SystemAPI.GetSingleton<TierProfileSettings>();
            var hysteresis = profile.TierHysteresisTicks == 0 ? 1u : profile.TierHysteresisTicks;

            foreach (var (tier, entity) in SystemAPI.Query<RefRW<AIFidelityTier>>().WithEntityAccess())
            {
                var current = tier.ValueRO;
                if (time.Tick - current.LastChangeTick < hysteresis)
                {
                    continue;
                }

                // Heuristic interest: visible companion implies high interest.
                var desired = SystemAPI.HasComponent<CompanionPresentation>(entity)
                    ? AILODTier.Tier0_Full
                    : AILODTier.Tier1_Reduced;

                if (desired == current.Tier)
                {
                    continue;
                }

                current.Tier = desired;
                current.LastChangeTick = time.Tick;
                current.ReasonMask = (byte)(desired == AILODTier.Tier0_Full ? 1 : 0);
                tier.ValueRW = current;
            }
        }
    }
}
