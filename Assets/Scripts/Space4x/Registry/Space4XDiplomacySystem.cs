using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Updates relation scores and applies modifiers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XRelationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DiplomaticStatusEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (statuses, modifiers, faction, entity) in
                SystemAPI.Query<DynamicBuffer<DiplomaticStatusEntry>, DynamicBuffer<RelationModifier>, RefRO<Space4XFaction>>()
                    .WithEntityAccess())
            {
                var statusBuffer = statuses;
                var modifierBuffer = modifiers;

                // Process and decay modifiers
                for (int m = modifierBuffer.Length - 1; m >= 0; m--)
                {
                    var mod = modifierBuffer[m];

                    // Check for expiration
                    if (mod.RemainingTicks > 0)
                    {
                        mod.RemainingTicks--;
                        modifierBuffer[m] = mod;

                        if (mod.RemainingTicks == 0)
                        {
                            modifierBuffer.RemoveAt(m);
                            continue;
                        }
                    }

                    // Apply decay (every 1000 ticks)
                    if (currentTick % 1000 == 0 && (float)mod.DecayRate > 0)
                    {
                        if (mod.ScoreChange > 0)
                        {
                            mod.ScoreChange = (sbyte)math.max(0, mod.ScoreChange - (int)((float)mod.DecayRate));
                        }
                        else if (mod.ScoreChange < 0)
                        {
                            mod.ScoreChange = (sbyte)math.min(0, mod.ScoreChange + (int)((float)mod.DecayRate));
                        }

                        if (mod.ScoreChange == 0)
                        {
                            modifierBuffer.RemoveAt(m);
                        }
                        else
                        {
                            modifierBuffer[m] = mod;
                        }
                    }
                }

                // Update diplomatic statuses
                for (int s = 0; s < statusBuffer.Length; s++)
                {
                    var entry = statusBuffer[s];
                    var status = entry.Status;

                    // Calculate total modifier
                    int modifierTotal = 0;
                    for (int m = 0; m < modifiers.Length; m++)
                    {
                        var mod = modifiers[m];
                        if (mod.SourceFactionId == status.OtherFactionId || mod.SourceFactionId == 0)
                        {
                            modifierTotal += mod.ScoreChange;
                        }
                    }

                    // Apply natural drift
                    status.RelationScore = DiplomacyMath.CalculateRelationDrift(status.RelationScore, status.Stance);

                    // Clamp with modifiers
                    status.RelationScore = (sbyte)math.clamp(status.RelationScore + modifierTotal / 10, -100, 100);

                    // Trust drift toward 0
                    if ((float)status.Trust > 0)
                    {
                        status.Trust = (half)math.max(0, (float)status.Trust - 0.0001f);
                    }
                    else if ((float)status.Trust < 0)
                    {
                        status.Trust = (half)math.min(0, (float)status.Trust + 0.0001f);
                    }

                    entry.Status = status;
                    statusBuffer[s] = entry;
                }
            }
        }
    }

    /// <summary>
    /// Updates diplomatic stances based on relations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XRelationSystem))]
    public partial struct Space4XStanceUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DiplomaticStatusEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Only update stances periodically
            if (currentTick % 500 != 0)
            {
                return;
            }

            foreach (var statuses in SystemAPI.Query<DynamicBuffer<DiplomaticStatusEntry>>())
            {
                var statusBuffer = statuses;

                for (int s = 0; s < statusBuffer.Length; s++)
                {
                    var entry = statusBuffer[s];
                    var status = entry.Status;

                    // Skip if in war or has treaty-locked stance
                    if (status.Stance == DiplomaticStance.War ||
                        status.Stance == DiplomaticStance.Vassal ||
                        status.Stance == DiplomaticStance.Overlord)
                    {
                        continue;
                    }

                    // Minimum time between stance changes
                    if (currentTick < status.StanceChangeTick + 1000)
                    {
                        continue;
                    }

                    DiplomaticStance newStance = DiplomacyMath.DetermineStance(status.RelationScore, status.Stance);

                    if (newStance != status.Stance)
                    {
                        status.Stance = newStance;
                        status.StanceChangeTick = currentTick;
                    }

                    entry.Status = status;
                    statusBuffer[s] = entry;
                }
            }
        }
    }

    /// <summary>
    /// Manages active treaties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XTreatySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XTreaty>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (treaty, terms, entity) in
                SystemAPI.Query<RefRW<Space4XTreaty>, DynamicBuffer<TreatyTerm>>()
                    .WithEntityAccess())
            {
                if (treaty.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Check expiration
                if (treaty.ValueRO.ExpirationTick > 0 && currentTick > treaty.ValueRO.ExpirationTick)
                {
                    treaty.ValueRW.IsActive = 0;
                    continue;
                }

                // Check term fulfillment
                bool allTermsFulfilled = true;
                for (int t = 0; t < terms.Length; t++)
                {
                    var term = terms[t];
                    if (term.IsFulfilled == 0)
                    {
                        allTermsFulfilled = false;

                        // Check for violations
                        if (term.Type == TreatyTermType.PayTribute || term.Type == TreatyTermType.PayCredits)
                        {
                            // Would check if payment was made - simplified for now
                        }
                    }
                }

                // Treaties with unfulfilled recurring terms generate violations
                if (!allTermsFulfilled && currentTick % 1000 == 0)
                {
                    for (int t = 0; t < terms.Length; t++)
                    {
                        var term = terms[t];
                        if (term.IsFulfilled == 0)
                        {
                            if (term.ObligatedFactionId == treaty.ValueRO.PartyAFactionId)
                            {
                                treaty.ValueRW.ViolationsA++;
                            }
                            else if (term.ObligatedFactionId == treaty.ValueRO.PartyBFactionId)
                            {
                                treaty.ValueRW.ViolationsB++;
                            }
                        }
                    }

                    // Too many violations = treaty broken
                    if (treaty.ValueRO.ViolationsA > 3 || treaty.ValueRO.ViolationsB > 3)
                    {
                        treaty.ValueRW.IsActive = 0;
                        treaty.ValueRW.WasBroken = 1;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Processes diplomatic proposals and AI responses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XProposalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DiplomaticProposal>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (proposal, entity) in SystemAPI.Query<RefRW<DiplomaticProposal>>().WithEntityAccess())
            {
                if (proposal.ValueRO.Status != ProposalStatus.Pending)
                {
                    continue;
                }

                // Check expiration
                if (currentTick > proposal.ValueRO.ExpirationTick)
                {
                    proposal.ValueRW.Status = ProposalStatus.Expired;
                    continue;
                }

                // Find target faction for AI evaluation
                foreach (var (faction, statuses) in SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<DiplomaticStatusEntry>>())
                {
                    if (faction.ValueRO.FactionId != proposal.ValueRO.TargetFactionId)
                    {
                        continue;
                    }

                    // Skip player factions - they decide manually
                    if (faction.ValueRO.Type == FactionType.Player)
                    {
                        continue;
                    }

                    // Find relation with proposer
                    sbyte relationScore = 0;
                    float trust = 0;
                    for (int s = 0; s < statuses.Length; s++)
                    {
                        if (statuses[s].Status.OtherFactionId == proposal.ValueRO.ProposerFactionId)
                        {
                            relationScore = statuses[s].Status.RelationScore;
                            trust = (float)statuses[s].Status.Trust;
                            break;
                        }
                    }

                    // AI decision
                    bool accept = false;
                    switch (proposal.ValueRO.Type)
                    {
                        case DiplomaticProposalType.Treaty:
                            accept = DiplomacyMath.ShouldAcceptTreaty(
                                proposal.ValueRO.TreatyType,
                                faction.ValueRO,
                                relationScore,
                                trust,
                                proposal.ValueRO.OfferedValue,
                                proposal.ValueRO.RequestedValue
                            );
                            break;

                        case DiplomaticProposalType.RequestPeace:
                            // More likely to accept peace if losing
                            accept = relationScore > -50 || trust > 0;
                            break;

                        case DiplomaticProposalType.DemandTribute:
                            // Only accept if much weaker and fearful
                            accept = false; // Would check power balance
                            break;

                        case DiplomaticProposalType.RequestAlliance:
                            accept = relationScore > 50 && trust > 0.3f;
                            break;
                    }

                    proposal.ValueRW.Status = accept ? ProposalStatus.Accepted : ProposalStatus.Rejected;
                    break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Updates ambassador missions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XAmbassadorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XAmbassador>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var ambassador in SystemAPI.Query<RefRW<Space4XAmbassador>>())
            {
                if (ambassador.ValueRO.CurrentMission == AmbassadorMission.None)
                {
                    continue;
                }

                // Progress mission
                float progressRate = 0.001f * (1f + (float)ambassador.ValueRO.DiplomacySkill);

                switch (ambassador.ValueRO.CurrentMission)
                {
                    case AmbassadorMission.ImproveRelations:
                        progressRate *= 1f + (float)ambassador.ValueRO.Charm;
                        break;

                    case AmbassadorMission.NegotiateTreaty:
                        progressRate *= 0.5f * (1f + (float)ambassador.ValueRO.DiplomacySkill);
                        break;

                    case AmbassadorMission.GatherIntel:
                        progressRate *= 0.3f * (1f + (float)ambassador.ValueRO.EspionageSkill);
                        break;

                    case AmbassadorMission.Sabotage:
                        progressRate *= 0.2f * (1f + (float)ambassador.ValueRO.EspionageSkill);
                        break;
                }

                ambassador.ValueRW.MissionProgress = (half)((float)ambassador.ValueRO.MissionProgress + progressRate);

                // Mission completion
                if ((float)ambassador.ValueRO.MissionProgress >= 1f)
                {
                    // Apply mission results
                    switch (ambassador.ValueRO.CurrentMission)
                    {
                        case AmbassadorMission.ImproveRelations:
                            ambassador.ValueRW.RelationsGenerated += 10f * (1f + (float)ambassador.ValueRO.Charm);
                            break;
                    }

                    ambassador.ValueRW.CurrentMission = AmbassadorMission.None;
                    ambassador.ValueRW.MissionProgress = (half)0f;
                }
            }
        }
    }

    /// <summary>
    /// Applies ambassador relation improvements.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XAmbassadorSystem))]
    public partial struct Space4XAmbassadorRelationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XAmbassador>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Process accumulated relation improvements
            foreach (var ambassador in SystemAPI.Query<RefRW<Space4XAmbassador>>())
            {
                if (ambassador.ValueRO.RelationsGenerated < 1f)
                {
                    continue;
                }

                // Find home faction and apply modifier
                foreach (var (faction, modifiers) in SystemAPI.Query<RefRO<Space4XFaction>, DynamicBuffer<RelationModifier>>())
                {
                    var modifierBuffer = modifiers;

                    if (faction.ValueRO.FactionId != ambassador.ValueRO.HomeFactionId)
                    {
                        continue;
                    }

                    sbyte improvement = (sbyte)math.min(10, (int)ambassador.ValueRO.RelationsGenerated);

                    if (modifierBuffer.Length < modifierBuffer.Capacity)
                    {
                        modifierBuffer.Add(new RelationModifier
                        {
                            Type = RelationModifierType.LongPeace, // Diplomatic work
                            ScoreChange = improvement,
                            DecayRate = (half)0.1f,
                            SourceFactionId = ambassador.ValueRO.PostedFactionId,
                            AppliedTick = currentTick
                        });
                    }

                    ambassador.ValueRW.RelationsGenerated = 0f;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for diplomacy system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XDiplomacyTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DiplomaticStatusEntry>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int atWar = 0;
            int allied = 0;
            int neutral = 0;
            int activeTreaties = 0;
            int pendingProposals = 0;
            int ambassadors = 0;

            foreach (var statuses in SystemAPI.Query<DynamicBuffer<DiplomaticStatusEntry>>())
            {
                for (int s = 0; s < statuses.Length; s++)
                {
                    switch (statuses[s].Status.Stance)
                    {
                        case DiplomaticStance.War:
                            atWar++;
                            break;
                        case DiplomaticStance.Allied:
                            allied++;
                            break;
                        case DiplomaticStance.Neutral:
                            neutral++;
                            break;
                    }
                }
            }

            foreach (var treaty in SystemAPI.Query<RefRO<Space4XTreaty>>())
            {
                if (treaty.ValueRO.IsActive != 0)
                {
                    activeTreaties++;
                }
            }

            foreach (var proposal in SystemAPI.Query<RefRO<DiplomaticProposal>>())
            {
                if (proposal.ValueRO.Status == ProposalStatus.Pending)
                {
                    pendingProposals++;
                }
            }

            foreach (var _ in SystemAPI.Query<RefRO<Space4XAmbassador>>())
            {
                ambassadors++;
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Diplomacy] AtWar: {atWar}, Allied: {allied}, Neutral: {neutral}, Treaties: {activeTreaties}, Proposals: {pendingProposals}, Ambassadors: {ambassadors}");
        }
    }
}

