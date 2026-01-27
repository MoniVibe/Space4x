using Unity.Entities;
using Unity.Burst;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// System that decays grudges over time.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct GrudgeDecaySystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            GrudgeConfig config = GrudgeHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<GrudgeConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            // Decay grudges
            foreach (var query in 
                SystemAPI.Query<DynamicBuffer<EntityGrudge>, RefRO<GrudgeBehavior>>()
                    .WithEntityAccess())
            {
                var grudges = query.Item1;
                var behavior = query.Item2;

                for (int i = grudges.Length - 1; i >= 0; i--)
                {
                    var grudge = grudges[i];
                    uint ticksSinceRenewal = currentTick - grudge.LastRenewedTick;
                    
                    grudge.Intensity = GrudgeHelpers.ApplyDecay(
                        grudge.Intensity,
                        ticksSinceRenewal,
                        config,
                        behavior.ValueRO);
                    
                    grudge.Severity = GrudgeHelpers.GetSeverity(grudge.Intensity);
                    
                    // Remove forgotten grudges
                    if (grudge.Intensity == 0)
                    {
                        grudges.RemoveAt(i);
                        continue;
                    }
                    
                    grudges[i] = grudge;
                }
            }

            // Handle entities without behavior component (use defaults)
            GrudgeBehavior defaultBehavior = GrudgeHelpers.CreateDefaultBehavior();
            
            foreach (var query in 
                SystemAPI.Query<DynamicBuffer<EntityGrudge>>()
                    .WithNone<GrudgeBehavior>()
                    .WithEntityAccess())
            {
                var grudges = query.Item1;

                for (int i = grudges.Length - 1; i >= 0; i--)
                {
                    var grudge = grudges[i];
                    uint ticksSinceRenewal = currentTick - grudge.LastRenewedTick;
                    
                    grudge.Intensity = GrudgeHelpers.ApplyDecay(
                        grudge.Intensity,
                        ticksSinceRenewal,
                        config,
                        defaultBehavior);
                    
                    grudge.Severity = GrudgeHelpers.GetSeverity(grudge.Intensity);
                    
                    if (grudge.Intensity == 0)
                    {
                        grudges.RemoveAt(i);
                        continue;
                    }
                    
                    grudges[i] = grudge;
                }
            }
        }
    }

    /// <summary>
    /// System that checks for vendetta escalation and revenge seeking.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GrudgeDecaySystem))]
    [BurstCompile]
    public partial struct GrudgeEscalationSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            GrudgeConfig config = GrudgeHelpers.DefaultConfig;
            if (SystemAPI.TryGetSingleton<GrudgeConfig>(out var existingConfig))
            {
                config = existingConfig;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Check for vendetta escalation
            foreach (var query in 
                SystemAPI.Query<DynamicBuffer<EntityGrudge>, DynamicBuffer<VendettaEvent>, RefRO<GrudgeBehavior>>()
                    .WithEntityAccess())
            {
                var grudges = query.Item1;
                var vendettaEvents = query.Item2;
                var behavior = query.Item3;
                var entity = query.Item4;

                for (int i = 0; i < grudges.Length; i++)
                {
                    var grudge = grudges[i];
                    
                    // Check if just reached vendetta threshold
                    if (grudge.Intensity >= config.VendettaThreshold && grudge.Severity == GrudgeSeverity.Vendetta)
                    {
                        // Check if we already emitted this vendetta
                        bool alreadyEmitted = false;
                        for (int j = 0; j < vendettaEvents.Length; j++)
                        {
                            if (vendettaEvents[j].OffenderEntity == grudge.OffenderEntity)
                            {
                                alreadyEmitted = true;
                                break;
                            }
                        }
                        
                        if (!alreadyEmitted)
                        {
                            vendettaEvents.Add(new VendettaEvent
                            {
                                OffenderEntity = grudge.OffenderEntity,
                                Type = grudge.Type,
                                Tick = currentTick
                            });
                        }
                    }
                    
                    // Check if should seek revenge
                    if (GrudgeHelpers.ShouldSeekRevenge(grudge, behavior.ValueRO))
                    {
                        // Add seeking revenge tag if not already present
                        if (!SystemAPI.HasComponent<SeekingRevengeTag>(entity))
                        {
                            ecb.AddComponent(entity, new SeekingRevengeTag
                            {
                                TargetEntity = grudge.OffenderEntity,
                                GrudgeType = grudge.Type,
                                Intensity = grudge.Intensity
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that processes add grudge requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(GrudgeDecaySystem))]
    [BurstCompile]
    public partial struct AddGrudgeSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process add grudge requests
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<AddGrudgeRequest>>()
                    .WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (SystemAPI.HasBuffer<EntityGrudge>(req.VictimEntity))
                {
                    var grudges = SystemAPI.GetBuffer<EntityGrudge>(req.VictimEntity);
                    GrudgeHelpers.AddGrudge(
                        ref grudges,
                        req.OffenderEntity,
                        req.Type,
                        req.BaseIntensity,
                        currentTick,
                        req.IsPublic);
                }
                
                ecb.DestroyEntity(entity);
            }
        }
    }
}

