using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Mobility;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Performance;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Performance;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.AI
{
    internal struct AISensorCategoryMask
    {
        public FixedList32Bytes<AISensorCategory> Categories;

        public static AISensorCategoryMask FromConfig(in AISensorConfig config)
        {
            var mask = new AISensorCategoryMask { Categories = default };
            if (config.PrimaryCategory != AISensorCategory.None)
            {
                mask.Categories.Add(config.PrimaryCategory);
            }

            if (config.SecondaryCategory != AISensorCategory.None &&
                config.SecondaryCategory != config.PrimaryCategory)
            {
                mask.Categories.Add(config.SecondaryCategory);
            }

            return mask;
        }
    }

    internal struct AISensorCategoryFilter : ISpatialQueryFilter
    {
        [ReadOnly] public NativeArray<AISensorCategoryMask> Masks;
        [ReadOnly] public ComponentLookup<VillagerId> VillagerLookup;
        [ReadOnly] public ComponentLookup<ResourceSourceConfig> ResourceLookup;
        [ReadOnly] public ComponentLookup<StorehouseConfig> StorehouseLookup;
        [ReadOnly] public ComponentLookup<MiracleDefinition> MiracleDefinitionLookup;
        [ReadOnly] public ComponentLookup<MiracleRuntimeState> MiracleStateLookup;
        [ReadOnly] public ComponentLookup<MiracleRuntimeStateNew> MiracleStateNewLookup;
        [ReadOnly] public ComponentLookup<TransportUnitTag> TransportLookup;

        public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
        {
            if (!Masks.IsCreated || descriptorIndex >= Masks.Length)
            {
                return true;
            }

            var categories = Masks[descriptorIndex].Categories;
            if (categories.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                switch (category)
                {
                    case AISensorCategory.Villager:
                        if (VillagerLookup.HasComponent(entry.Entity))
                        {
                            return true;
                        }
                        break;
                    case AISensorCategory.ResourceNode:
                        if (ResourceLookup.HasComponent(entry.Entity))
                        {
                            return true;
                        }
                        break;
                    case AISensorCategory.Storehouse:
                        if (StorehouseLookup.HasComponent(entry.Entity))
                        {
                            return true;
                        }
                        break;
                    case AISensorCategory.TransportUnit:
                        if (TransportLookup.HasComponent(entry.Entity))
                        {
                            return true;
                        }
                        break;
                    case AISensorCategory.Miracle:
                        if (MiracleDefinitionLookup.HasComponent(entry.Entity) ||
                            MiracleStateLookup.HasComponent(entry.Entity) ||
                            MiracleStateNewLookup.HasComponent(entry.Entity))
                        {
                            return true;
                        }
                        break;
                    default:
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// DERIVED path: Updates AI sensor awareness from Perception outputs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct AISensorUpdateSystem : ISystem
    {
        private ComponentLookup<VillagerId> _villagerLookup;
        private ComponentLookup<ResourceSourceConfig> _resourceLookup;
        private ComponentLookup<StorehouseConfig> _storehouseLookup;
        private ComponentLookup<MiracleDefinition> _miracleDefinitionLookup;
        private ComponentLookup<MiracleRuntimeState> _miracleStateLookup;
        private ComponentLookup<MiracleRuntimeStateNew> _miracleStateNewLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<TransportUnitTag> _transportLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<UniversalPerformanceBudget>();
            state.RequireForUpdate<UniversalPerformanceCounters>();
            state.RequireForUpdate<MindCadenceSettings>();
            state.RequireForUpdate<AISensorConfig>();

            _villagerLookup = state.GetComponentLookup<VillagerId>(true);
            _resourceLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _storehouseLookup = state.GetComponentLookup<StorehouseConfig>(true);
            _miracleDefinitionLookup = state.GetComponentLookup<MiracleDefinition>(true);
            _miracleStateLookup = state.GetComponentLookup<MiracleRuntimeState>(true);
            _miracleStateNewLookup = state.GetComponentLookup<MiracleRuntimeStateNew>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);
            _transportLookup = state.GetComponentLookup<TransportUnitTag>(true);
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.SensorCadenceTicks))
            {
                return;
            }

            _villagerLookup.Update(ref state);
            _resourceLookup.Update(ref state);
            _storehouseLookup.Update(ref state);
            _miracleDefinitionLookup.Update(ref state);
            _miracleStateLookup.Update(ref state);
            _miracleStateNewLookup.Update(ref state);
            _residencyLookup.Update(ref state);
            _transportLookup.Update(ref state);
            _perceivedLookup.Update(ref state);

            var budget = SystemAPI.GetSingleton<UniversalPerformanceBudget>();
            var counters = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();
            var results = new NativeList<AISensorReading>(Allocator.Temp);

            int processedCount = 0;
            foreach (var (config, sensorState, readingsBuffer, entity) in
                     SystemAPI.Query<RefRO<AISensorConfig>, RefRW<AISensorState>, DynamicBuffer<AISensorReading>>()
                         .WithEntityAccess())
            {
                var readings = readingsBuffer;
                if (processedCount >= budget.MaxPerceptionChecksPerTick)
                {
                    break;
                }

                var sensorConfig = config.ValueRO;
                var stateRef = sensorState.ValueRW;
                stateRef.Elapsed += timeState.FixedDeltaTime;

                if (sensorConfig.UpdateInterval > 0f &&
                    stateRef.Elapsed + 1e-5f < sensorConfig.UpdateInterval)
                {
                    sensorState.ValueRW = stateRef;
                    continue;
                }

                stateRef.Elapsed = 0f;
                stateRef.LastSampleTick = timeState.Tick;
                sensorState.ValueRW = stateRef;
                processedCount++;

                readings.Clear();

                if (!_perceivedLookup.HasBuffer(entity))
                {
                    continue;
                }

                var perceived = _perceivedLookup[entity];
                if (perceived.Length == 0)
                {
                    continue;
                }

                var maxResults = math.max(1, sensorConfig.MaxResults);
                if (results.Capacity < maxResults)
                {
                    results.Capacity = maxResults;
                }

                results.Clear();
                var mask = AISensorCategoryMask.FromConfig(sensorConfig);

                for (int i = 0; i < perceived.Length; i++)
                {
                    var contact = perceived[i];
                    if (contact.TargetEntity == Entity.Null)
                    {
                        continue;
                    }

                    var category = ResolveCategory(contact.TargetEntity, mask, _villagerLookup, _resourceLookup, _storehouseLookup,
                        _miracleDefinitionLookup, _miracleStateLookup, _miracleStateNewLookup, _transportLookup);
                    if (category == AISensorCategory.None)
                    {
                        continue;
                    }

                    var distance = math.max(0f, contact.Distance);
                    var distanceSq = distance * distance;
                    var normalized = ComputeSensorScore(distanceSq, sensorConfig.Range);
                    var cellId = -1;
                    uint spatialVersion = 0;

                    if (_residencyLookup.HasComponent(contact.TargetEntity))
                    {
                        var residency = _residencyLookup[contact.TargetEntity];
                        cellId = residency.CellId;
                        spatialVersion = residency.Version;
                    }

                    InsertSortedByDistance(ref results, new AISensorReading
                    {
                        Target = contact.TargetEntity,
                        DistanceSq = distanceSq,
                        NormalizedScore = normalized,
                        CellId = cellId,
                        SpatialVersion = spatialVersion,
                        Category = category
                    }, maxResults);
                }

                if (results.Length == 0)
                {
                    continue;
                }

                readings.ResizeUninitialized(results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    readings[i] = results[i];
                }
            }

            counters.ValueRW.PerceptionChecksThisTick += processedCount;
            counters.ValueRW.TotalWarmOperationsThisTick += processedCount;
            results.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static void InsertSortedByDistance(ref NativeList<AISensorReading> readings, in AISensorReading reading, int maxResults)
        {
            if (maxResults <= 0)
            {
                return;
            }

            var length = readings.Length;
            if (length == 0)
            {
                readings.Add(reading);
                return;
            }

            if (length >= maxResults && reading.DistanceSq >= readings[length - 1].DistanceSq)
            {
                return;
            }

            if (length < maxResults)
            {
                readings.Add(reading);
                length++;
            }
            else
            {
                readings.Add(readings[length - 1]);
                length++;
            }

            var index = length - 1;
            while (index > 0 && reading.DistanceSq < readings[index - 1].DistanceSq)
            {
                readings[index] = readings[index - 1];
                index--;
            }

            readings[index] = reading;

            if (length > maxResults)
            {
                readings.RemoveAt(length - 1);
            }
        }

        private static float ComputeSensorScore(float distanceSq, float range)
        {
            var distance = math.sqrt(math.max(distanceSq, 1e-6f));
            var normalized = 1f - distance / math.max(range, 1e-3f);
            return math.saturate(normalized);
        }

        private static AISensorCategory ResolveCategory(
            Entity entity,
            in AISensorCategoryMask mask,
            ComponentLookup<VillagerId> villagerLookup,
            ComponentLookup<ResourceSourceConfig> resourceLookup,
            ComponentLookup<StorehouseConfig> storehouseLookup,
            ComponentLookup<MiracleDefinition> miracleDefinitionLookup,
            ComponentLookup<MiracleRuntimeState> miracleStateLookup,
            ComponentLookup<MiracleRuntimeStateNew> miracleStateNewLookup,
            ComponentLookup<TransportUnitTag> transportLookup
        )
        {
            if (entity == Entity.Null)
            {
                return AISensorCategory.None;
            }

            var categories = mask.Categories;
            if (categories.Length > 0)
            {
                for (var i = 0; i < categories.Length; i++)
                {
                    var category = categories[i];
                    if (MatchesCategory(entity, category, villagerLookup, resourceLookup, storehouseLookup,
                        miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                    {
                        return category;
                    }
                }
            }
            else
            {
                if (MatchesCategory(entity, AISensorCategory.Villager, villagerLookup, resourceLookup, storehouseLookup,
                    miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                {
                    return AISensorCategory.Villager;
                }

                if (MatchesCategory(entity, AISensorCategory.ResourceNode, villagerLookup, resourceLookup, storehouseLookup,
                    miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                {
                    return AISensorCategory.ResourceNode;
                }

                if (MatchesCategory(entity, AISensorCategory.Storehouse, villagerLookup, resourceLookup, storehouseLookup,
                    miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                {
                    return AISensorCategory.Storehouse;
                }

                if (MatchesCategory(entity, AISensorCategory.Miracle, villagerLookup, resourceLookup, storehouseLookup,
                    miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                {
                    return AISensorCategory.Miracle;
                }

                if (MatchesCategory(entity, AISensorCategory.TransportUnit, villagerLookup, resourceLookup, storehouseLookup,
                    miracleDefinitionLookup, miracleStateLookup, miracleStateNewLookup, transportLookup))
                {
                    return AISensorCategory.TransportUnit;
                }
            }

            return AISensorCategory.None;
        }

        private static bool MatchesCategory(
            Entity entity,
            AISensorCategory category,
            ComponentLookup<VillagerId> villagerLookup,
            ComponentLookup<ResourceSourceConfig> resourceLookup,
            ComponentLookup<StorehouseConfig> storehouseLookup,
            ComponentLookup<MiracleDefinition> miracleDefinitionLookup,
            ComponentLookup<MiracleRuntimeState> miracleStateLookup,
            ComponentLookup<MiracleRuntimeStateNew> miracleStateNewLookup,
            ComponentLookup<TransportUnitTag> transportLookup
        )
        {
            return category switch
            {
                AISensorCategory.Villager => villagerLookup.HasComponent(entity),
                AISensorCategory.ResourceNode => resourceLookup.HasComponent(entity),
                AISensorCategory.Storehouse => storehouseLookup.HasComponent(entity),
                AISensorCategory.Miracle =>
                    miracleDefinitionLookup.HasComponent(entity) ||
                    miracleStateLookup.HasComponent(entity) ||
                    miracleStateNewLookup.HasComponent(entity),
                AISensorCategory.TransportUnit => transportLookup.HasComponent(entity),
                _ => true
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(AISensorUpdateSystem))]
    public partial struct AIUtilityScoringSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<AIBehaviourArchetype>();
            state.RequireForUpdate<AISensorConfig>();
            state.RequireForUpdate<MindCadenceSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.EvaluationCadenceTicks))
            {
                return;
            }

            _transformLookup.Update(ref state);

            foreach (var (archetype, utilityState, sensorBuffer, transform, entity) in SystemAPI.Query<RefRO<AIBehaviourArchetype>, RefRW<AIUtilityState>, DynamicBuffer<AISensorReading>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var blobRef = archetype.ValueRO.UtilityBlob;
                if (!blobRef.IsCreated)
                {
                    continue;
                }

                ref var blob = ref blobRef.Value;
                if (blob.Actions.Length == 0)
                {
                    continue;
                }

                var readings = sensorBuffer.AsNativeArray();
                var bestScore = float.MinValue;
                byte bestIndex = 0;

                var hasActionBuffer = SystemAPI.HasBuffer<AIActionState>(entity);
                DynamicBuffer<AIActionState> actionBuffer = default;
                if (hasActionBuffer)
                {
                    actionBuffer = SystemAPI.GetBuffer<AIActionState>(entity);
                    actionBuffer.Clear();
                    actionBuffer.ResizeUninitialized(blob.Actions.Length);
                }

                for (var actionIndex = 0; actionIndex < blob.Actions.Length; actionIndex++)
                {
                    ref var action = ref blob.Actions[actionIndex];
                    var score = 0f;

                    for (var factorIndex = 0; factorIndex < action.Factors.Length; factorIndex++)
                    {
                        ref var factor = ref action.Factors[factorIndex];
                        var sensorValue = factor.SensorIndex < readings.Length
                            ? readings[factor.SensorIndex].NormalizedScore
                            : 0f;
                        score += EvaluateCurve(sensorValue, in factor);
                    }

                    if (hasActionBuffer)
                    {
                        actionBuffer[actionIndex] = new AIActionState
                        {
                            Score = score
                        };
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = (byte)actionIndex;
                    }
                }

                var stateRef = utilityState.ValueRW;
                stateRef.BestActionIndex = bestIndex;
                stateRef.BestScore = bestScore;
                stateRef.LastEvaluationTick = timeState.Tick;
                utilityState.ValueRW = stateRef;

                if (state.EntityManager.HasComponent<AITargetState>(entity))
                {
                    var targetState = state.EntityManager.GetComponentData<AITargetState>(entity);
                    targetState.ActionIndex = bestIndex;
                    targetState.Flags = 0;
                    targetState.TargetEntity = readings.Length > 0 ? readings[0].Target : Entity.Null;
                    targetState.TargetPosition = targetState.TargetEntity != Entity.Null && _transformLookup.HasComponent(targetState.TargetEntity)
                        ? _transformLookup[targetState.TargetEntity].Position
                        : transform.ValueRO.Position;

                    SystemAPI.SetComponent(entity, targetState);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static float EvaluateCurve(float sensorValue, in AIUtilityCurveBlob curve)
        {
            var normalized = math.saturate(sensorValue / math.max(curve.MaxValue, 1e-3f));
            var delta = math.max(normalized - curve.Threshold, 0f);
            return math.pow(delta, math.max(curve.ResponsePower, 1f)) * curve.Weight;
        }
    }

    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(AIUtilityScoringSystem))]
    public partial struct AISteeringSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<FlowFieldAgentTag> _flowAgentLookup;
        private ComponentLookup<FlowFieldGoalTag> _flowGoalLookup;
        private ComponentLookup<FlowFieldState> _flowStateLookup;
        private ComponentLookup<SteeringState> _localSteeringLookup;

        public void OnCreate(ref SystemState state)
        {
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _flowAgentLookup = state.GetComponentLookup<FlowFieldAgentTag>(true);
            _flowGoalLookup = state.GetComponentLookup<FlowFieldGoalTag>(true);
            _flowStateLookup = state.GetComponentLookup<FlowFieldState>();
            _localSteeringLookup = state.GetComponentLookup<SteeringState>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<AISteeringConfig>();
            state.RequireForUpdate<AISteeringState>();
            state.RequireForUpdate<AITargetState>();
            state.RequireForUpdate<MindCadenceSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.EvaluationCadenceTicks))
            {
                return;
            }

            _entityInfoLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _flowAgentLookup.Update(ref state);
            _flowGoalLookup.Update(ref state);
            _flowStateLookup.Update(ref state);
            _localSteeringLookup.Update(ref state);

            foreach (var (config, steering, target, transform, entity) in SystemAPI.Query<RefRO<AISteeringConfig>, RefRW<AISteeringState>, RefRO<AITargetState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var steeringState = steering.ValueRW;
                var targetState = target.ValueRO;
                var steeringConfig = config.ValueRO;

                var targetPosition = targetState.TargetPosition;
                var targetEntity = targetState.TargetEntity;
                var targetValid = targetEntity != Entity.Null && _entityInfoLookup.Exists(targetEntity);
                if (targetValid && _transformLookup.HasComponent(targetEntity))
                {
                    targetPosition = _transformLookup[targetEntity].Position;
                }

                var direction = targetPosition - transform.ValueRO.Position;
                direction = ProjectDegreesOfFreedom(direction, steeringConfig.DegreesOfFreedom);

                var distance = math.length(direction);
                var desiredDirection = distance > 1e-4f
                    ? math.normalizesafe(direction)
                    : float3.zero;
                var localHeading2 = float2.zero;
                var hasLocalHeading = false;

                if (_flowAgentLookup.HasComponent(entity) && _flowStateLookup.HasComponent(entity))
                {
                    var flowState = _flowStateLookup[entity];
                    if (targetValid && _flowGoalLookup.HasComponent(targetEntity))
                    {
                        var goalTag = _flowGoalLookup[targetEntity];
                        if (flowState.CurrentLayerId != goalTag.LayerId)
                        {
                            flowState.CurrentLayerId = goalTag.LayerId;
                            _flowStateLookup[entity] = flowState;
                        }
                    }

                    var flowWeight = math.saturate(steeringConfig.FlowFieldWeight);
                    if (flowWeight > 0f)
                    {
                        var flowHeading2 = flowState.CachedDirection;
                        if (_localSteeringLookup.HasComponent(entity))
                        {
                            localHeading2 = _localSteeringLookup[entity].BlendedHeading;
                            hasLocalHeading = math.lengthsq(localHeading2) > 1e-4f;
                        }

                        if (math.lengthsq(flowHeading2) > 1e-4f)
                        {
                            var flowHeading3 = new float3(flowHeading2.x, 0f, flowHeading2.y);
                            flowHeading3 = ProjectDegreesOfFreedom(flowHeading3, steeringConfig.DegreesOfFreedom);

                            if (math.lengthsq(flowHeading3) > 1e-4f)
                            {
                                flowHeading3 = math.normalizesafe(flowHeading3);
                                var desired2 = new float2(desiredDirection.x, desiredDirection.z);
                                var flow2 = new float2(flowHeading3.x, flowHeading3.z);
                                AISteeringUtilities.BlendWithFlowField(ref flow2, ref desired2, flowWeight, 1f - flowWeight, out var blended2);
                                desiredDirection = math.normalizesafe(new float3(blended2.x, 0f, blended2.y));
                            }
                        }
                    }
                }

                if (_localSteeringLookup.HasComponent(entity) && !hasLocalHeading)
                {
                    localHeading2 = _localSteeringLookup[entity].BlendedHeading;
                    hasLocalHeading = math.lengthsq(localHeading2) > 1e-4f;
                }

                if (hasLocalHeading)
                {
                    var localHeading3 = new float3(localHeading2.x, 0f, localHeading2.y);
                    localHeading3 = ProjectDegreesOfFreedom(localHeading3, steeringConfig.DegreesOfFreedom);
                    if (math.lengthsq(localHeading3) > 1e-4f)
                    {
                        desiredDirection = math.normalizesafe(desiredDirection + math.normalizesafe(localHeading3));
                    }
                }

                var responsiveness = math.saturate(steeringConfig.Responsiveness);
                steeringState.DesiredDirection = math.normalizesafe(math.lerp(steeringState.DesiredDirection, desiredDirection, responsiveness));

                var maxSpeed = math.max(0f, steeringConfig.MaxSpeed);
                var acceleration = math.max(0f, steeringConfig.Acceleration);
                var deltaTime = math.max(timeState.FixedDeltaTime, 1e-4f);
                var targetSpeed = math.min(maxSpeed, distance / deltaTime);
                var desiredVelocity = steeringState.DesiredDirection * targetSpeed;
                var lerpFactor = math.saturate(acceleration * deltaTime);

                steeringState.LinearVelocity = math.lerp(steeringState.LinearVelocity, desiredVelocity, lerpFactor);
                steeringState.LastSampledTarget = targetPosition;
                steeringState.LastUpdateTick = timeState.Tick;

                steering.ValueRW = steeringState;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static float3 ProjectDegreesOfFreedom(float3 vector, byte degreesOfFreedom)
        {
            if (degreesOfFreedom <= 2)
            {
                vector.y = 0f;
            }

            return vector;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(AISteeringSystem))]
    public partial struct AITaskResolutionSystem : ISystem
    {
        private ComponentLookup<AIAckConfig> _ackConfigLookup;
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<FocusBudget> _focusLookup;
        private ComponentLookup<ResourcePools> _poolsLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VillagerNeedState> _villagerNeedsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AICommandQueueTag>();
            state.RequireForUpdate<AIUtilityState>();
            state.RequireForUpdate<AITargetState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<MindCadenceSettings>();

            _ackConfigLookup = state.GetComponentLookup<AIAckConfig>(true);
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _focusLookup = state.GetComponentLookup<FocusBudget>(true);
            _poolsLookup = state.GetComponentLookup<ResourcePools>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _villagerNeedsLookup = state.GetComponentLookup<VillagerNeedState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<AICommandQueueTag>();
            var commands = state.EntityManager.GetBuffer<AICommand>(queueEntity);
            commands.Clear();

            var cadenceSettings = SystemAPI.GetSingleton<MindCadenceSettings>();
            if (!CadenceGate.ShouldRun(timeState.Tick, cadenceSettings.ResolutionCadenceTicks))
            {
                return;
            }

            _ackConfigLookup.Update(ref state);
            _alignmentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _poolsLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _villagerNeedsLookup.Update(ref state);

            var hasAckStream = SystemAPI.HasSingleton<AIAckStreamTag>();
            DynamicBuffer<AIAckEvent> ackEvents = default;
            var ackBudget = int.MaxValue;
            RefRW<UniversalPerformanceCounters> countersRW = default;
            if (hasAckStream)
            {
                var ackEntity = SystemAPI.GetSingletonEntity<AIAckStreamTag>();
                ackEvents = state.EntityManager.GetBuffer<AIAckEvent>(ackEntity);
                if (SystemAPI.HasSingleton<UniversalPerformanceBudget>() && SystemAPI.HasSingleton<UniversalPerformanceCounters>())
                {
                    ackBudget = math.max(0, SystemAPI.GetSingleton<UniversalPerformanceBudget>().MaxAckEventsPerTick);
                    countersRW = SystemAPI.GetSingletonRW<UniversalPerformanceCounters>();
                }
            }

            foreach (var (utility, target, entity) in SystemAPI.Query<RefRO<AIUtilityState>, RefRO<AITargetState>>()
                         .WithEntityAccess())
            {
                var utilityState = utility.ValueRO;
                if (utilityState.LastEvaluationTick != timeState.Tick || utilityState.BestScore <= 0f)
                {
                    continue;
                }

                var targetState = target.ValueRO;
                var cmd = new AICommand
                {
                    Agent = entity,
                    ActionIndex = utilityState.BestActionIndex,
                    TargetEntity = targetState.TargetEntity,
                    TargetPosition = targetState.TargetPosition,
                    AckToken = 0u,
                    AckFlags = AIAckRequestFlags.None
                };

                if (_ackConfigLookup.HasComponent(entity))
                {
                    var config = _ackConfigLookup[entity];
                    if (config.Enabled != 0)
                    {
                        // Deterministic token for this (tick, agent, action, target).
                        var token = math.hash(new uint4(timeState.Tick, (uint)entity.Index, (uint)cmd.ActionIndex, (uint)cmd.TargetEntity.Index));

                        var hasAlignment = _alignmentLookup.HasComponent(entity);
                        var alignment = hasAlignment ? _alignmentLookup[entity] : default;
                        var chaos01 = hasAlignment ? AIAckPolicyUtility.ComputeChaos01(in alignment) : 0.5f;

                        var hasFocus = _focusLookup.HasComponent(entity);
                        var focus = hasFocus ? _focusLookup[entity] : default;
                        var hasPools = _poolsLookup.HasComponent(entity);
                        var pools = hasPools ? _poolsLookup[entity] : default;
                        var focusRatio01 = AIAckPolicyUtility.ComputeFocusRatio01(hasFocus, in focus, hasPools, in pools);

                        var hasNeeds = _villagerNeedsLookup.HasComponent(entity);
                        var needs = hasNeeds ? _villagerNeedsLookup[entity] : default;
                        var hasStats = _statsLookup.HasComponent(entity);
                        var stats = hasStats ? _statsLookup[entity] : default;
                        var sleep01 = AIAckPolicyUtility.ComputeSleepPressure01(hasNeeds, in needs, hasPools, in pools, hasStats, in stats);

                        if (AIAckPolicyUtility.ShouldRequestReceiptAcks(in config, focusRatio01, sleep01, chaos01, token))
                        {
                            cmd.AckToken = token;
                            cmd.AckFlags |= AIAckRequestFlags.RequestReceipt;
                        }

                        if (config.EmitsIssuedAcks != 0)
                        {
                            cmd.AckToken = cmd.AckToken == 0u ? token : cmd.AckToken;
                            cmd.AckFlags |= AIAckRequestFlags.EmitIssuedAck;
                        }

                        // Optional issued ack stream emission (bounded).
                        if (hasAckStream && (cmd.AckFlags & AIAckRequestFlags.EmitIssuedAck) != 0 && ackBudget > 0)
                        {
                            ackBudget--;
                            if (countersRW.IsValid)
                            {
                                countersRW.ValueRW.AckEventsEmittedThisTick++;
                                countersRW.ValueRW.TotalWarmOperationsThisTick++;
                            }

                            ackEvents.Add(new AIAckEvent
                            {
                                Tick = timeState.Tick,
                                Token = cmd.AckToken,
                                Agent = entity,
                                TargetEntity = cmd.TargetEntity,
                                ActionIndex = cmd.ActionIndex,
                                Stage = AIAckStage.Issued,
                                Reason = AIAckReason.None,
                                Flags = (byte)cmd.AckFlags
                            });
                        }
                        else if (hasAckStream && (cmd.AckFlags & AIAckRequestFlags.EmitIssuedAck) != 0 && ackBudget == 0 && countersRW.IsValid)
                        {
                            countersRW.ValueRW.AckEventsDroppedThisTick++;
                            countersRW.ValueRW.TotalOperationsDroppedThisTick++;
                        }
                    }
                }

                commands.Add(cmd);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
