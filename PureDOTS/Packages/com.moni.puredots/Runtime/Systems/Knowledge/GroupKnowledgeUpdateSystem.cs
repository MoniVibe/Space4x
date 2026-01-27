using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Villagers;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Updates bounded group knowledge caches from perception and comms receipts.
    /// Village-first MVP for group cache communications.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(GroupKnowledgeEmitterBootstrapSystem))]
    public partial struct GroupKnowledgeUpdateSystem : ISystem
    {
        private const uint DefaultEmitIntervalTicks = 30;
        private const byte DefaultThreatThreshold = 100;

        private ComponentLookup<Detectable> _detectableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<GroupKnowledgeConfig> _configLookup;
        private ComponentLookup<GroupKnowledgeCache> _cacheLookup;
        private BufferLookup<GroupKnowledgeEntry> _entryLookup;
        private BufferLookup<CommReceipt> _receiptLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _detectableLookup = state.GetComponentLookup<Detectable>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _configLookup = state.GetComponentLookup<GroupKnowledgeConfig>(true);
            _cacheLookup = state.GetComponentLookup<GroupKnowledgeCache>();
            _entryLookup = state.GetBufferLookup<GroupKnowledgeEntry>();
            _receiptLookup = state.GetBufferLookup<CommReceipt>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;

            _detectableLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _configLookup.Update(ref state);
            _cacheLookup.Update(ref state);
            _entryLookup.Update(ref state);
            _receiptLookup.Update(ref state);

            // Prune stale entries on group caches.
            foreach (var (cache, config, entries, entity) in SystemAPI.Query<RefRW<GroupKnowledgeCache>, RefRO<GroupKnowledgeConfig>, DynamicBuffer<GroupKnowledgeEntry>>()
                .WithEntityAccess())
            {
                if (config.ValueRO.Enabled == 0)
                {
                    continue;
                }

                if (tick == cache.ValueRO.LastPruneTick)
                {
                    continue;
                }

                PruneStale(entries, tick, config.ValueRO);
                cache.ValueRW.LastPruneTick = tick;
            }

            foreach (var (villageRef, perceptionState, perceivedBuffer, emitterState, transform, entity) in
                SystemAPI.Query<RefRO<VillagerVillageRef>, RefRO<PerceptionState>, DynamicBuffer<PerceivedEntity>, RefRW<GroupKnowledgeEmitterState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                var villageEntity = villageRef.ValueRO.VillageEntity;
                if (villageEntity == Entity.Null || !_entryLookup.HasBuffer(villageEntity) || !_configLookup.HasComponent(villageEntity))
                {
                    continue;
                }

                var config = _configLookup[villageEntity];
                if (config.Enabled == 0)
                {
                    continue;
                }

                var entries = _entryLookup[villageEntity];
                var emitted = false;

                if (tick - emitterState.ValueRO.LastEmitTick >= DefaultEmitIntervalTicks)
                {
                    emitted = TryEmitPerceptionEntry(entries, tick, config, perceivedBuffer, transform.ValueRO.Position);
                    if (emitted)
                    {
                        emitterState.ValueRW.LastEmitTick = tick;
                    }
                }

                if (_receiptLookup.HasBuffer(entity))
                {
                    var receipts = _receiptLookup[entity];
                    if (receipts.Length > 0)
                    {
                        emitted |= TryEmitCommEntries(entries, tick, config, receipts);
                        receipts.Clear();
                        if (emitted)
                        {
                            emitterState.ValueRW.LastEmitTick = tick;
                        }
                    }
                }

                if (emitted && _cacheLookup.HasComponent(villageEntity))
                {
                    var cache = _cacheLookup[villageEntity];
                    cache.LastUpdateTick = tick;
                    _cacheLookup[villageEntity] = cache;
                }
            }
        }

        private bool TryEmitPerceptionEntry(
            DynamicBuffer<GroupKnowledgeEntry> entries,
            uint tick,
            in GroupKnowledgeConfig config,
            DynamicBuffer<PerceivedEntity> perceivedBuffer,
            float3 observerPosition)
        {
            if (perceivedBuffer.Length == 0)
            {
                return false;
            }

            var bestPriority = -1;
            var bestConfidence = 0f;
            var best = new GroupKnowledgeEntry
            {
                Kind = GroupKnowledgeClaimKind.None
            };

            for (int i = 0; i < perceivedBuffer.Length; i++)
            {
                var perceived = perceivedBuffer[i];
                var target = perceived.TargetEntity;
                if (target == Entity.Null || !_detectableLookup.HasComponent(target))
                {
                    continue;
                }

                var detectable = _detectableLookup[target];
                var kind = ResolveClaimKind(detectable.Category, perceived.ThreatLevel);
                if (kind == GroupKnowledgeClaimKind.None)
                {
                    continue;
                }

                var priority = ResolvePriority(kind);
                var confidence = math.max(perceived.Confidence, perceived.ThreatLevel / 255f);

                if (priority < bestPriority || (priority == bestPriority && confidence <= bestConfidence))
                {
                    continue;
                }

                var position = observerPosition + perceived.Direction * perceived.Distance;
                bestPriority = priority;
                bestConfidence = confidence;
                best = new GroupKnowledgeEntry
                {
                    Kind = kind,
                    Subject = target,
                    Source = target,
                    Position = position,
                    Confidence = math.saturate(confidence),
                    LastSeenTick = tick,
                    PayloadId = default,
                    Flags = GroupKnowledgeFlags.FromPerception
                };
            }

            if (best.Kind == GroupKnowledgeClaimKind.None)
            {
                return false;
            }

            UpsertEntry(entries, tick, config, best);
            return true;
        }

        private bool TryEmitCommEntries(
            DynamicBuffer<GroupKnowledgeEntry> entries,
            uint tick,
            in GroupKnowledgeConfig config,
            DynamicBuffer<CommReceipt> receipts)
        {
            var emitted = false;
            for (int i = 0; i < receipts.Length; i++)
            {
                var receipt = receipts[i];
                var kind = ResolveClaimKind(receipt.Intent);
                if (kind == GroupKnowledgeClaimKind.None)
                {
                    continue;
                }

                var flags = GroupKnowledgeFlags.FromComms;
                if (receipt.Integrity < config.MinConfidence)
                {
                    flags |= GroupKnowledgeFlags.Unreliable;
                }

                var entry = new GroupKnowledgeEntry
                {
                    Kind = kind,
                    Subject = receipt.Sender,
                    Source = receipt.Sender,
                    Position = default,
                    Confidence = math.saturate(receipt.Integrity),
                    LastSeenTick = tick,
                    PayloadId = receipt.PayloadId,
                    Flags = flags
                };

                UpsertEntry(entries, tick, config, entry);
                emitted = true;
            }

            return emitted;
        }

        private static void PruneStale(DynamicBuffer<GroupKnowledgeEntry> entries, uint tick, in GroupKnowledgeConfig config)
        {
            if (config.StaleAfterTicks == 0 || entries.Length == 0)
            {
                return;
            }

            for (int i = entries.Length - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (tick - entry.LastSeenTick > config.StaleAfterTicks)
                {
                    entries.RemoveAt(i);
                }
            }
        }

        private static void UpsertEntry(
            DynamicBuffer<GroupKnowledgeEntry> entries,
            uint tick,
            in GroupKnowledgeConfig config,
            GroupKnowledgeEntry candidate)
        {
            if (candidate.Confidence < config.MinConfidence)
            {
                candidate.Flags |= GroupKnowledgeFlags.Unreliable;
            }

            var matchIndex = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Kind != candidate.Kind)
                {
                    continue;
                }

                if (candidate.Subject != Entity.Null && entry.Subject == candidate.Subject)
                {
                    matchIndex = i;
                    break;
                }

                if (candidate.PayloadId.Length > 0 && entry.PayloadId.Equals(candidate.PayloadId))
                {
                    matchIndex = i;
                    break;
                }
            }

            candidate.LastSeenTick = tick;

            if (matchIndex >= 0)
            {
                var existing = entries[matchIndex];
                existing.Confidence = math.max(existing.Confidence, candidate.Confidence);
                existing.LastSeenTick = tick;
                existing.Subject = candidate.Subject != Entity.Null ? candidate.Subject : existing.Subject;
                existing.Source = candidate.Source != Entity.Null ? candidate.Source : existing.Source;
                existing.Position = math.lengthsq(candidate.Position) > 0f ? candidate.Position : existing.Position;
                existing.PayloadId = candidate.PayloadId.Length > 0 ? candidate.PayloadId : existing.PayloadId;
                existing.Flags |= candidate.Flags;
                entries[matchIndex] = existing;
                return;
            }

            if (entries.Length < math.max(1, config.MaxEntries))
            {
                entries.Add(candidate);
                return;
            }

            var evictIndex = SelectEvictionIndex(entries, tick, config.StaleAfterTicks);
            entries[evictIndex] = candidate;
        }

        private static int SelectEvictionIndex(DynamicBuffer<GroupKnowledgeEntry> entries, uint tick, uint staleAfterTicks)
        {
            var worstIndex = 0;
            var worstScore = float.MaxValue;
            var decayTicks = staleAfterTicks > 0 ? staleAfterTicks : 900u;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var age = tick >= entry.LastSeenTick ? tick - entry.LastSeenTick : 0u;
                var score = entry.Confidence - math.min(1f, age / (float)decayTicks);
                if (score < worstScore)
                {
                    worstScore = score;
                    worstIndex = i;
                }
            }

            return worstIndex;
        }

        private static GroupKnowledgeClaimKind ResolveClaimKind(DetectableCategory category, byte threatLevel)
        {
            if (threatLevel >= DefaultThreatThreshold || category == DetectableCategory.Enemy)
            {
                return GroupKnowledgeClaimKind.ThreatSeen;
            }

            if (category == DetectableCategory.Hazard)
            {
                return GroupKnowledgeClaimKind.HazardSeen;
            }

            if (category == DetectableCategory.Objective)
            {
                return GroupKnowledgeClaimKind.ObjectiveSeen;
            }

            if (category == DetectableCategory.Resource)
            {
                return GroupKnowledgeClaimKind.ResourceSeen;
            }

            return GroupKnowledgeClaimKind.None;
        }

        private static int ResolvePriority(GroupKnowledgeClaimKind kind)
        {
            return kind switch
            {
                GroupKnowledgeClaimKind.ThreatSeen => 3,
                GroupKnowledgeClaimKind.HazardSeen => 2,
                GroupKnowledgeClaimKind.ObjectiveSeen => 1,
                GroupKnowledgeClaimKind.ResourceSeen => 0,
                _ => -1
            };
        }

        private static GroupKnowledgeClaimKind ResolveClaimKind(CommunicationIntent intent)
        {
            return intent switch
            {
                CommunicationIntent.Warning => GroupKnowledgeClaimKind.ThreatSeen,
                CommunicationIntent.Threat => GroupKnowledgeClaimKind.ThreatSeen,
                CommunicationIntent.Rumor => GroupKnowledgeClaimKind.Rumor,
                CommunicationIntent.ShareKnowledge => GroupKnowledgeClaimKind.Rumor,
                CommunicationIntent.AskForKnowledge => GroupKnowledgeClaimKind.Rumor,
                CommunicationIntent.RequestHelp => GroupKnowledgeClaimKind.ThreatSeen,
                _ => GroupKnowledgeClaimKind.None
            };
        }
    }
}
