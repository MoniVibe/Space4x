using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Updates grudge modifiers based on active grudges.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrudgeModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeBehavior>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (behavior, speciesGrudges, factionGrudges, personalGrudges, modifiers) in
                SystemAPI.Query<
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<SpeciesGrudgeEntry>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>,
                    RefRW<GrudgeModifiers>>())
            {
                float maxCombatBonus = 0;
                float maxTradePenalty = 0;
                float maxDiploPenalty = 0;
                float maxCoopPenalty = 0;
                float maxMoralePenalty = 0;
                float maxTargetBoost = 0;

                // Process species grudges
                if (behavior.ValueRO.CaresAboutSpeciesHistory != 0)
                {
                    for (int i = 0; i < speciesGrudges.Length; i++)
                    {
                        var grudge = speciesGrudges[i].Grudge;
                        if (grudge.Intensity == 0) continue;

                        maxCombatBonus = math.max(maxCombatBonus,
                            GrudgeHelpers.GetCombatBonus(grudge.Intensity, behavior.ValueRO.Vengefulness));
                        maxTradePenalty = math.max(maxTradePenalty, grudge.Intensity * 0.008f);
                        maxDiploPenalty = math.max(maxDiploPenalty, grudge.Intensity * 0.01f);
                        maxTargetBoost = math.max(maxTargetBoost,
                            GrudgeHelpers.GetTargetPriorityBoost(grudge.Intensity));
                    }
                }

                // Process faction grudges
                if (behavior.ValueRO.ActsOnFactionGrudges != 0)
                {
                    for (int i = 0; i < factionGrudges.Length; i++)
                    {
                        var grudge = factionGrudges[i];
                        if (grudge.Intensity == 0) continue;

                        maxCombatBonus = math.max(maxCombatBonus,
                            GrudgeHelpers.GetCombatBonus(grudge.Intensity, behavior.ValueRO.Vengefulness));
                        maxTradePenalty = math.max(maxTradePenalty, grudge.Intensity * 0.006f);
                        maxDiploPenalty = math.max(maxDiploPenalty, grudge.Intensity * 0.008f);
                        maxCoopPenalty = math.max(maxCoopPenalty,
                            GrudgeHelpers.GetCooperationPenalty(grudge.Intensity));
                    }
                }

                // Process personal grudges
                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    var grudge = personalGrudges[i];
                    if (grudge.Intensity == 0) continue;

                    maxCombatBonus = math.max(maxCombatBonus,
                        GrudgeHelpers.GetCombatBonus(grudge.Intensity, behavior.ValueRO.Vengefulness));
                    maxCoopPenalty = math.max(maxCoopPenalty,
                        GrudgeHelpers.GetCooperationPenalty(grudge.Intensity));
                    maxMoralePenalty = math.max(maxMoralePenalty, grudge.Intensity * 0.003f);
                }

                modifiers.ValueRW.CombatBonusVsGrudgeTarget = (half)maxCombatBonus;
                modifiers.ValueRW.TradePenalty = (half)maxTradePenalty;
                modifiers.ValueRW.DiplomacyPenalty = (half)maxDiploPenalty;
                modifiers.ValueRW.CooperationPenalty = (half)maxCoopPenalty;
                modifiers.ValueRW.MoralePenalty = (half)maxMoralePenalty;
                modifiers.ValueRW.TargetPriorityBoost = (half)maxTargetBoost;
            }
        }
    }

    /// <summary>
    /// Decays grudge intensity over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrudgeDecaySystem : ISystem
    {
        private uint _lastDecayTick;
        private const uint DecayInterval = 50;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeBehavior>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            if (currentTick - _lastDecayTick < DecayInterval)
            {
                return;
            }
            _lastDecayTick = currentTick;

            foreach (var (behavior, speciesGrudgesBuffer, factionGrudgesBuffer, personalGrudgesBuffer) in
                SystemAPI.Query<
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<SpeciesGrudgeEntry>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>>())
            {
                var speciesGrudges = speciesGrudgesBuffer;
                var factionGrudges = factionGrudgesBuffer;
                var personalGrudges = personalGrudgesBuffer;

                float forgivenessMultiplier = behavior.ValueRO.Forgiveness * 0.01f;

                // Decay species grudges
                for (int i = 0; i < speciesGrudges.Length; i++)
                {
                    var entry = speciesGrudges[i];
                    var grudge = entry.Grudge;

                    if (grudge.Intensity == 0) continue;

                    float decayRate = GrudgeHelpers.GetDecayRate(grudge.Severity);
                    decayRate *= forgivenessMultiplier;

                    int newIntensity = (int)(grudge.Intensity - decayRate);
                    grudge.Intensity = (byte)math.max(0, newIntensity);
                    entry.Grudge = grudge;
                    speciesGrudges[i] = entry;
                }

                // Decay faction grudges
                for (int i = 0; i < factionGrudges.Length; i++)
                {
                    var grudge = factionGrudges[i];
                    if (grudge.Intensity == 0) continue;

                    float decayRate = GrudgeHelpers.GetDecayRate(grudge.Severity);
                    decayRate *= forgivenessMultiplier;

                    int newIntensity = (int)(grudge.Intensity - decayRate);
                    grudge.Intensity = (byte)math.max(0, newIntensity);
                    factionGrudges[i] = grudge;
                }

                // Decay personal grudges
                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    var grudge = personalGrudges[i];
                    if (grudge.Intensity == 0) continue;

                    // Personal grudges decay faster with forgiveness
                    float decayRate = 0.02f * (1f + forgivenessMultiplier);

                    int newIntensity = (int)(grudge.Intensity - decayRate);
                    grudge.Intensity = (byte)math.max(0, newIntensity);
                    personalGrudges[i] = grudge;
                }
            }
        }
    }

    /// <summary>
    /// Processes grievance requests and creates grudges.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrievanceProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrievanceRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, behavior, factionGrudgesBuffer, personalGrudgesBuffer, entity) in
                SystemAPI.Query<
                    RefRO<GrievanceRequest>,
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>>()
                    .WithEntityAccess())
            {
                var factionGrudges = factionGrudgesBuffer;
                var personalGrudges = personalGrudgesBuffer;

                var req = request.ValueRO;
                byte intensity = GrudgeHelpers.GetBaseIntensity(req.Type, req.Severity);

                // Vengefulness affects initial intensity
                float vengeMult = 1f + (behavior.ValueRO.Vengefulness - 50) * 0.01f;
                intensity = (byte)math.min(100, (int)(intensity * vengeMult));

                if (req.IsFactionLevel != 0)
                {
                    // Check if grudge already exists
                    bool found = false;
                    for (int i = 0; i < factionGrudges.Length; i++)
                    {
                        var existing = factionGrudges[i];
                        if (existing.OffendingFaction == req.Offender && existing.Type == req.Type)
                        {
                            // Renew existing grudge
                            existing.Intensity = (byte)math.min(100, existing.Intensity + intensity / 2);
                            existing.LastRenewedTick = currentTick;
                            factionGrudges[i] = existing;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        factionGrudges.Add(new FactionGrudge
                        {
                            OffendingFaction = req.Offender,
                            Type = req.Type,
                            Severity = req.Severity,
                            Intensity = intensity,
                            OriginTick = currentTick,
                            LastRenewedTick = currentTick,
                            SeekingRevenge = (byte)(intensity >= behavior.ValueRO.RevengeThreshold ? 1 : 0),
                            ReparationsDemanded = 0,
                            ReparationsReceived = 0
                        });
                    }
                }
                else
                {
                    // Personal grudge
                    bool found = false;
                    for (int i = 0; i < personalGrudges.Length; i++)
                    {
                        var existing = personalGrudges[i];
                        if (existing.Offender == req.Offender)
                        {
                            // Renew/intensify existing grudge
                            existing.Intensity = (byte)math.min(100, existing.Intensity + intensity / 2);
                            existing.SeekingRevenge = (byte)(existing.Intensity >= behavior.ValueRO.RevengeThreshold ? 1 : 0);
                            personalGrudges[i] = existing;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        personalGrudges.Add(new PersonalGrudge
                        {
                            Offender = req.Offender,
                            Type = req.Type,
                            Intensity = intensity,
                            OriginTick = currentTick,
                            IsInherited = 0,
                            SeekingRevenge = (byte)(intensity >= behavior.ValueRO.RevengeThreshold ? 1 : 0)
                        });
                    }
                }

                ecb.RemoveComponent<GrievanceRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Processes grievance resolution attempts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XGrievanceResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrievanceResolutionRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, behavior, factionGrudgesBuffer, personalGrudgesBuffer, resolutions, entity) in
                SystemAPI.Query<
                    RefRO<GrievanceResolutionRequest>,
                    RefRO<GrudgeBehavior>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>,
                    DynamicBuffer<GrievanceResolutionEvent>>()
                    .WithEntityAccess())
            {
                var factionGrudges = factionGrudgesBuffer;
                var personalGrudges = personalGrudgesBuffer;

                var req = request.ValueRO;
                // Try faction grudges first
                for (int i = 0; i < factionGrudges.Length; i++)
                {
                    var grudge = factionGrudges[i];
                    if (grudge.OffendingFaction != req.Target) continue;

                    byte reduction = GrudgeHelpers.GetResolutionReduction(
                        req.ResolutionType, grudge.Type, req.OfferedValue);

                    // Forgiveness affects resolution effectiveness
                    float forgivenessMult = 1f + behavior.ValueRO.Forgiveness * 0.005f;
                    reduction = (byte)math.min(100, (int)(reduction * forgivenessMult));

                    // Apply reparations
                    if (req.ResolutionType == GrievanceResolutionType.Reparations)
                    {
                        grudge.ReparationsReceived += req.OfferedValue;
                        if (grudge.ReparationsDemanded > 0)
                        {
                            float ratio = grudge.ReparationsReceived / grudge.ReparationsDemanded;
                            reduction = (byte)math.min(100, (int)(reduction * ratio));
                        }
                    }

                    resolutions.Add(new GrievanceResolutionEvent
                    {
                        FormerOffender = req.Target,
                        Type = grudge.Type,
                        Resolution = req.ResolutionType,
                        IntensityReduction = reduction,
                        ResolvedTick = currentTick
                    });

                    grudge.Intensity = (byte)math.max(0, grudge.Intensity - reduction);
                    grudge.SeekingRevenge = (byte)(grudge.Intensity >= behavior.ValueRO.RevengeThreshold ? 1 : 0);
                    factionGrudges[i] = grudge;
                }

                // Try personal grudges
                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    var grudge = personalGrudges[i];
                    if (grudge.Offender != req.Target) continue;

                    byte reduction = GrudgeHelpers.GetResolutionReduction(
                        req.ResolutionType, grudge.Type, req.OfferedValue);

                    float forgivenessMult = 1f + behavior.ValueRO.Forgiveness * 0.01f;
                    reduction = (byte)math.min(100, (int)(reduction * forgivenessMult));

                    resolutions.Add(new GrievanceResolutionEvent
                    {
                        FormerOffender = req.Target,
                        Type = grudge.Type,
                        Resolution = req.ResolutionType,
                        IntensityReduction = reduction,
                        ResolvedTick = currentTick
                    });

                    grudge.Intensity = (byte)math.max(0, grudge.Intensity - reduction);
                    grudge.SeekingRevenge = (byte)(grudge.Intensity >= behavior.ValueRO.RevengeThreshold ? 1 : 0);
                    personalGrudges[i] = grudge;
                }

                ecb.RemoveComponent<GrievanceResolutionRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Propagates species grudges to new entities of that species.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSpeciesGrudgeInheritanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RaceId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This would typically run on entity creation or when species is assigned
            // For now, it ensures entities inherit species grudges based on their race

            foreach (var (raceId, behavior, speciesGrudges) in
                SystemAPI.Query<RefRO<RaceId>, RefRO<GrudgeBehavior>, DynamicBuffer<SpeciesGrudgeEntry>>())
            {
                if (behavior.ValueRO.CaresAboutSpeciesHistory == 0)
                {
                    continue;
                }

                // In a full implementation, this would look up the species-wide grudge registry
                // and copy relevant grudges to the entity's buffer
            }
        }
    }

    /// <summary>
    /// Cleans up expired/zeroed grudges.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XGrudgeCleanupSystem : ISystem
    {
        private uint _lastCleanupTick;
        private const uint CleanupInterval = 500;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeBehavior>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            if (currentTick - _lastCleanupTick < CleanupInterval)
            {
                return;
            }
            _lastCleanupTick = currentTick;

            foreach (var (factionGrudgesBuffer, personalGrudgesBuffer) in
                SystemAPI.Query<DynamicBuffer<FactionGrudge>, DynamicBuffer<PersonalGrudge>>())
            {
                var factionGrudges = factionGrudgesBuffer;
                var personalGrudges = personalGrudgesBuffer;

                // Remove zeroed faction grudges
                for (int i = factionGrudges.Length - 1; i >= 0; i--)
                {
                    if (factionGrudges[i].Intensity == 0)
                    {
                        factionGrudges.RemoveAt(i);
                    }
                }

                // Remove zeroed personal grudges
                for (int i = personalGrudges.Length - 1; i >= 0; i--)
                {
                    if (personalGrudges[i].Intensity == 0)
                    {
                        personalGrudges.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for grudge system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XGrudgeTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrudgeBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalEntities = 0;
            int speciesGrudgesTotal = 0;
            int factionGrudgesTotal = 0;
            int personalGrudgesTotal = 0;
            int seekingRevenge = 0;
            float avgIntensity = 0;

            foreach (var (speciesGrudges, factionGrudges, personalGrudges) in
                SystemAPI.Query<
                    DynamicBuffer<SpeciesGrudgeEntry>,
                    DynamicBuffer<FactionGrudge>,
                    DynamicBuffer<PersonalGrudge>>())
            {
                totalEntities++;
                speciesGrudgesTotal += speciesGrudges.Length;
                factionGrudgesTotal += factionGrudges.Length;
                personalGrudgesTotal += personalGrudges.Length;

                for (int i = 0; i < factionGrudges.Length; i++)
                {
                    avgIntensity += factionGrudges[i].Intensity;
                    if (factionGrudges[i].SeekingRevenge != 0)
                        seekingRevenge++;
                }

                for (int i = 0; i < personalGrudges.Length; i++)
                {
                    avgIntensity += personalGrudges[i].Intensity;
                    if (personalGrudges[i].SeekingRevenge != 0)
                        seekingRevenge++;
                }
            }

            int totalGrudges = factionGrudgesTotal + personalGrudgesTotal;
            if (totalGrudges > 0)
            {
                avgIntensity /= totalGrudges;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Grudge] Entities: {totalEntities}, Species: {speciesGrudgesTotal}, Faction: {factionGrudgesTotal}, Personal: {personalGrudgesTotal}, Revenge: {seekingRevenge}, AvgIntensity: {avgIntensity:F1}");
        }
    }
}

