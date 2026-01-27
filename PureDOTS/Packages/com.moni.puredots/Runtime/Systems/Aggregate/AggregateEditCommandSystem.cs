using System;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using SystemEnv = System.Environment;
using AggregateEntity = PureDOTS.Runtime.Aggregate.AggregateEntity;

namespace PureDOTS.Systems.Aggregate
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct AggregateEditBootstrapSystem : ISystem
    {
        private const string EnableEnv = "PUREDOTS_AGGREGATE_EDIT";

        public void OnUpdate(ref SystemState state)
        {
            if (!ShouldEnable(ref state))
            {
                return;
            }

            var entityManager = state.EntityManager;
            var streamEntity = AggregateEditCommandStreamUtility.EnsureStream(entityManager);

            if (!entityManager.HasComponent<AggregateEditAuthority>(streamEntity))
            {
                entityManager.AddComponentData(streamEntity, new AggregateEditAuthority
                {
                    AllowEdits = 1,
                    IsSandboxMode = 1,
                    MarkSavesAsEdited = 1
                });
            }

            if (!entityManager.HasBuffer<AggregateEditAuditEntry>(streamEntity))
            {
                entityManager.AddBuffer<AggregateEditAuditEntry>(streamEntity);
            }
        }

        private static bool ShouldEnable(ref SystemState state)
        {
            var enabled = SystemEnv.GetEnvironmentVariable(EnableEnv);
            if (string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            using var authorityQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AggregateEditAuthority>());
            return !authorityQuery.IsEmptyIgnoreFilter;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AggregateEditCommandApplySystem : ISystem
    {
        private static readonly FixedString64Bytes EventType = new FixedString64Bytes("aggregate.edit");
        private static readonly FixedString64Bytes EventSource = new FixedString64Bytes("aggregate");
        private const float StatMin = 0f;
        private const float StatMax = 100f;
        private const float BirthRateMin = 0f;
        private const float BirthRateMax = 1f;
        private const uint DefaultDriftDurationTicks = 600;

        private EntityQuery _aggregateIdentityQuery;
        private EntityQuery _aggregateEntityQuery;
        private EntityQuery _orgIdQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<AggregateEditCommandStreamSingleton>();
            _aggregateIdentityQuery = state.GetEntityQuery(ComponentType.ReadOnly<AggregateIdentity>());
            _aggregateEntityQuery = state.GetEntityQuery(ComponentType.ReadOnly<AggregateEntity>());
            _orgIdQuery = state.GetEntityQuery(ComponentType.ReadOnly<OrgId>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var streamSingleton = SystemAPI.GetSingleton<AggregateEditCommandStreamSingleton>();
            var streamEntity = streamSingleton.Stream;
            if (streamEntity == Entity.Null || !state.EntityManager.Exists(streamEntity))
            {
                return;
            }

            if (!state.EntityManager.HasComponent<AggregateEditAuthority>(streamEntity))
            {
                return;
            }

            var authority = state.EntityManager.GetComponentData<AggregateEditAuthority>(streamEntity);
            if (authority.AllowEdits == 0)
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<AggregateEditCommand>(streamEntity))
            {
                return;
            }

            var commandBuffer = state.EntityManager.GetBuffer<AggregateEditCommand>(streamEntity);
            if (commandBuffer.Length == 0)
            {
                return;
            }

            DynamicBuffer<AggregateEditAuditEntry> auditBuffer = default;
            if (state.EntityManager.HasBuffer<AggregateEditAuditEntry>(streamEntity))
            {
                auditBuffer = state.EntityManager.GetBuffer<AggregateEditAuditEntry>(streamEntity);
            }

            var telemetryBuffer = GetTelemetryBuffer(ref state);

            for (int i = commandBuffer.Length - 1; i >= 0; i--)
            {
                var command = commandBuffer[i];
                var applyTick = command.ApplyTick == 0u ? timeState.Tick : command.ApplyTick;
                if (applyTick > timeState.Tick)
                {
                    continue;
                }

                var appliedCount = ApplyCommand(ref state, command, timeState.Tick);
                authority.AppliedCount += (uint)appliedCount;
                authority.LastAppliedTick = timeState.Tick;
                authority.LastAppliedSequence = command.Sequence;

                if (auditBuffer.IsCreated)
                {
                    auditBuffer.Add(new AggregateEditAuditEntry
                    {
                        AppliedTick = timeState.Tick,
                        Target = command.Target,
                        TargetOrgId = command.TargetOrgId,
                        Scope = command.Scope,
                        ScopeAggregateType = command.ScopeAggregateType,
                        ScopeOrgKind = command.ScopeOrgKind,
                        Field = command.Field,
                        Op = command.Op,
                        Flags = command.Flags,
                        Value = command.Value,
                        ValueB = command.ValueB,
                        DurationTicks = command.DurationTicks,
                        Sequence = command.Sequence,
                        Result = (byte)(appliedCount > 0 ? 1 : 0),
                        Source = command.Source
                    });
                }

                if (telemetryBuffer.IsCreated)
                {
                    EmitTelemetryEvent(ref telemetryBuffer, command, timeState.Tick, appliedCount);
                }

                commandBuffer.RemoveAt(i);
            }

            state.EntityManager.SetComponentData(streamEntity, authority);
        }

        private int ApplyCommand(ref SystemState state, in AggregateEditCommand command, uint currentTick)
        {
            switch (command.Scope)
            {
                case AggregateEditScopeKind.Single:
                    if (TryResolveTarget(ref state, command, out var target))
                    {
                        return ApplyEditToAggregate(ref state, target, command, currentTick) ? 1 : 0;
                    }

                    return 0;
                case AggregateEditScopeKind.AggregateType:
                    return ApplyToAggregateType(ref state, command, currentTick);
                case AggregateEditScopeKind.OrgKind:
                    return ApplyToOrgKind(ref state, command, currentTick);
                case AggregateEditScopeKind.AllAggregates:
                    return ApplyToAllAggregates(ref state, command, currentTick);
                default:
                    return 0;
            }
        }

        private int ApplyToAggregateType(ref SystemState state, in AggregateEditCommand command, uint currentTick)
        {
            if (_aggregateEntityQuery.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var entities = _aggregateEntityQuery.ToEntityArray(Allocator.Temp);
            var aggregates = _aggregateEntityQuery.ToComponentDataArray<AggregateEntity>(Allocator.Temp);
            var appliedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (aggregates[i].Type != command.ScopeAggregateType)
                {
                    continue;
                }

                if (ApplyEditToAggregate(ref state, entities[i], command, currentTick))
                {
                    appliedCount++;
                }
            }

            entities.Dispose();
            aggregates.Dispose();
            return appliedCount;
        }

        private int ApplyToOrgKind(ref SystemState state, in AggregateEditCommand command, uint currentTick)
        {
            if (_orgIdQuery.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var entities = _orgIdQuery.ToEntityArray(Allocator.Temp);
            var orgIds = _orgIdQuery.ToComponentDataArray<OrgId>(Allocator.Temp);
            var appliedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (orgIds[i].Kind != command.ScopeOrgKind)
                {
                    continue;
                }

                if (ApplyEditToAggregate(ref state, entities[i], command, currentTick))
                {
                    appliedCount++;
                }
            }

            entities.Dispose();
            orgIds.Dispose();
            return appliedCount;
        }

        private int ApplyToAllAggregates(ref SystemState state, in AggregateEditCommand command, uint currentTick)
        {
            if (_aggregateIdentityQuery.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var entities = _aggregateIdentityQuery.ToEntityArray(Allocator.Temp);
            var appliedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (ApplyEditToAggregate(ref state, entities[i], command, currentTick))
                {
                    appliedCount++;
                }
            }

            entities.Dispose();
            return appliedCount;
        }

        private static bool TryResolveTarget(ref SystemState state, in AggregateEditCommand command, out Entity target)
        {
            var entityManager = state.EntityManager;

            if (command.Target != Entity.Null && entityManager.Exists(command.Target))
            {
                target = command.Target;
                return true;
            }

            if (command.TargetOrgId < 0)
            {
                target = Entity.Null;
                return false;
            }

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<OrgId>());
            if (query.IsEmptyIgnoreFilter)
            {
                target = Entity.Null;
                return false;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var orgIds = query.ToComponentDataArray<OrgId>(Allocator.Temp);
            target = Entity.Null;

            for (int i = 0; i < entities.Length; i++)
            {
                if (orgIds[i].Value != command.TargetOrgId)
                {
                    continue;
                }

                target = entities[i];
                break;
            }

            entities.Dispose();
            orgIds.Dispose();
            return target != Entity.Null;
        }

        private static bool ApplyEditToAggregate(ref SystemState state, Entity target, in AggregateEditCommand command, uint currentTick)
        {
            var entityManager = state.EntityManager;
            if (!IsAggregateCandidate(entityManager, target))
            {
                return false;
            }

            AggregatePopulationTuning tuning;
            if (entityManager.HasComponent<AggregatePopulationTuning>(target))
            {
                tuning = entityManager.GetComponentData<AggregatePopulationTuning>(target);
            }
            else
            {
                tuning = AggregatePopulationTuningDefaults.DefaultTuning();
            }

            var distributionChanged = ApplyEditToTuning(ref tuning, command);

            if (entityManager.HasComponent<AggregatePopulationTuning>(target))
            {
                entityManager.SetComponentData(target, tuning);
            }
            else
            {
                entityManager.AddComponentData(target, tuning);
            }

            if ((command.Flags & AggregateEditFlags.ApplyToExisting) != 0 && distributionChanged)
            {
                var duration = command.DurationTicks > 0 ? command.DurationTicks : DefaultDriftDurationTicks;
                var drift = new AggregatePopulationDrift
                {
                    IntelligenceTargetMean = tuning.Intelligence.Mean,
                    WillTargetMean = tuning.Will.Mean,
                    AppliedTick = currentTick,
                    DurationTicks = duration
                };

                if (entityManager.HasComponent<AggregatePopulationDrift>(target))
                {
                    entityManager.SetComponentData(target, drift);
                }
                else
                {
                    entityManager.AddComponentData(target, drift);
                }
            }

            return true;
        }

        private static bool ApplyEditToTuning(ref AggregatePopulationTuning tuning, in AggregateEditCommand command)
        {
            var distributionChanged = false;

            switch (command.Field)
            {
                case AggregateEditField.BirthRatePerTick:
                    tuning.BirthRatePerTick = ClampBirthRate(ApplyValue(tuning.BirthRatePerTick, command.Op, command.Value, command.ValueB));
                    break;
                case AggregateEditField.NewbornIntelligenceMean:
                    tuning.Intelligence.Mean = ApplyValue(tuning.Intelligence.Mean, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornIntelligenceVariance:
                    tuning.Intelligence.Variance = ApplyValue(tuning.Intelligence.Variance, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornIntelligenceMin:
                    tuning.Intelligence.Min = ApplyValue(tuning.Intelligence.Min, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornIntelligenceMax:
                    tuning.Intelligence.Max = ApplyValue(tuning.Intelligence.Max, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornWillMean:
                    tuning.Will.Mean = ApplyValue(tuning.Will.Mean, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornWillVariance:
                    tuning.Will.Variance = ApplyValue(tuning.Will.Variance, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornWillMin:
                    tuning.Will.Min = ApplyValue(tuning.Will.Min, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
                case AggregateEditField.NewbornWillMax:
                    tuning.Will.Max = ApplyValue(tuning.Will.Max, command.Op, command.Value, command.ValueB);
                    distributionChanged = true;
                    break;
            }

            tuning.Intelligence = NormalizeDistribution(tuning.Intelligence);
            tuning.Will = NormalizeDistribution(tuning.Will);
            return distributionChanged;
        }

        private static AggregateStatDistribution NormalizeDistribution(AggregateStatDistribution distribution)
        {
            distribution.Min = math.clamp(distribution.Min, StatMin, StatMax);
            distribution.Max = math.clamp(distribution.Max, StatMin, StatMax);

            if (distribution.Min > distribution.Max)
            {
                var mid = (distribution.Min + distribution.Max) * 0.5f;
                distribution.Min = mid;
                distribution.Max = mid;
            }

            distribution.Mean = math.clamp(distribution.Mean, distribution.Min, distribution.Max);
            distribution.Variance = math.clamp(distribution.Variance, 0f, StatMax);
            return distribution;
        }

        private static float ClampBirthRate(float value)
        {
            return math.clamp(value, BirthRateMin, BirthRateMax);
        }

        private static float ApplyValue(float current, AggregateEditOp op, float value, float valueB)
        {
            switch (op)
            {
                case AggregateEditOp.Set:
                    current = value;
                    break;
                case AggregateEditOp.Add:
                    current += value;
                    break;
                case AggregateEditOp.Multiply:
                    current *= value;
                    break;
                case AggregateEditOp.Clamp:
                    current = math.clamp(current, math.min(value, valueB), math.max(value, valueB));
                    break;
            }

            return current;
        }

        private static bool IsAggregateCandidate(EntityManager entityManager, Entity target)
        {
            return entityManager.HasComponent<AggregateIdentity>(target) || entityManager.HasComponent<AggregateEntity>(target);
        }

        private static DynamicBuffer<TelemetryEvent> GetTelemetryBuffer(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var streamEntity = TelemetryStreamUtility.EnsureEventStream(entityManager);
            if (streamEntity == Entity.Null || !entityManager.HasBuffer<TelemetryEvent>(streamEntity))
            {
                return default;
            }

            return entityManager.GetBuffer<TelemetryEvent>(streamEntity);
        }

        private static void EmitTelemetryEvent(ref DynamicBuffer<TelemetryEvent> buffer, in AggregateEditCommand command, uint tick, int appliedCount)
        {
            var payload = new FixedString128Bytes();
            payload.Append("{\"field\":\"");
            payload.Append(FieldToString(command.Field));
            payload.Append("\",\"op\":\"");
            payload.Append(OpToString(command.Op));
            payload.Append("\",\"applied\":");
            payload.Append(appliedCount);
            payload.Append("}");

            buffer.AddEvent(EventType, tick, EventSource, payload);
        }

        private static FixedString32Bytes FieldToString(AggregateEditField field)
        {
            return field switch
            {
                AggregateEditField.BirthRatePerTick => new FixedString32Bytes("birth_rate"),
                AggregateEditField.NewbornIntelligenceMean => new FixedString32Bytes("int_mean"),
                AggregateEditField.NewbornIntelligenceVariance => new FixedString32Bytes("int_var"),
                AggregateEditField.NewbornIntelligenceMin => new FixedString32Bytes("int_min"),
                AggregateEditField.NewbornIntelligenceMax => new FixedString32Bytes("int_max"),
                AggregateEditField.NewbornWillMean => new FixedString32Bytes("will_mean"),
                AggregateEditField.NewbornWillVariance => new FixedString32Bytes("will_var"),
                AggregateEditField.NewbornWillMin => new FixedString32Bytes("will_min"),
                AggregateEditField.NewbornWillMax => new FixedString32Bytes("will_max"),
                _ => new FixedString32Bytes("unknown")
            };
        }

        private static FixedString32Bytes OpToString(AggregateEditOp op)
        {
            return op switch
            {
                AggregateEditOp.Set => new FixedString32Bytes("set"),
                AggregateEditOp.Add => new FixedString32Bytes("add"),
                AggregateEditOp.Multiply => new FixedString32Bytes("mul"),
                AggregateEditOp.Clamp => new FixedString32Bytes("clamp"),
                _ => new FixedString32Bytes("unknown")
            };
        }
    }
}
