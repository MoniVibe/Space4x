using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Comms
{
    /// <summary>
    /// Drains CommsOutboxEntry buffers into:
    /// - the shared comms message stream (semantic payload)
    /// - the existing signal field (Sound/EM strength) so sensing stays medium-first + scalable.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PureDOTS.Systems.Perception.PerceptionSignalFieldUpdateSystem))]
    public partial struct CommsOutboxDepositSystem : ISystem
    {
        private ComponentLookup<MediumContext> _mediumLookup;
        private ComponentLookup<FocusBudget> _focusLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<CommsMessageStreamTag>();
            _mediumLookup = state.GetComponentLookup<MediumContext>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
            var hasSignalField = SystemAPI.HasBuffer<SignalFieldCell>(gridEntity);
            var signalConfig = SignalFieldConfig.Default;
            var emissionScale = 0f;
            var maxStrength = 0f;
            if (hasSignalField)
            {
                signalConfig = SystemAPI.HasComponent<SignalFieldConfig>(gridEntity)
                    ? SystemAPI.GetComponentRO<SignalFieldConfig>(gridEntity).ValueRO
                    : SignalFieldConfig.Default;

                emissionScale = math.max(0f, signalConfig.EmissionScale);
                maxStrength = math.max(0f, signalConfig.MaxStrength);
            }

            var gridConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var cells = hasSignalField ? SystemAPI.GetBuffer<SignalFieldCell>(gridEntity) : default;

            var commEntity = SystemAPI.GetSingletonEntity<CommsMessageStreamTag>();
            var comms = state.EntityManager.GetBuffer<CommsMessage>(commEntity);

            var settings = SystemAPI.GetSingleton<CommsSettings>();
            var maxAge = settings.MaxMessageAgeTicks == 0 ? 1u : settings.MaxMessageAgeTicks;
            var maxStream = settings.MaxMessagesInStream <= 0 ? 0 : settings.MaxMessagesInStream;

            var budget = SystemAPI.HasSingleton<UniversalPerformanceBudget>()
                ? SystemAPI.GetSingleton<UniversalPerformanceBudget>()
                : UniversalPerformanceBudget.CreateDefaults();

            var countersRW = SystemAPI.HasSingleton<UniversalPerformanceCounters>()
                ? SystemAPI.GetSingletonRW<UniversalPerformanceCounters>()
                : default;

            var remaining = math.max(0, budget.MaxCommsMessagesPerTick);
            _mediumLookup.Update(ref state);
            _focusLookup.Update(ref state);

            var updatedSignalField = false;

            foreach (var (outboxBuffer, transform, entity) in SystemAPI.Query<DynamicBuffer<CommsOutboxEntry>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var outbox = outboxBuffer;
                if (remaining <= 0)
                {
                    break;
                }

                if (outbox.Length == 0)
                {
                    continue;
                }

                var medium = _mediumLookup.HasComponent(entity) ? _mediumLookup[entity].Type : MediumType.Gas;
                var origin = transform.ValueRO.Position;
                SpatialHash.Quantize(origin, gridConfig, out var cellCoords);
                var cellId = SpatialHash.Flatten(in cellCoords, in gridConfig);
                if ((uint)cellId >= (uint)gridConfig.CellCount)
                {
                    outbox.Clear();
                    continue;
                }

                // Iterate backwards so we can RemoveAtSwapBack safely.
                for (int i = outbox.Length - 1; i >= 0; i--)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var entry = outbox[i];
                    if (entry.NextEmitTick != 0 && time.Tick < entry.NextEmitTick)
                    {
                        continue;
                    }

                    if (entry.MaxAttempts > 0 && entry.Attempts >= entry.MaxAttempts)
                    {
                        outbox.RemoveAtSwapBack(i);
                        continue;
                    }

                    if (entry.FirstEmitTick != 0 && entry.TtlTicks > 0 && time.Tick > entry.FirstEmitTick + entry.TtlTicks)
                    {
                        outbox.RemoveAtSwapBack(i);
                        continue;
                    }

                    if (entry.Strength01 <= 0f)
                    {
                        outbox.RemoveAtSwapBack(i);
                        continue;
                    }

                    // Focus cost to emit (optional).
                    if (entry.FocusCost > 0f && _focusLookup.HasComponent(entity))
                    {
                        var focus = _focusLookup.GetRefRW(entity);
                        if (!focus.ValueRO.CanReserve(entry.FocusCost))
                        {
                            // Too focused/exhausted to speak right now: delay.
                            entry.NextEmitTick = time.Tick + math.max(1u, entry.RepeatCadenceTicks);
                            outbox[i] = entry;
                            continue;
                        }

                        focus.ValueRW.Current = math.max(0f, focus.ValueRO.Current - entry.FocusCost);
                    }

                    var preferred = entry.TransportMaskPreferred == PerceptionChannel.None
                        ? (PerceptionChannel.Hearing | PerceptionChannel.EM)
                        : entry.TransportMaskPreferred;

                    var supported = MediumUtilities.FilterChannels(medium, preferred);
                    var transport = ChooseTransport(supported, preferred);
                    if (transport == PerceptionChannel.None)
                    {
                        if (countersRW.IsValid)
                        {
                            countersRW.ValueRW.CommsMessagesDroppedThisTick++;
                            countersRW.ValueRW.TotalOperationsDroppedThisTick++;
                        }
                        outbox.RemoveAtSwapBack(i);
                        continue;
                    }

                    // Deposit into medium field when transport is a medium-carried channel.
                    if (hasSignalField && (transport == PerceptionChannel.Hearing || transport == PerceptionChannel.EM))
                    {
                        var cell = cells[cellId];
                        ApplyDecay(ref cell, time.Tick, signalConfig, time.FixedDeltaTime);

                        var strength = math.saturate(entry.Strength01) * emissionScale;
                        if (transport == PerceptionChannel.Hearing)
                        {
                            cell.Sound = math.min(maxStrength, cell.Sound + strength);
                        }
                        else
                        {
                            cell.EM = math.min(maxStrength, cell.EM + strength);
                        }

                        cell.LastUpdatedTick = time.Tick;
                        cells[cellId] = cell;
                        updatedSignalField = true;
                    }

                    if (entry.FirstEmitTick == 0)
                    {
                        entry.FirstEmitTick = time.Tick;
                    }

                    if (entry.Token == 0u)
                    {
                        entry.Token = CommsDeterminism.ComputeToken(entry.FirstEmitTick, entity, entry.PayloadId, entry.InterruptType);
                    }

                    var token = entry.Token;
                    var ttl = entry.TtlTicks == 0 ? maxAge : entry.TtlTicks;

                    if (maxStream > 0 && comms.Length >= maxStream)
                    {
                        // Drop oldest (front) to keep bounded (cheap, deterministic).
                        comms.RemoveAt(0);
                        if (countersRW.IsValid)
                        {
                            countersRW.ValueRW.CommsMessagesDroppedThisTick++;
                        }
                    }

                    comms.Add(new CommsMessage
                    {
                        Token = token,
                        EmittedTick = time.Tick,
                        ExpirationTick = time.Tick + ttl,
                        CellId = cellId,
                        Sender = entity,
                        Origin = origin,
                        InterruptType = entry.InterruptType,
                        Priority = entry.Priority,
                        PayloadId = entry.PayloadId,
                        TransportUsed = transport,
                        Strength01 = math.saturate(entry.Strength01),
                        Clarity01 = math.saturate(entry.Clarity01),
                        DeceptionStrength01 = math.saturate(entry.DeceptionStrength01),
                        Secrecy01 = math.saturate(entry.Secrecy01),
                        IntendedReceiver = entry.IntendedReceiver,
                        Flags = entry.Flags
                    });

                    entry.Attempts++;
                    if ((entry.Flags & CommsMessageFlags.RequestsAck) != 0)
                    {
                        var cadence = entry.RepeatCadenceTicks == 0 ? 10u : entry.RepeatCadenceTicks;
                        entry.NextEmitTick = time.Tick + cadence;
                        outbox[i] = entry;
                    }
                    else
                    {
                        outbox.RemoveAtSwapBack(i);
                    }

                    remaining--;
                    if (countersRW.IsValid)
                    {
                        countersRW.ValueRW.CommsMessagesEmittedThisTick++;
                        countersRW.ValueRW.TotalWarmOperationsThisTick++;
                    }
                }
            }

            if (updatedSignalField && SystemAPI.HasComponent<SignalFieldState>(gridEntity))
            {
                var stateRW = SystemAPI.GetComponentRW<SignalFieldState>(gridEntity);
                stateRW.ValueRW.LastUpdateTick = time.Tick;
                stateRW.ValueRW.Version++;
            }
        }

        private static PerceptionChannel ChooseTransport(PerceptionChannel supported, PerceptionChannel preferred)
        {
            // Prefer EM for space/tech by default; otherwise hearing.
            if ((preferred & PerceptionChannel.Vision) != 0 && (supported & PerceptionChannel.Vision) != 0)
            {
                return PerceptionChannel.Vision;
            }
            if ((preferred & PerceptionChannel.Paranormal) != 0 && (supported & PerceptionChannel.Paranormal) != 0)
            {
                return PerceptionChannel.Paranormal;
            }
            if ((preferred & PerceptionChannel.EM) != 0 && (supported & PerceptionChannel.EM) != 0)
            {
                return PerceptionChannel.EM;
            }
            if ((preferred & PerceptionChannel.Hearing) != 0 && (supported & PerceptionChannel.Hearing) != 0)
            {
                return PerceptionChannel.Hearing;
            }
            if ((supported & PerceptionChannel.Vision) != 0)
            {
                return PerceptionChannel.Vision;
            }
            if ((supported & PerceptionChannel.Paranormal) != 0)
            {
                return PerceptionChannel.Paranormal;
            }
            if ((supported & PerceptionChannel.EM) != 0)
            {
                return PerceptionChannel.EM;
            }
            if ((supported & PerceptionChannel.Hearing) != 0)
            {
                return PerceptionChannel.Hearing;
            }
            return PerceptionChannel.None;
        }

        [BurstCompile]
        private static void ApplyDecay(ref SignalFieldCell cell, uint currentTick, in SignalFieldConfig config, float fixedDt)
        {
            if (cell.LastUpdatedTick == currentTick)
            {
                return;
            }

            var smellDecayPerTick = math.clamp(1f - config.SmellDecayPerSecond * fixedDt, 0f, 1f);
            var soundDecayPerTick = math.clamp(1f - config.SoundDecayPerSecond * fixedDt, 0f, 1f);
            var emDecayPerTick = math.clamp(1f - config.EMDecayPerSecond * fixedDt, 0f, 1f);

            var ticks = currentTick - cell.LastUpdatedTick;
            if (ticks == 0)
            {
                return;
            }

            var ticksF = (float)ticks;
            cell.Smell *= math.pow(smellDecayPerTick, ticksF);
            cell.Sound *= math.pow(soundDecayPerTick, ticksF);
            cell.EM *= math.pow(emDecayPerTick, ticksF);
            cell.LastUpdatedTick = currentTick;
        }
    }
}


