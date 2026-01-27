using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace PureDOTS.Systems.Streaming
{
    /// <summary>
    /// Examines streaming focus points and issues deterministic load/unload commands.
    /// </summary>
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    public partial struct StreamingScannerSystem : ISystem
    {
        private EntityQuery _sectionQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StreamingCoordinator>();

            _sectionQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StreamingSectionDescriptor>(),
                    ComponentType.ReadWrite<StreamingSectionState>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_sectionQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var focusList = new NativeList<StreamingFocus>(Allocator.Temp);
            foreach (var focus in SystemAPI.Query<RefRO<StreamingFocus>>())
            {
                focusList.Add(focus.ValueRO);
            }

            if (focusList.Length == 0)
            {
                focusList.Dispose();
                return;
            }

            var coordinatorEntity = SystemAPI.GetSingletonEntity<StreamingCoordinator>();
            var coordinator = SystemAPI.GetSingleton<StreamingCoordinator>();
            Assert.AreEqual(coordinator.WorldSequenceNumber, (uint)state.WorldUnmanaged.SequenceNumber,
                "[PureDOTS] StreamingCoordinator belongs to a different world.");
            var commands = state.EntityManager.GetBuffer<StreamingSectionCommand>(coordinatorEntity);
            var timeTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            foreach (var (descriptor, sectionState, entity) in SystemAPI
                         .Query<RefRO<StreamingSectionDescriptor>, RefRW<StreamingSectionState>>()
                         .WithEntityAccess())
            {
                var desc = descriptor.ValueRO;
                var stateValue = sectionState.ValueRO;

                if ((desc.Flags & StreamingSectionFlags.Manual) != 0)
                {
                    continue;
                }

                bool desired = false;
                bool withinExit = false;
                float bestDistance = float.MaxValue;
                float velocityBias = 0f;

                for (int i = 0; i < focusList.Length; i++)
                {
                    var focus = focusList[i];

                    float radiusScale = math.max(0.01f, focus.RadiusScale);
                    float enterRadius = desc.EnterRadius * radiusScale + focus.LoadRadiusOffset;
                    float exitRadius = desc.ExitRadius * radiusScale + focus.UnloadRadiusOffset;
                    if (exitRadius < enterRadius)
                    {
                        exitRadius = enterRadius + 0.5f;
                    }

                    float distance = math.distance(focus.Position, desc.Center);
                    bestDistance = math.min(bestDistance, distance);

                    if (distance <= enterRadius)
                    {
                        desired = true;
                    }

                    if (distance <= exitRadius)
                    {
                        withinExit = true;
                    }

                    var directionToSection = math.normalizesafe(desc.Center - focus.Position);
                    var focusVelocity = focus.Velocity;
                    if (!math.all(focusVelocity == float3.zero))
                    {
                        var heading = math.dot(math.normalizesafe(focusVelocity), directionToSection);
                        velocityBias = math.max(velocityBias, heading);
                    }
                }

                sectionState.ValueRW.LastSeenTick = timeTick;

                if (timeTick < stateValue.CooldownUntilTick)
                {
                    continue;
                }

                bool isLoadedOrPending = stateValue.Status is StreamingSectionStatus.Loaded
                    or StreamingSectionStatus.Loading
                    or StreamingSectionStatus.QueuedLoad;
                bool isUnloadPending = stateValue.Status is StreamingSectionStatus.Unloading
                    or StreamingSectionStatus.QueuedUnload;
                bool isError = stateValue.Status == StreamingSectionStatus.Error;

                if (isError && desired)
                {
                    sectionState.ValueRW.Status = StreamingSectionStatus.Unloaded;
                    stateValue = sectionState.ValueRO;
                }

                if (desired)
                {
                    if (!isLoadedOrPending)
                    {
                        float score = bestDistance - desc.Priority * 10f - velocityBias * 25f + desc.EstimatedCost;
                        commands.Add(new StreamingSectionCommand
                        {
                            SectionEntity = entity,
                            Action = StreamingSectionAction.Load,
                            Reason = StreamingSectionCommandReason.FocusEnter,
                            Score = score
                        });
                        sectionState.ValueRW.Status = StreamingSectionStatus.QueuedLoad;
                    }
                }
                else if (!withinExit && (isLoadedOrPending || isUnloadPending))
                {
                    if (!isUnloadPending)
                    {
                        float score = bestDistance + desc.Priority * 5f + desc.EstimatedCost;
                        commands.Add(new StreamingSectionCommand
                        {
                            SectionEntity = entity,
                            Action = StreamingSectionAction.Unload,
                            Reason = StreamingSectionCommandReason.FocusExit,
                            Score = score
                        });
                        sectionState.ValueRW.Status = StreamingSectionStatus.QueuedUnload;
                    }
                }
            }

            focusList.Dispose();
        }
    }
}
