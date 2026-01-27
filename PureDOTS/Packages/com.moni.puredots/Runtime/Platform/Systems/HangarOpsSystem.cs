using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Manages launch/recall operations for craft/drones/swarms from carriers.
    /// Rate-limited by LaunchRate/RecoveryRate. Handles orphaned craft.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HangarOpsSystem : ISystem
    {
        private EntityQuery _craftWithAssignmentQuery;
        private BufferLookup<HangarAssignment> _assignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _craftWithAssignmentQuery = SystemAPI
                .QueryBuilder()
                .WithAll<PlatformKind, HangarAssignment>()
                .Build();

            state.RequireForUpdate(_craftWithAssignmentQuery);

            _assignmentLookup = state.GetBufferLookup<HangarAssignment>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            _assignmentLookup.Update(ref state);
            var hangarBayLookup = SystemAPI.GetBufferLookup<HangarBay>(false);
            var assignmentLookup = SystemAPI.GetBufferLookup<HangarAssignment>(false);

            foreach (var (kind, entity) in SystemAPI.Query<RefRO<PlatformKind>>().WithAll<HangarBay>().WithAll<HangarAssignment>().WithEntityAccess())
            {
                if ((kind.ValueRO.Flags & PlatformFlags.IsCarrier) == 0)
                {
                    continue;
                }

                var hangarBays = hangarBayLookup[entity];
                var assignments = assignmentLookup[entity];
                var carrierEntityRef = entity;
                ProcessHangarOperations(
                    ref state,
                    ref ecb,
                    ref carrierEntityRef,
                    ref hangarBays,
                    ref assignments,
                    timeState.Tick);
            }

            CheckOrphanedCraft(ref state, ref ecb);
        }

        [BurstCompile]
        private static void ProcessHangarOperations(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref Entity carrierEntity,
            ref DynamicBuffer<HangarBay> hangarBays,
            ref DynamicBuffer<HangarAssignment> assignments,
            uint currentTick)
        {
            for (int bayIndex = 0; bayIndex < hangarBays.Length; bayIndex++)
            {
                var bay = hangarBays[bayIndex];
                
                if (bay.OccupiedSlots >= bay.Capacity)
                {
                    continue;
                }

                var availableSlots = bay.Capacity - bay.OccupiedSlots - bay.ReservedSlots;
                if (availableSlots <= 0)
                {
                    continue;
                }

                var launchCount = 0;
                var maxLaunches = (int)math.floor(bay.LaunchRate);

                var assignmentsToRemove = new NativeList<int>(8, Allocator.Temp);
                for (int i = 0; i < assignments.Length && launchCount < maxLaunches && launchCount < availableSlots; i++)
                {
                    var assignment = assignments[i];
                    
                    if (assignment.HangarIndex != bayIndex)
                    {
                        continue;
                    }

                    if (!state.EntityManager.Exists(assignment.SubPlatform))
                    {
                        assignmentsToRemove.Add(i);
                        continue;
                    }

                    if (state.EntityManager.HasComponent<PlatformKind>(assignment.SubPlatform))
                    {
                        var subKind = state.EntityManager.GetComponentData<PlatformKind>(assignment.SubPlatform);
                        if ((subKind.Flags & PlatformFlags.Craft) != 0 || (subKind.Flags & PlatformFlags.Drone) != 0)
                        {
                            var subPlatformRef = assignment.SubPlatform;
                            var carrierRef = carrierEntity;
                            LaunchSubPlatform(ref state, ref ecb, ref subPlatformRef, ref carrierRef);
                            assignmentsToRemove.Add(i);
                            launchCount++;
                            bay.OccupiedSlots--;
                        }
                    }
                }

                for (int i = assignmentsToRemove.Length - 1; i >= 0; i--)
                {
                    assignments.RemoveAt(assignmentsToRemove[i]);
                }
                assignmentsToRemove.Dispose();

                if (launchCount > 0)
                {
                    hangarBays[bayIndex] = bay;
                }
            }
        }

        [BurstCompile]
        private static void LaunchSubPlatform(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref Entity subPlatform,
            ref Entity carrier)
        {
            if (state.EntityManager.HasComponent<HangarAssignment>(subPlatform))
            {
                ecb.RemoveComponent<DynamicBuffer<HangarAssignment>>(subPlatform);
            }
        }

        [BurstCompile]
        private void CheckOrphanedCraft(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            if (_craftWithAssignmentQuery.IsEmptyIgnoreFilter)
                return;

            var em = state.EntityManager;

            var entities = _craftWithAssignmentQuery.ToEntityArray(Allocator.Temp);
            var kinds = _craftWithAssignmentQuery.ToComponentDataArray<PlatformKind>(Allocator.Temp);
            for (int idx = 0; idx < entities.Length; idx++)
            {
                var entity = entities[idx];
                var kind = kinds[idx];

                if ((kind.Flags & (PlatformFlags.Craft | PlatformFlags.Drone)) == 0)
                {
                    continue;
                }

                if (!_assignmentLookup.HasBuffer(entity))
                {
                    continue;
                }

                var assignmentBuffer = _assignmentLookup[entity];
                if (assignmentBuffer.Length == 0)
                {
                    continue;
                }
                var assignment = assignmentBuffer[0];
                
                if (!em.Exists(assignment.SubPlatform))
                {
                    kind.Flags |= PlatformFlags.IsDisposable;
                    ecb.SetComponent(entity, kind);
                }
            }

            entities.Dispose();
            kinds.Dispose();
        }
    }
}

