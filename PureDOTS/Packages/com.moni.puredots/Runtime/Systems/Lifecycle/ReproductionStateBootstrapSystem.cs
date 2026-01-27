using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Lifecycle;

namespace PureDOTS.Systems.Lifecycle
{
    /// <summary>
    /// Optional bootstrap system that initializes ReproductionState for mature entities that can reproduce.
    /// This ensures entities have proper reproduction components without requiring game-side authoring.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct ReproductionStateBootstrapSystem : ISystem
    {
        private ComponentLookup<ReproductionState> _reproductionStateLookup;
        private ComponentLookup<OffspringConfig> _offspringConfigLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _reproductionStateLookup = state.GetComponentLookup<ReproductionState>(true);
            _offspringConfigLookup = state.GetComponentLookup<OffspringConfig>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            _reproductionStateLookup.Update(ref state);
            _offspringConfigLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            var job = new BootstrapReproductionStateJob
            {
                Ecb = ecb,
                CurrentTick = timeState.Tick,
                ReproductionStateLookup = _reproductionStateLookup,
                OffspringConfigLookup = _offspringConfigLookup
            };
            job.Run();
        }

        [BurstCompile]
        partial struct BootstrapReproductionStateJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<ReproductionState> ReproductionStateLookup;
            [ReadOnly] public ComponentLookup<OffspringConfig> OffspringConfigLookup;

            void Execute(
                [EntityIndexInQuery] int entityInQueryIndex,
                Entity entity,
                in LifecycleState lifecycle)
            {
                // Only add reproduction components for mature entities
                if (lifecycle.CurrentStage != LifecycleStage.Mature && 
                    lifecycle.CurrentStage != LifecycleStage.Elder)
                {
                    return;
                }

                // Add ReproductionState if missing
                if (!ReproductionStateLookup.HasComponent(entity))
                {
                    float maturityAge = lifecycle.TotalAge;
                    var reproductionState = ReproductionBootstrapDefaults.CreateReproductionState(maturityAge);
                    Ecb.AddComponent(entity, reproductionState);
                }

                // Add OffspringConfig if missing
                if (!OffspringConfigLookup.HasComponent(entity))
                {
                    var offspringConfig = ReproductionBootstrapDefaults.CreateOffspringConfig();
                    Ecb.AddComponent(entity, offspringConfig);
                }
            }
        }
    }

    static class ReproductionBootstrapDefaults
    {
        public const float DefaultReproductionCooldown = 1000f;
        public const byte DefaultMaxOffspring = 10;
        public const float DefaultGestationTicks = 5000f;
        public const float DefaultInheritanceStrength = 0.5f;
        public const float DefaultMutationChance = 0.1f;

        public static ReproductionState CreateReproductionState(float maturityAge)
        {
            return new ReproductionState
            {
                MaturityAge = maturityAge,
                ReproductionCooldown = DefaultReproductionCooldown,
                LastReproductionTick = 0,
                OffspringCount = 0,
                MaxOffspring = DefaultMaxOffspring,
                CanReproduce = 1,
                IsPregnant = 0
            };
        }

        public static OffspringConfig CreateOffspringConfig()
        {
            return new OffspringConfig
            {
                OffspringTypeId = default,
                MinOffspring = 1,
                MaxOffspring = 1,
                GestationTicks = DefaultGestationTicks,
                InheritanceStrength = DefaultInheritanceStrength,
                MutationChance = DefaultMutationChance
            };
        }
    }
}

