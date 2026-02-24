using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Lifecycle;
using PureDOTS.Runtime.Social;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds individuals with PureDOTS social and lifecycle components.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Lifecycle.ReproductionStateBootstrapSystem))]
    public partial struct Space4XSocialLifecycleBootstrapSystem : ISystem
    {
        private const float DaysPerYear = 365f;
        private const float MinAdultYears = 22f;
        private const float MaxAdultYears = 45f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var ticksPerDay = ComputeTicksPerDay(timeState.FixedDeltaTime);
            var lifecycleConfig = BuildLifecycleConfig(ticksPerDay);
            var mortalityConfig = BuildMortalityConfig(ticksPerDay);
            var relationConfig = BuildRelationConfig(ticksPerDay);
            var currentTick = timeState.Tick;

            var em = state.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<SimIndividualTag>>().WithEntityAccess())
            {
                EnsureSocial(ref ecb, em, entity, relationConfig);
                EnsureLifecycle(ref ecb, em, entity, currentTick, ticksPerDay, lifecycleConfig, mortalityConfig);
                EnsureHealth(ref ecb, em, entity);
            }

            ecb.Playback(em);
        }

        private static void EnsureSocial(ref EntityCommandBuffer ecb, EntityManager em, Entity entity, RelationConfig relationConfig)
        {
            if (!em.HasBuffer<EntityRelation>(entity))
            {
                ecb.AddBuffer<EntityRelation>(entity);
            }

            if (!em.HasComponent<RelationConfig>(entity))
            {
                ecb.AddComponent(entity, relationConfig);
            }

            if (!em.HasComponent<SocialStanding>(entity))
            {
                ecb.AddComponent(entity, default(SocialStanding));
            }

            if (!em.HasBuffer<FirstMeetingEvent>(entity))
            {
                ecb.AddBuffer<FirstMeetingEvent>(entity);
            }

            if (!em.HasBuffer<RelationChangedEvent>(entity))
            {
                ecb.AddBuffer<RelationChangedEvent>(entity);
            }
        }

        private static void EnsureLifecycle(
            ref EntityCommandBuffer ecb,
            EntityManager em,
            Entity entity,
            uint currentTick,
            float ticksPerDay,
            in LifecycleConfig lifecycleConfig,
            in MortalityConfig mortalityConfig)
        {
            if (!em.HasComponent<LifecycleConfig>(entity))
            {
                ecb.AddComponent(entity, lifecycleConfig);
            }

            if (!em.HasComponent<MortalityConfig>(entity))
            {
                ecb.AddComponent(entity, mortalityConfig);
            }

            if (!em.HasComponent<AgingEffects>(entity))
            {
                ecb.AddComponent(entity, default(AgingEffects));
            }

            if (!em.HasComponent<LifecycleState>(entity))
            {
                var lifecycle = CreateSeededLifecycle(entity, currentTick, ticksPerDay, lifecycleConfig);
                ecb.AddComponent(entity, lifecycle);
            }

            if (!em.HasComponent<ReproductionState>(entity))
            {
                var maturityAge = ResolveMaturityAge(lifecycleConfig);
                ecb.AddComponent(entity, new ReproductionState
                {
                    MaturityAge = maturityAge,
                    ReproductionCooldown = ticksPerDay * 180f,
                    LastReproductionTick = 0,
                    OffspringCount = 0,
                    MaxOffspring = 4,
                    CanReproduce = 0,
                    IsPregnant = 0
                });
            }

            if (!em.HasComponent<OffspringConfig>(entity))
            {
                ecb.AddComponent(entity, new OffspringConfig
                {
                    OffspringTypeId = default,
                    MinOffspring = 1,
                    MaxOffspring = 1,
                    GestationTicks = ticksPerDay * 270f,
                    InheritanceStrength = 0.5f,
                    MutationChance = 0.05f
                });
            }
        }

        private static void EnsureHealth(ref EntityCommandBuffer ecb, EntityManager em, Entity entity)
        {
            if (!em.HasComponent<Health>(entity))
            {
                ecb.AddComponent(entity, new Health
                {
                    Current = 100f,
                    Max = 100f,
                    RegenRate = 0f,
                    LastDamageTick = 0
                });
            }

            if (!em.HasComponent<DeathState>(entity))
            {
                ecb.AddComponent(entity, new DeathState
                {
                    IsDead = false,
                    DeathTick = 0,
                    KillerEntity = Entity.Null,
                    KillingBlowType = DamageType.True
                });
            }

            if (!em.HasBuffer<DeathEvent>(entity))
            {
                ecb.AddBuffer<DeathEvent>(entity);
            }
        }

        private static float ComputeTicksPerDay(float fixedDeltaTime)
        {
            float dt = math.max(0.0001f, fixedDeltaTime);
            return math.max(1f, 86400f / dt);
        }

        private static LifecycleConfig BuildLifecycleConfig(float ticksPerDay)
        {
            float yearTicks = ticksPerDay * DaysPerYear;

            return new LifecycleConfig
            {
                Type = LifecycleType.Linear,
                AdvanceTrigger = StageTrigger.Age,
                JuvenileDuration = yearTicks * 18f,
                MatureDuration = yearTicks * 30f,
                ElderDuration = yearTicks * 20f,
                DecayDuration = yearTicks * 5f,
                ProgressRate = 1f,
                MaxStage = (byte)LifecycleStage.Decaying
            };
        }

        private static MortalityConfig BuildMortalityConfig(float ticksPerDay)
        {
            float yearTicks = ticksPerDay * DaysPerYear;
            float lifespan = yearTicks * 70f;

            return new MortalityConfig
            {
                NaturalLifespan = lifespan,
                LifespanVariance = yearTicks * 10f,
                DeathChancePerTick = 1f / math.max(1f, lifespan * 80f),
                MinimumAge = yearTicks * 40f,
                CanDieOfAge = 1,
                CanResurrect = 0,
                LeavesCorpse = 1
            };
        }

        private static RelationConfig BuildRelationConfig(float ticksPerDay)
        {
            return new RelationConfig
            {
                DecayRatePerDay = 0.5f,
                MinIntensity = -50,
                MaxIntensity = 100,
                FamiliarityPerInteraction = 2,
                TrustPerPositiveInteraction = 2,
                TrustLossPerNegative = 3,
                DecayCheckInterval = ticksPerDay
            };
        }

        private static float ResolveMaturityAge(in LifecycleConfig config)
        {
            float nascent = LifecycleHelpers.GetStageDuration(LifecycleStage.Nascent, config);
            float seed = LifecycleHelpers.GetStageDuration(LifecycleStage.Seed, config);
            return nascent + seed + config.JuvenileDuration;
        }

        private static LifecycleState CreateSeededLifecycle(
            Entity entity,
            uint currentTick,
            float ticksPerDay,
            in LifecycleConfig config)
        {
            float ageYears = math.lerp(MinAdultYears, MaxAdultYears, Deterministic01(entity, 0x51C3u));
            float ageTicks = ageYears * DaysPerYear * ticksPerDay;

            ResolveStageFromAge(ageTicks, config, out var stage, out var stageProgress, out var stageElapsed);

            uint stageElapsedTicks = (uint)math.clamp(stageElapsed, 0f, uint.MaxValue);
            uint birthTicks = (uint)math.clamp(ageTicks, 0f, uint.MaxValue);

            uint stageEnteredTick = currentTick > stageElapsedTicks ? currentTick - stageElapsedTicks : 0u;
            uint birthTick = currentTick > birthTicks ? currentTick - birthTicks : 0u;

            return new LifecycleState
            {
                CurrentStage = stage,
                Type = config.Type,
                StageProgress = stageProgress,
                TotalAge = ageTicks,
                StageEnteredTick = stageEnteredTick,
                BirthTick = birthTick,
                StageCount = (byte)math.clamp((int)stage, 0, 255),
                CanAdvance = 1,
                IsFrozen = 0
            };
        }

        private static void ResolveStageFromAge(
            float ageTicks,
            in LifecycleConfig config,
            out LifecycleStage stage,
            out float stageProgress,
            out float stageElapsed)
        {
            float remaining = ageTicks;

            float nascent = LifecycleHelpers.GetStageDuration(LifecycleStage.Nascent, config);
            if (remaining <= nascent)
            {
                stage = LifecycleStage.Nascent;
                stageElapsed = remaining;
                stageProgress = ResolveProgress(remaining, nascent);
                return;
            }

            remaining -= nascent;
            float seed = LifecycleHelpers.GetStageDuration(LifecycleStage.Seed, config);
            if (remaining <= seed)
            {
                stage = LifecycleStage.Seed;
                stageElapsed = remaining;
                stageProgress = ResolveProgress(remaining, seed);
                return;
            }

            remaining -= seed;
            if (remaining <= config.JuvenileDuration)
            {
                stage = LifecycleStage.Juvenile;
                stageElapsed = remaining;
                stageProgress = ResolveProgress(remaining, config.JuvenileDuration);
                return;
            }

            remaining -= config.JuvenileDuration;
            if (remaining <= config.MatureDuration)
            {
                stage = LifecycleStage.Mature;
                stageElapsed = remaining;
                stageProgress = ResolveProgress(remaining, config.MatureDuration);
                return;
            }

            remaining -= config.MatureDuration;
            if (remaining <= config.ElderDuration)
            {
                stage = LifecycleStage.Elder;
                stageElapsed = remaining;
                stageProgress = ResolveProgress(remaining, config.ElderDuration);
                return;
            }

            remaining -= config.ElderDuration;
            stage = LifecycleStage.Decaying;
            stageElapsed = remaining;
            stageProgress = ResolveProgress(remaining, config.DecayDuration);
        }

        private static float ResolveProgress(float elapsed, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            return math.saturate(elapsed / duration);
        }

        private static float Deterministic01(Entity entity, uint salt)
        {
            uint hash = math.hash(new uint2((uint)entity.Index, salt));
            return (hash & 0x00FFFFFF) / (float)0x01000000;
        }
    }
}
