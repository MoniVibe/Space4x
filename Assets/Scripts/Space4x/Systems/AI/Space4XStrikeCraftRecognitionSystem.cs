using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Applies lightweight recognition/mercy decisions for strike craft pilots.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4X.Registry.Space4XCombatInitiationSystem))]
    public partial struct Space4XStrikeCraftRecognitionSystem : ISystem
    {
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<StrikeCraftExperience> _experienceLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<Space4XNormalizedIndividualStats> _statsLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<CultureId> _cultureLookup;
        private ComponentLookup<RaceId> _raceLookup;
        private ComponentLookup<StrikeCraftFireDiscipline> _fireDisciplineLookup;
        private BufferLookup<PersonalRelationEntry> _personalRelationLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<DiplomaticStatusEntry> _diplomaticStatusLookup;
        private BufferLookup<FactionRelationEntry> _factionRelationLookup;
        private ComponentLookup<ModuleTargetPolicyOverride> _policyOverrideLookup;
        private ComponentLookup<DisciplinaryRecord> _disciplinaryLookup;
        private EntityStorageInfoLookup _entityLookup;
        private FixedString64Bytes _sourceId;
        private FixedString64Bytes _eventMercyStarted;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<StrikeCraftState>();

            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _experienceLookup = state.GetComponentLookup<StrikeCraftExperience>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _statsLookup = state.GetComponentLookup<Space4XNormalizedIndividualStats>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _cultureLookup = state.GetComponentLookup<CultureId>(true);
            _raceLookup = state.GetComponentLookup<RaceId>(true);
            _fireDisciplineLookup = state.GetComponentLookup<StrikeCraftFireDiscipline>(true);
            _personalRelationLookup = state.GetBufferLookup<PersonalRelationEntry>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _diplomaticStatusLookup = state.GetBufferLookup<DiplomaticStatusEntry>(true);
            _factionRelationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
            _policyOverrideLookup = state.GetComponentLookup<ModuleTargetPolicyOverride>(true);
            _disciplinaryLookup = state.GetComponentLookup<DisciplinaryRecord>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
            _sourceId = new FixedString64Bytes("Space4X.StrikeCraft");
            _eventMercyStarted = new FixedString64Bytes("RecognitionMercy");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _experienceLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _cultureLookup.Update(ref state);
            _raceLookup.Update(ref state);
            _fireDisciplineLookup.Update(ref state);
            _personalRelationLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _diplomaticStatusLookup.Update(ref state);
            _factionRelationLookup.Update(ref state);
            _policyOverrideLookup.Update(ref state);
            _disciplinaryLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var config = StrikeCraftRecognitionConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftRecognitionConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var emitTelemetry = TryResolveTelemetryEventBuffer(ref state, out var eventBuffer);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<StrikeCraftState>>()
                .WithNone<StrikeCraftRecognitionState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, StrikeCraftRecognitionState.Default);
            }

            foreach (var (strikeState, recognition, transform, entity) in SystemAPI
                .Query<RefRO<StrikeCraftState>, RefRW<StrikeCraftRecognitionState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                var targetEntity = strikeState.ValueRO.TargetEntity;
                var previousMercyTarget = recognition.ValueRO.MercyTarget;
                var mercyActive = previousMercyTarget != Entity.Null &&
                                  recognition.ValueRO.MercyUntilTick > currentTick;

                if (previousMercyTarget != Entity.Null &&
                    (!mercyActive || previousMercyTarget != targetEntity))
                {
                    recognition.ValueRW.MercyTarget = Entity.Null;
                    recognition.ValueRW.MercyUntilTick = 0;
                    if (_policyOverrideLookup.HasComponent(entity))
                    {
                        var overridePolicy = _policyOverrideLookup[entity];
                        if (overridePolicy.TargetShip == previousMercyTarget)
                        {
                            ecb.RemoveComponent<ModuleTargetPolicyOverride>(entity);
                        }
                    }

                    if (_fireDisciplineLookup.HasComponent(entity))
                    {
                        var discipline = _fireDisciplineLookup[entity];
                        if (discipline.Target == previousMercyTarget)
                        {
                            ecb.RemoveComponent<StrikeCraftFireDiscipline>(entity);
                        }
                    }
                }

                if (targetEntity == Entity.Null || !_entityLookup.Exists(targetEntity))
                {
                    continue;
                }

                if (recognition.ValueRO.MercyTarget == targetEntity && recognition.ValueRO.MercyUntilTick > currentTick)
                {
                    EnsurePolicyOverride(ref ecb, entity, targetEntity, recognition.ValueRO.MercyUntilTick);
                    EnsureFireDiscipline(ref ecb, entity, targetEntity, recognition.ValueRO.MercyUntilTick,
                        config.MercySuppressMinChance, config.MercySuppressMaxChance, ResolveGoodness(ResolveProfileEntity(entity)));
                    continue;
                }

                if (currentTick < recognition.ValueRO.NextCheckTick)
                {
                    continue;
                }

                recognition.ValueRW.NextCheckTick = currentTick + math.max(1u, config.CheckCooldownTicks);

                if (!_transformLookup.HasComponent(targetEntity))
                {
                    continue;
                }

                if (config.RecognitionRange > 0f)
                {
                    var targetPosition = _transformLookup[targetEntity].Position;
                    var distance = math.distance(transform.ValueRO.Position, targetPosition);
                    if (distance > config.RecognitionRange)
                    {
                        continue;
                    }
                }

                if (!IsHostileEnough(entity, targetEntity, config))
                {
                    continue;
                }

                var selfProfile = ResolveProfileEntity(entity);
                var targetProfile = ResolveProfileEntity(targetEntity);

                var cultureMatch = TryResolveCultureMatch(selfProfile, targetProfile);
                var raceMatch = TryResolveRaceMatch(selfProfile, targetProfile);
                var hasPersonalRelation = TryResolvePersonalRelation(selfProfile, targetProfile, out var relationScore, out var relationKind);
                if (!cultureMatch && !raceMatch && !hasPersonalRelation)
                {
                    continue;
                }

                float baseChance = 0f;
                if (cultureMatch)
                {
                    baseChance = math.max(baseChance, config.CultureRecognitionChance);
                }
                if (raceMatch)
                {
                    baseChance = math.max(baseChance, config.RaceRecognitionChance);
                }
                if (hasPersonalRelation && relationScore >= config.PersonalRelationThreshold)
                {
                    baseChance = math.max(baseChance, config.PersonalRecognitionChance);
                }

                if (baseChance <= 0f)
                {
                    continue;
                }

                var mercyProfile = ResolveProfileEntity(entity);
                var goodness = ResolveGoodness(mercyProfile);
                if (goodness < config.MercyGoodThreshold)
                {
                    continue;
                }

                var chance = math.saturate(baseChance + ResolveRecognitionSkill(mercyProfile, entity));
                var roll = DeterministicRoll(entity, targetEntity, currentTick, 17);
                if (roll > chance)
                {
                    continue;
                }

                var mercyUntil = currentTick + math.max(1u, config.MercyDurationTicks);
                recognition.ValueRW.MercyTarget = targetEntity;
                recognition.ValueRW.MercyUntilTick = mercyUntil;
                EnsurePolicyOverride(ref ecb, entity, targetEntity, mercyUntil);
                EnsureFireDiscipline(ref ecb, entity, targetEntity, mercyUntil,
                    config.MercySuppressMinChance, config.MercySuppressMaxChance, goodness);

                var disciplineApplied = false;
                var penaltyApplied = 0f;
                var disciplineFaction = ResolveFactionEntity(entity);
                var mercyAllowed = IsMercyAllowed(disciplineFaction);
                if (!mercyAllowed && config.MercyLoyaltyPenalty > 0f)
                {
                    penaltyApplied = ApplyLoyaltyPenalty(mercyProfile, disciplineFaction, config.MercyLoyaltyPenalty);
                    if (penaltyApplied > 0f)
                    {
                        disciplineApplied = true;
                    }

                    SetDisciplinaryRecord(ref ecb, mercyProfile, disciplineFaction, penaltyApplied, currentTick);
                }

                if (emitTelemetry)
                {
                    eventBuffer.AddEvent(_eventMercyStarted, currentTick, _sourceId,
                        BuildMercyPayload(entity, mercyProfile, targetEntity, cultureMatch, raceMatch, hasPersonalRelation,
                            relationScore, relationKind, goodness, chance, mercyUntil, disciplineApplied, penaltyApplied));
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity ResolveProfileEntity(Entity shipEntity)
        {
            var pilot = ResolvePilot(shipEntity);
            return pilot != Entity.Null ? pilot : shipEntity;
        }

        private Entity ResolvePilot(Entity shipEntity)
        {
            if (_strikePilotLookup.HasComponent(shipEntity))
            {
                var pilot = _strikePilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(shipEntity))
            {
                var pilot = _vesselPilotLookup[shipEntity].Pilot;
                if (pilot != Entity.Null)
                {
                    return pilot;
                }
            }

            return Entity.Null;
        }

        private bool TryResolveCultureMatch(Entity self, Entity other)
        {
            if (!TryResolveCultureId(self, out var selfCulture) ||
                !TryResolveCultureId(other, out var otherCulture))
            {
                return false;
            }

            return selfCulture == otherCulture;
        }

        private bool TryResolveRaceMatch(Entity self, Entity other)
        {
            if (!TryResolveRaceId(self, out var selfRace) ||
                !TryResolveRaceId(other, out var otherRace))
            {
                return false;
            }

            return selfRace == otherRace;
        }

        private bool TryResolveCultureId(Entity entity, out ushort cultureId)
        {
            if (_cultureLookup.HasComponent(entity))
            {
                cultureId = _cultureLookup[entity].Value;
                return true;
            }

            cultureId = 0;
            return false;
        }

        private bool TryResolveRaceId(Entity entity, out ushort raceId)
        {
            if (_raceLookup.HasComponent(entity))
            {
                raceId = _raceLookup[entity].Value;
                return true;
            }

            raceId = 0;
            return false;
        }

        private float ResolveGoodness(Entity profileEntity)
        {
            if (_alignmentLookup.HasComponent(profileEntity))
            {
                var alignment = _alignmentLookup[profileEntity];
                return math.saturate(0.5f * (1f + (float)alignment.Good));
            }

            return 0.5f;
        }

        private bool TryResolvePersonalRelation(Entity self, Entity other, out sbyte score, out PersonalRelationKind kind)
        {
            score = 0;
            kind = PersonalRelationKind.None;

            if (self != Entity.Null && _personalRelationLookup.HasBuffer(self))
            {
                var relations = _personalRelationLookup[self];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    if (relation.Other == other)
                    {
                        score = relation.Score;
                        kind = relation.Kind;
                        return true;
                    }
                }
            }

            if (other != Entity.Null && _personalRelationLookup.HasBuffer(other))
            {
                var relations = _personalRelationLookup[other];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i];
                    if (relation.Other == self)
                    {
                        score = relation.Score;
                        kind = relation.Kind;
                        return true;
                    }
                }
            }

            return false;
        }

        private float ResolveRecognitionSkill(Entity profileEntity, Entity craftEntity)
        {
            float bonus = 0f;
            if (_statsLookup.HasComponent(profileEntity))
            {
                var stats = _statsLookup[profileEntity];
                var diplomacy = math.saturate(stats.Diplomacy);
                var wisdom = math.saturate(stats.Wisdom);
                bonus += (diplomacy - 0.5f) * 0.2f;
                bonus += (wisdom - 0.5f) * 0.1f;
            }

            if (_experienceLookup.HasComponent(craftEntity))
            {
                var experience = _experienceLookup[craftEntity];
                var normalizedLevel = math.saturate(experience.Level / 5f);
                bonus += normalizedLevel * 0.05f;
            }

            return bonus;
        }

        private bool IsHostileEnough(Entity self, Entity target, StrikeCraftRecognitionConfig config)
        {
            var selfFaction = ResolveFactionEntity(self);
            var targetFaction = ResolveFactionEntity(target);

            if (selfFaction == Entity.Null || targetFaction == Entity.Null)
            {
                return config.RequireHostileRelation == 0;
            }

            if (selfFaction == targetFaction)
            {
                return false;
            }

            if (!TryResolveRelationScore(selfFaction, targetFaction, out var score, out var stance))
            {
                return config.RequireHostileRelation == 0;
            }

            if (IsFriendlyRelation(score, stance))
            {
                return false;
            }

            if (IsHostileRelation(score, stance) || score <= config.HostileRelationThreshold)
            {
                return true;
            }

            return config.RequireHostileRelation == 0;
        }

        private Entity ResolveFactionEntity(Entity entity)
        {
            if (_affiliationLookup.HasBuffer(entity))
            {
                var affiliations = _affiliationLookup[entity];
                Entity fallback = Entity.Null;
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (tag.Type == AffiliationType.Faction)
                    {
                        return tag.Target;
                    }

                    if (fallback == Entity.Null && tag.Type == AffiliationType.Fleet)
                    {
                        fallback = tag.Target;
                    }
                    else if (fallback == Entity.Null)
                    {
                        fallback = tag.Target;
                    }
                }

                if (fallback != Entity.Null && _affiliationLookup.HasBuffer(fallback))
                {
                    var nested = _affiliationLookup[fallback];
                    for (int i = 0; i < nested.Length; i++)
                    {
                        var tag = nested[i];
                        if (tag.Target == Entity.Null)
                        {
                            continue;
                        }

                        if (tag.Type == AffiliationType.Faction)
                        {
                            return tag.Target;
                        }

                        if (tag.Type == AffiliationType.Fleet)
                        {
                            return tag.Target;
                        }
                    }
                }

                if (fallback != Entity.Null)
                {
                    return fallback;
                }
            }

            if (_carrierLookup.HasComponent(entity))
            {
                var carrier = _carrierLookup[entity];
                if (carrier.AffiliationEntity != Entity.Null)
                {
                    return carrier.AffiliationEntity;
                }
            }

            return Entity.Null;
        }

        private bool TryResolveRelationScore(Entity selfFaction, Entity targetFaction, out sbyte score, out DiplomaticStance stance)
        {
            score = 0;
            stance = DiplomaticStance.Neutral;

            if (selfFaction == Entity.Null || targetFaction == Entity.Null)
            {
                return false;
            }

            if (selfFaction == targetFaction)
            {
                score = 100;
                stance = DiplomaticStance.Allied;
                return true;
            }

            if (!_factionLookup.HasComponent(targetFaction))
            {
                return false;
            }

            ushort targetFactionId = _factionLookup[targetFaction].FactionId;

            if (_diplomaticStatusLookup.HasBuffer(selfFaction))
            {
                var statuses = _diplomaticStatusLookup[selfFaction];
                for (int i = 0; i < statuses.Length; i++)
                {
                    var status = statuses[i].Status;
                    if (status.OtherFactionId == targetFactionId)
                    {
                        score = status.RelationScore;
                        stance = status.Stance;
                        return true;
                    }
                }
            }

            if (_factionRelationLookup.HasBuffer(selfFaction))
            {
                var relations = _factionRelationLookup[selfFaction];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == targetFactionId)
                    {
                        score = relation.Score;
                        stance = DiplomacyMath.DetermineStance(score, DiplomaticStance.Neutral);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsFriendlyRelation(sbyte relationScore, DiplomaticStance stance)
        {
            if (stance == DiplomaticStance.Allied ||
                stance == DiplomaticStance.Friendly ||
                stance == DiplomaticStance.Cordial ||
                stance == DiplomaticStance.Vassal ||
                stance == DiplomaticStance.Overlord)
            {
                return true;
            }

            return relationScore >= 25;
        }

        private static bool IsHostileRelation(sbyte relationScore, DiplomaticStance stance)
        {
            if (stance == DiplomaticStance.War || stance == DiplomaticStance.Hostile)
            {
                return true;
            }

            return relationScore <= -25;
        }

        private bool IsMercyAllowed(Entity factionEntity)
        {
            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return true;
            }

            var outlook = _factionLookup[factionEntity].Outlook;
            if ((outlook & (FactionOutlook.Pacifist | FactionOutlook.Honorable)) != 0)
            {
                return true;
            }

            if ((outlook & (FactionOutlook.Militarist | FactionOutlook.Authoritarian | FactionOutlook.Xenophobe)) != 0)
            {
                return false;
            }

            return true;
        }

        private float ApplyLoyaltyPenalty(Entity profileEntity, Entity factionEntity, float penalty)
        {
            if (factionEntity == Entity.Null || !_affiliationLookup.HasBuffer(profileEntity))
            {
                return 0f;
            }

            var buffer = _affiliationLookup[profileEntity];
            for (int i = 0; i < buffer.Length; i++)
            {
                var tag = buffer[i];
                if (tag.Type != AffiliationType.Faction || tag.Target != factionEntity)
                {
                    continue;
                }

                var before = (float)tag.Loyalty;
                var after = math.saturate(before - penalty);
                tag.Loyalty = (half)after;
                buffer[i] = tag;
                return math.max(0f, before - after);
            }

            return 0f;
        }

        private void SetDisciplinaryRecord(ref EntityCommandBuffer ecb, Entity profileEntity, Entity factionEntity, float penalty, uint tick)
        {
            if (profileEntity == Entity.Null)
            {
                return;
            }

            var record = new DisciplinaryRecord
            {
                Faction = factionEntity,
                Kind = DisciplinaryInfractionKind.MercyInCombat,
                Severity = (half)math.saturate(penalty),
                Tick = tick
            };

            if (_disciplinaryLookup.HasComponent(profileEntity))
            {
                ecb.SetComponent(profileEntity, record);
            }
            else
            {
                ecb.AddComponent(profileEntity, record);
            }
        }

        private void EnsurePolicyOverride(ref EntityCommandBuffer ecb, Entity craftEntity, Entity targetEntity, uint expireTick)
        {
            var overridePolicy = new ModuleTargetPolicyOverride
            {
                Kind = ModuleTargetPolicyKind.DisableMobility,
                TargetShip = targetEntity,
                ExpireTick = expireTick
            };

            if (_policyOverrideLookup.HasComponent(craftEntity))
            {
                ecb.SetComponent(craftEntity, overridePolicy);
            }
            else
            {
                ecb.AddComponent(craftEntity, overridePolicy);
            }
        }

        private void EnsureFireDiscipline(ref EntityCommandBuffer ecb, Entity craftEntity, Entity targetEntity,
            uint expireTick, float minChance, float maxChance, float goodness)
        {
            var normalized = math.saturate(goodness);
            var suppressChance = math.lerp(minChance, maxChance, normalized);
            var discipline = new StrikeCraftFireDiscipline
            {
                SuppressFire = 1,
                SuppressChance = math.clamp(suppressChance, 0f, 1f),
                UntilTick = expireTick,
                Target = targetEntity
            };

            if (_fireDisciplineLookup.HasComponent(craftEntity))
            {
                ecb.SetComponent(craftEntity, discipline);
            }
            else
            {
                ecb.AddComponent(craftEntity, discipline);
            }
        }

        private static FixedString128Bytes BuildMercyPayload(Entity craft, Entity pilot, Entity target,
            bool cultureMatch, bool raceMatch, bool personalMatch, sbyte relationScore, PersonalRelationKind relationKind,
            float goodness, float chance, uint untilTick, bool disciplineApplied, float penalty)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("craft", craft);
            writer.AddEntity("pilot", pilot);
            writer.AddEntity("target", target);
            writer.AddBool("culture", cultureMatch);
            writer.AddBool("race", raceMatch);
            writer.AddBool("personal", personalMatch);
            writer.AddInt("relationScore", relationScore);
            writer.AddInt("relationKind", (int)relationKind);
            writer.AddFloat("good", goodness);
            writer.AddFloat("chance", chance);
            writer.AddUInt("untilTick", untilTick);
            writer.AddBool("discipline", disciplineApplied);
            writer.AddFloat("penalty", penalty);
            return writer.Build();
        }

        private bool TryResolveTelemetryEventBuffer(ref SystemState state, out DynamicBuffer<TelemetryEvent> buffer)
        {
            buffer = default;
            if (SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config))
            {
                if (config.Enabled == 0 || (config.Flags & TelemetryExportFlags.IncludeTelemetryEvents) == 0)
                {
                    return false;
                }
            }

            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Stream == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(telemetryRef.Stream))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryEvent>(telemetryRef.Stream);
            return true;
        }

        private static float DeterministicRoll(Entity actor, Entity target, uint tick, uint salt)
        {
            var hash = math.hash(new uint4((uint)actor.Index, (uint)target.Index, tick, salt));
            return (hash & 0xFFFF) / 65535f;
        }
    }
}
