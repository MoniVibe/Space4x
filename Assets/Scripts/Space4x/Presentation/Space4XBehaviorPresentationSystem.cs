using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    /// <summary>
    /// Stub behavior → visuals mapping for Space4X.
    /// Keeps behavior readable (moving/mining/returning/depleted) without modeling deeper crew/officer nuance yet.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    [UpdateBefore(typeof(Space4XRenderTintSyncSystem))]
    public partial struct Space4XBehaviorPresentationSystem : ISystem
    {
        private uint _lastTick;
        private byte _tickInitialized;
        private EntityQuery _craftQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _strikeCraftQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _tickInitialized = 0;
            _lastTick = 0;

            _craftQuery = SystemAPI.QueryBuilder()
                .WithAll<CraftPresentationTag, MiningVessel, MiningJob, CraftVisualState, RenderTint, MaterialPropertyOverride>()
                .Build();

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<AsteroidPresentationTag, Asteroid, AsteroidVisualState, RenderTint, MaterialPropertyOverride>()
                .Build();

            _strikeCraftQuery = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftPresentationTag, StrikeCraftProfile, StrikeCraftVisualState, RenderTint, MaterialPropertyOverride>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (_craftQuery.IsEmptyIgnoreFilter && _asteroidQuery.IsEmptyIgnoreFilter && _strikeCraftQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = ResolveDeltaTime(timeState);
            if (deltaTime <= 0f)
            {
                return;
            }

            var timeSeconds = timeState.WorldSeconds;

            // Craft: map mining job → visual state and tint pulse.
            if (!_craftQuery.IsEmptyIgnoreFilter)
            {
                var activeCarriers = new NativeHashMap<Entity, byte>(math.max(1, _craftQuery.CalculateEntityCount()), Allocator.Temp);

                foreach (var (vessel, job) in SystemAPI.Query<RefRO<MiningVessel>, RefRO<MiningJob>>())
                {
                    if (vessel.ValueRO.CarrierEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (job.ValueRO.State == MiningJobState.None)
                    {
                        continue;
                    }

                    activeCarriers.TryAdd(vessel.ValueRO.CarrierEntity, 1);
                }

                foreach (var (job, visual, tint, material, entity) in SystemAPI
                             .Query<RefRO<MiningJob>, RefRW<CraftVisualState>, RefRW<RenderTint>, RefRO<MaterialPropertyOverride>>()
                             .WithAll<CraftPresentationTag>()
                             .WithEntityAccess())
                {
                    var mapped = MapCraftState(job.ValueRO.State);
                    if (visual.ValueRO.State != mapped)
                    {
                        visual.ValueRW.State = mapped;
                        visual.ValueRW.StateTimer = 0f;
                    }
                    else
                    {
                        visual.ValueRW.StateTimer += deltaTime;
                    }

                    var baseColor = material.ValueRO.BaseColor;
                    var pulse = ResolvePulse(mapped, timeSeconds, entity.Index);
                    tint.ValueRW.Value = new float4(baseColor.xyz * pulse, baseColor.w);
                }

                foreach (var (visual, tint, material, entity) in SystemAPI
                             .Query<RefRW<CarrierVisualState>, RefRW<RenderTint>, RefRO<MaterialPropertyOverride>>()
                             .WithAll<CarrierPresentationTag>()
                             .WithEntityAccess())
                {
                    bool isActive = activeCarriers.ContainsKey(entity);
                    var desired = isActive ? CarrierVisualStateType.Mining : CarrierVisualStateType.Idle;
                    if (visual.ValueRO.State != desired)
                    {
                        visual.ValueRW.State = desired;
                        visual.ValueRW.StateTimer = 0f;
                    }
                    else
                    {
                        visual.ValueRW.StateTimer += deltaTime;
                    }

                    var baseColor = material.ValueRO.BaseColor;
                    var pulse = isActive
                        ? 0.90f + 0.18f * math.sin(timeSeconds * 2.2f + entity.Index * 0.05f)
                        : 1f;
                    tint.ValueRW.Value = new float4(baseColor.xyz * pulse, baseColor.w);
                }

                activeCarriers.Dispose();
            }

            // Asteroids: depletion ratio → dimming, with a subtle pulse if still rich.
            if (!_asteroidQuery.IsEmptyIgnoreFilter)
            {
                foreach (var (asteroid, visual, tint, material, entity) in SystemAPI
                             .Query<RefRO<Asteroid>, RefRW<AsteroidVisualState>, RefRW<RenderTint>, RefRO<MaterialPropertyOverride>>()
                             .WithAll<AsteroidPresentationTag>()
                             .WithEntityAccess())
                {
                    float ratio = asteroid.ValueRO.MaxResourceAmount > 0f
                        ? asteroid.ValueRO.ResourceAmount / math.max(0.0001f, asteroid.ValueRO.MaxResourceAmount)
                        : 1f;

                    ratio = math.saturate(ratio);

                    var stateType = ratio <= 0.10f ? AsteroidVisualStateType.Depleted : AsteroidVisualStateType.Full;
                    visual.ValueRW.State = stateType;
                    visual.ValueRW.DepletionRatio = 1f - ratio;
                    visual.ValueRW.StateTimer += deltaTime;

                    var baseColor = material.ValueRO.BaseColor;
                    float brightness = stateType == AsteroidVisualStateType.Depleted
                        ? 0.20f
                        : 0.88f + 0.12f * math.sin(timeSeconds * 1.4f + entity.Index * 0.03f);

                    tint.ValueRW.Value = new float4(baseColor.xyz * brightness, baseColor.w);
                }
            }

            // Strike craft: attack run phase → visual state and pulse.
            if (!_strikeCraftQuery.IsEmptyIgnoreFilter)
            {
                foreach (var (profile, visual, tint, material, entity) in SystemAPI
                             .Query<RefRO<StrikeCraftProfile>, RefRW<StrikeCraftVisualState>, RefRW<RenderTint>, RefRO<MaterialPropertyOverride>>()
                             .WithAll<StrikeCraftPresentationTag>()
                             .WithEntityAccess())
                {
                    var mapped = MapStrikeCraftState(profile.ValueRO.Phase);
                    if (visual.ValueRO.State != mapped)
                    {
                        visual.ValueRW.State = mapped;
                        visual.ValueRW.StateTimer = 0f;
                    }
                    else
                    {
                        visual.ValueRW.StateTimer += deltaTime;
                    }

                    var baseColor = material.ValueRO.BaseColor;
                    var pulse = ResolveStrikeCraftPulse(mapped, timeSeconds, entity.Index);
                    tint.ValueRW.Value = new float4(baseColor.xyz * pulse, baseColor.w);
                }
            }
        }

        private static CraftVisualStateType MapCraftState(MiningJobState state)
        {
            return state switch
            {
                MiningJobState.None => CraftVisualStateType.Idle,
                MiningJobState.Mining => CraftVisualStateType.Mining,
                MiningJobState.ReturningToCarrier => CraftVisualStateType.Returning,
                MiningJobState.TransferringResources => CraftVisualStateType.Returning,
                _ => CraftVisualStateType.Moving
            };
        }

        private static float ResolvePulse(CraftVisualStateType state, float timeSeconds, int entityIndex)
        {
            return state switch
            {
                CraftVisualStateType.Mining => 0.85f + 0.25f * math.sin(timeSeconds * 5.8f + entityIndex * 0.07f),
                CraftVisualStateType.Returning => 0.90f + 0.15f * math.sin(timeSeconds * 3.2f + entityIndex * 0.05f),
                CraftVisualStateType.Moving => 0.95f + 0.08f * math.sin(timeSeconds * 2.0f + entityIndex * 0.03f),
                _ => 1f
            };
        }

        private static StrikeCraftVisualStateType MapStrikeCraftState(AttackRunPhase phase)
        {
            return phase switch
            {
                AttackRunPhase.Docked => StrikeCraftVisualStateType.Docked,
                AttackRunPhase.Launching => StrikeCraftVisualStateType.FormingUp,
                AttackRunPhase.FormUp => StrikeCraftVisualStateType.FormingUp,
                AttackRunPhase.Approach => StrikeCraftVisualStateType.Approaching,
                AttackRunPhase.Execute => StrikeCraftVisualStateType.Engaging,
                AttackRunPhase.Disengage => StrikeCraftVisualStateType.Disengaging,
                AttackRunPhase.CombatAirPatrol => StrikeCraftVisualStateType.Engaging,
                AttackRunPhase.Return => StrikeCraftVisualStateType.Returning,
                AttackRunPhase.Landing => StrikeCraftVisualStateType.Returning,
                _ => StrikeCraftVisualStateType.Returning
            };
        }

        private static float ResolveStrikeCraftPulse(StrikeCraftVisualStateType state, float timeSeconds, int entityIndex)
        {
            return state switch
            {
                StrikeCraftVisualStateType.Engaging => 0.82f + 0.28f * math.sin(timeSeconds * 6.4f + entityIndex * 0.09f),
                StrikeCraftVisualStateType.Approaching => 0.88f + 0.18f * math.sin(timeSeconds * 3.6f + entityIndex * 0.07f),
                StrikeCraftVisualStateType.FormingUp => 0.92f + 0.12f * math.sin(timeSeconds * 2.4f + entityIndex * 0.05f),
                StrikeCraftVisualStateType.Returning => 0.90f + 0.14f * math.sin(timeSeconds * 2.2f + entityIndex * 0.05f),
                StrikeCraftVisualStateType.Disengaging => 0.86f + 0.18f * math.sin(timeSeconds * 3.2f + entityIndex * 0.06f),
                _ => 1f
            };
        }

        private float ResolveDeltaTime(in TimeState timeState)
        {
            var tick = timeState.Tick;
            if (_tickInitialized == 0)
            {
                _tickInitialized = 1;
                _lastTick = tick;
                return 0f;
            }

            var deltaTicks = tick >= _lastTick ? tick - _lastTick : 0u;
            _lastTick = tick;

            if (deltaTicks == 0u)
            {
                return 0f;
            }

            var fixedDt = math.max(timeState.FixedDeltaTime, 1e-4f);
            return fixedDt * deltaTicks;
        }
    }
}
