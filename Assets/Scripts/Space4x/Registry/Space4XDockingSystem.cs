using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Validates and processes docking requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XDockingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            var commandLookup = state.GetComponentLookup<CommandLoad>(false);
            var dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
            dockingLookup.Update(ref state);
            commandLookup.Update(ref state);
            dockedBufferLookup.Update(ref state);

            // Process docking requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<DockingRequest>>()
                .WithNone<DockedTag>()
                .WithEntityAccess())
            {
                ProcessDockingRequest(
                    in entity,
                    request.ValueRO,
                    ref dockingLookup,
                    ref commandLookup,
                    ref dockedBufferLookup,
                    currentTick,
                    ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static void ProcessDockingRequest(
            in Entity requestingEntity,
            in DockingRequest request,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            uint currentTick,
            ref EntityCommandBuffer ecb)
        {
            if (request.TargetCarrier == Entity.Null)
            {
                return;
            }

            // Check if carrier has docking capacity component
            if (!dockingLookup.HasComponent(request.TargetCarrier))
            {
                return;
            }

            var docking = dockingLookup[request.TargetCarrier];

            // Check if slot is available
            if (!docking.HasSlotAvailable(request.RequiredSlot))
            {
                return; // No slot available, keep request pending
            }

            // Calculate command cost
            int commandCost = DockingUtility.GetCommandPointCost(request.RequiredSlot);

            // Check command load if carrier has it
            if (commandLookup.HasComponent(request.TargetCarrier))
            {
                var commandLoad = commandLookup[request.TargetCarrier];
                // Allow docking even if overloaded, but apply penalties in other systems
                commandLoad.CurrentCommandPoints += commandCost;
                commandLookup[request.TargetCarrier] = commandLoad;
            }

            // Update docking capacity
            switch (request.RequiredSlot)
            {
                case DockingSlotType.SmallCraft:
                    docking.CurrentSmallCraft++;
                    break;
                case DockingSlotType.MediumCraft:
                    docking.CurrentMediumCraft++;
                    break;
                case DockingSlotType.LargeCraft:
                    docking.CurrentLargeCraft++;
                    break;
                case DockingSlotType.ExternalMooring:
                    docking.CurrentExternalMooring++;
                    break;
                case DockingSlotType.Utility:
                    docking.CurrentUtility++;
                    break;
            }
            dockingLookup[request.TargetCarrier] = docking;

            // Add to docked entities buffer
            if (dockedBufferLookup.HasBuffer(request.TargetCarrier))
            {
                var dockedBuffer = dockedBufferLookup[request.TargetCarrier];
                dockedBuffer.Add(new DockedEntity
                {
                    Entity = requestingEntity,
                    SlotType = request.RequiredSlot,
                    DockedTick = currentTick,
                    CommandPointCost = (byte)commandCost
                });
            }

            // Add DockedTag to requesting entity
            ecb.AddComponent(requestingEntity, new DockedTag
            {
                CarrierEntity = request.TargetCarrier,
                SlotIndex = (byte)(docking.TotalDocked - 1)
            });

            // Remove docking request
            ecb.RemoveComponent<DockingRequest>(requestingEntity);
        }
    }

    /// <summary>
    /// Handles undocking and updates capacity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingSystem))]
    public partial struct Space4XUndockingSystem : ISystem
    {
        private BufferLookup<DockedEntity> _dockedBufferLookup;
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<CommandLoad> _commandLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
            _dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            _commandLookup = state.GetComponentLookup<CommandLoad>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _dockedBufferLookup.Update(ref state);
            _dockingLookup.Update(ref state);
            _commandLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process entities that have both DockedTag and are in certain AI states that indicate undocking
            // This is a simplified check - in practice, undocking would be triggered by orders
            foreach (var (docked, aiState, entity) in SystemAPI.Query<RefRO<DockedTag>, RefRO<VesselAIState>>()
                .WithEntityAccess())
            {
                // If vessel has a target and is in MovingToTarget state, it's undocking
                if (aiState.ValueRO.CurrentState == VesselAIState.State.MovingToTarget &&
                    aiState.ValueRO.TargetEntity != Entity.Null &&
                    aiState.ValueRO.TargetEntity != docked.ValueRO.CarrierEntity)
                {
                    // Queue undocking
                    ProcessUndocking(
                        in entity,
                        docked.ValueRO,
                        ref ecb,
                        ref _dockedBufferLookup,
                        ref _dockingLookup,
                        ref _commandLookup);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static void ProcessUndocking(
            in Entity undockingEntity,
            in DockedTag docked,
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup)
        {
            if (docked.CarrierEntity == Entity.Null)
            {
                ecb.RemoveComponent<DockedTag>(undockingEntity);
                return;
            }

            // Update carrier's docked entities buffer
            if (dockedBufferLookup.HasBuffer(docked.CarrierEntity))
            {
                var dockedBuffer = dockedBufferLookup[docked.CarrierEntity];
                for (int i = dockedBuffer.Length - 1; i >= 0; i--)
                {
                    if (dockedBuffer[i].Entity == undockingEntity)
                    {
                        var dockedEntry = dockedBuffer[i];

                        // Update docking capacity
                        if (dockingLookup.HasComponent(docked.CarrierEntity))
                        {
                            var docking = dockingLookup.GetRefRW(docked.CarrierEntity);
                            switch (dockedEntry.SlotType)
                            {
                                case DockingSlotType.SmallCraft:
                                    docking.ValueRW.CurrentSmallCraft = (byte)math.max(0, docking.ValueRO.CurrentSmallCraft - 1);
                                    break;
                                case DockingSlotType.MediumCraft:
                                    docking.ValueRW.CurrentMediumCraft = (byte)math.max(0, docking.ValueRO.CurrentMediumCraft - 1);
                                    break;
                                case DockingSlotType.LargeCraft:
                                    docking.ValueRW.CurrentLargeCraft = (byte)math.max(0, docking.ValueRO.CurrentLargeCraft - 1);
                                    break;
                                case DockingSlotType.ExternalMooring:
                                    docking.ValueRW.CurrentExternalMooring = (byte)math.max(0, docking.ValueRO.CurrentExternalMooring - 1);
                                    break;
                                case DockingSlotType.Utility:
                                    docking.ValueRW.CurrentUtility = (byte)math.max(0, docking.ValueRO.CurrentUtility - 1);
                                    break;
                            }
                        }

                        // Update command load
                        if (commandLookup.HasComponent(docked.CarrierEntity))
                        {
                            var commandLoad = commandLookup.GetRefRW(docked.CarrierEntity);
                            commandLoad.ValueRW.CurrentCommandPoints = math.max(0, commandLoad.ValueRO.CurrentCommandPoints - dockedEntry.CommandPointCost);
                        }

                        dockedBuffer.RemoveAt(i);
                        break;
                    }
                }
            }

            ecb.RemoveComponent<DockedTag>(undockingEntity);
        }
    }

    /// <summary>
    /// Applies overcrowding and command overload penalties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XUndockingSystem))]
    public partial struct Space4XDockingPenaltySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
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

            // Apply penalties to carriers with overcrowding or command overload
            foreach (var (crewCapacity, morale, entity) in
                SystemAPI.Query<RefRO<CrewCapacity>, RefRW<MoraleState>>()
                    .WithEntityAccess())
            {
                float penalty = DockingUtility.GetOvercrowdingPenalty(crewCapacity.ValueRO);
                if (penalty > 0f)
                {
                    // Apply morale penalty
                    float currentMorale = (float)morale.ValueRO.Current;
                    currentMorale -= penalty * 0.01f; // Slow drain
                    morale.ValueRW.Current = (half)math.clamp(currentMorale, -1f, 1f);
                }
            }

            // Apply command overload penalties to efficiency
            foreach (var (commandLoad, carrierState, entity) in
                SystemAPI.Query<RefRO<CommandLoad>, RefRW<CarrierDepartmentState>>()
                    .WithEntityAccess())
            {
                float penalty = DockingUtility.GetCommandOverloadPenalty(commandLoad.ValueRO);
                if (penalty > 0f)
                {
                    float efficiency = (float)carrierState.ValueRO.OverallEfficiency;
                    efficiency = math.max(0.5f, efficiency - penalty);
                    carrierState.ValueRW.OverallEfficiency = (half)efficiency;
                }
            }
        }
    }

    /// <summary>
    /// Emits docking telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingPenaltySystem))]
    public partial struct Space4XDockingTelemetrySystem : ISystem
    {
        private EntityQuery _dockingQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _dockingQuery = SystemAPI.QueryBuilder()
                .WithAll<DockingCapacity>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            int carrierCount = _dockingQuery.CalculateEntityCount();
            int totalDocked = 0;
            int totalCapacity = 0;
            int overcrowdedCount = 0;
            int commandOverloadCount = 0;

            foreach (var docking in SystemAPI.Query<RefRO<DockingCapacity>>())
            {
                totalDocked += docking.ValueRO.TotalDocked;
                totalCapacity += docking.ValueRO.TotalCapacity;
            }

            foreach (var crew in SystemAPI.Query<RefRO<CrewCapacity>>())
            {
                if (crew.ValueRO.IsOvercrowded)
                {
                    overcrowdedCount++;
                }
            }

            foreach (var command in SystemAPI.Query<RefRO<CommandLoad>>())
            {
                if (command.ValueRO.IsOverloaded)
                {
                    commandOverloadCount++;
                }
            }

            float avgUtilization = totalCapacity > 0 ? (float)totalDocked / totalCapacity : 0f;

            buffer.AddMetric("space4x.docking.carriers", carrierCount);
            buffer.AddMetric("space4x.docking.totalDocked", totalDocked);
            buffer.AddMetric("space4x.docking.totalCapacity", totalCapacity);
            buffer.AddMetric("space4x.docking.avgUtilization", avgUtilization, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.docking.overcrowded", overcrowdedCount);
            buffer.AddMetric("space4x.docking.commandOverloaded", commandOverloadCount);
        }
    }
}

