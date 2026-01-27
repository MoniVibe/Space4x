using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// Updates villager resource awareness using perceived entities to avoid world scans.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct VillagerResourceAwarenessSystem : ISystem
    {
        private ComponentLookup<ResourceNodeSummary> _nodeLookup;
        private ComponentLookup<StorehouseInventory> _storehouseLookup;
        private ComponentLookup<VillagerResourceNeed> _needLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerResourceAwareness>();
            state.RequireForUpdate<TimeState>();
            _nodeLookup = state.GetComponentLookup<ResourceNodeSummary>(true);
            _storehouseLookup = state.GetComponentLookup<StorehouseInventory>(true);
            _needLookup = state.GetComponentLookup<VillagerResourceNeed>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var config = SystemAPI.HasSingleton<VillagerResourceAwarenessConfig>()
                ? SystemAPI.GetSingleton<VillagerResourceAwarenessConfig>()
                : VillagerResourceAwarenessConfig.Default;

            if (!CadenceGate.ShouldRun(timeState.Tick, config.CadenceTicks))
            {
                return;
            }

            _nodeLookup.Update(ref state);
            _storehouseLookup.Update(ref state);
            _needLookup.Update(ref state);

            var job = new AwarenessJob
            {
                CurrentTick = timeState.Tick,
                MaxDistance = math.max(0f, config.MaxDistance),
                MinConfidence = config.MinConfidence,
                StaleTicks = config.StaleTicks,
                NodeLookup = _nodeLookup,
                StorehouseLookup = _storehouseLookup,
                NeedLookup = _needLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct AwarenessJob : IJobEntity
        {
            public uint CurrentTick;
            public float MaxDistance;
            public float MinConfidence;
            public uint StaleTicks;
            [ReadOnly] public ComponentLookup<ResourceNodeSummary> NodeLookup;
            [ReadOnly] public ComponentLookup<StorehouseInventory> StorehouseLookup;
            [ReadOnly] public ComponentLookup<VillagerResourceNeed> NeedLookup;

            public void Execute(
                Entity entity,
                ref VillagerResourceAwareness awareness,
                in DynamicBuffer<PerceivedEntity> perceived)
            {
                var desiredType = ushort.MaxValue;
                if (NeedLookup.HasComponent(entity))
                {
                    desiredType = NeedLookup[entity].ResourceTypeIndex;
                }

                Entity bestNode = Entity.Null;
                float bestNodeDistance = float.MaxValue;
                float bestNodeConfidence = 0f;
                ushort bestNodeType = ushort.MaxValue;

                Entity bestStorehouse = Entity.Null;
                float bestStorehouseDistance = float.MaxValue;

                for (int i = 0; i < perceived.Length; i++)
                {
                    var contact = perceived[i];
                    if (contact.TargetEntity == Entity.Null || contact.TargetEntity == entity)
                    {
                        continue;
                    }

                    if (contact.Confidence < MinConfidence)
                    {
                        continue;
                    }

                    if (MaxDistance > 0f && contact.Distance > MaxDistance)
                    {
                        continue;
                    }

                    if (NodeLookup.HasComponent(contact.TargetEntity))
                    {
                        var node = NodeLookup[contact.TargetEntity];
                        if (node.IsDepleted != 0 || node.UnitsRemaining <= 0f)
                        {
                            continue;
                        }

                        if (desiredType != ushort.MaxValue && node.ResourceTypeIndex != desiredType)
                        {
                            continue;
                        }

                        if (contact.Distance < bestNodeDistance)
                        {
                            bestNodeDistance = contact.Distance;
                            bestNode = contact.TargetEntity;
                            bestNodeConfidence = contact.Confidence;
                            bestNodeType = node.ResourceTypeIndex;
                        }
                    }
                    else if (StorehouseLookup.HasComponent(contact.TargetEntity))
                    {
                        if (contact.Distance < bestStorehouseDistance)
                        {
                            bestStorehouseDistance = contact.Distance;
                            bestStorehouse = contact.TargetEntity;
                        }
                    }
                }

                var updated = awareness;
                var updatedAny = false;
                var isStale = StaleTicks > 0u
                              && CurrentTick > updated.LastSeenTick
                              && CurrentTick - updated.LastSeenTick > StaleTicks;

                if (bestNode != Entity.Null)
                {
                    updated.KnownNode = bestNode;
                    updated.ResourceTypeIndex = desiredType != ushort.MaxValue ? desiredType : bestNodeType;
                    updated.Confidence = bestNodeConfidence;
                    updated.LastSeenTick = CurrentTick;
                    updatedAny = true;
                }
                else if (isStale)
                {
                    updated.KnownNode = Entity.Null;
                    updated.ResourceTypeIndex = ushort.MaxValue;
                    updated.Confidence = 0f;
                    updated.LastSeenTick = CurrentTick;
                    updatedAny = true;
                }

                if (bestStorehouse != Entity.Null)
                {
                    updated.KnownStorehouse = bestStorehouse;
                    if (!updatedAny)
                    {
                        updated.LastSeenTick = CurrentTick;
                        updatedAny = true;
                    }
                }
                else if (isStale)
                {
                    updated.KnownStorehouse = Entity.Null;
                    updatedAny = true;
                }

                if (updatedAny)
                {
                    awareness = updated;
                }
            }
        }
    }
}
