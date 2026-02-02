using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Stats;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Transfers miner cargo directly into the assigned carrier hold when the vessel is near the carrier.
    /// This provides a deterministic headless-friendly "gather -> return -> dropoff" loop without relying on pickup entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    [UpdateBefore(typeof(MiningResourceSpawnSystem))]
    public partial struct CarrierDropoffSystem : ISystem
    {
        private const float DropoffDistance = 3.5f;
        private const float DropoffDistanceSq = DropoffDistance * DropoffDistance;
        private const float DropoffRatePerSecond = 250f;
        private const float DockingHoldDuration = 1.2f;

        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ResourceStorage> _storageLookup;
        private BufferLookup<CraftOperatorConsole> _operatorConsoleLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private BufferLookup<DepartmentStatsBuffer> _departmentStatsLookup;
        private ComponentLookup<CarrierDepartmentState> _departmentStateLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private FixedString64Bytes _roleLogisticsOfficer;
        private FixedString64Bytes _roleCaptain;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _storageLookup = state.GetBufferLookup<ResourceStorage>(false);
            _operatorConsoleLookup = state.GetBufferLookup<CraftOperatorConsole>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _departmentStatsLookup = state.GetBufferLookup<DepartmentStatsBuffer>(true);
            _departmentStateLookup = state.GetComponentLookup<CarrierDepartmentState>(true);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);

            _roleLogisticsOfficer = default;
            _roleLogisticsOfficer.Append('s');
            _roleLogisticsOfficer.Append('h');
            _roleLogisticsOfficer.Append('i');
            _roleLogisticsOfficer.Append('p');
            _roleLogisticsOfficer.Append('.');
            _roleLogisticsOfficer.Append('l');
            _roleLogisticsOfficer.Append('o');
            _roleLogisticsOfficer.Append('g');
            _roleLogisticsOfficer.Append('i');
            _roleLogisticsOfficer.Append('s');
            _roleLogisticsOfficer.Append('t');
            _roleLogisticsOfficer.Append('i');
            _roleLogisticsOfficer.Append('c');
            _roleLogisticsOfficer.Append('s');
            _roleLogisticsOfficer.Append('_');
            _roleLogisticsOfficer.Append('o');
            _roleLogisticsOfficer.Append('f');
            _roleLogisticsOfficer.Append('f');
            _roleLogisticsOfficer.Append('i');
            _roleLogisticsOfficer.Append('c');
            _roleLogisticsOfficer.Append('e');
            _roleLogisticsOfficer.Append('r');

            _roleCaptain = default;
            _roleCaptain.Append('s');
            _roleCaptain.Append('h');
            _roleCaptain.Append('i');
            _roleCaptain.Append('p');
            _roleCaptain.Append('.');
            _roleCaptain.Append('c');
            _roleCaptain.Append('a');
            _roleCaptain.Append('p');
            _roleCaptain.Append('t');
            _roleCaptain.Append('a');
            _roleCaptain.Append('i');
            _roleCaptain.Append('n');

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningVessel>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _storageLookup.Update(ref state);
            _operatorConsoleLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _crewSkillsLookup.Update(ref state);
            _departmentStatsLookup.Update(ref state);
            _departmentStateLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);

            var operatorTuning = CraftOperatorTuning.Default;
            if (SystemAPI.TryGetSingleton<CraftOperatorTuning>(out var operatorTuningSingleton))
            {
                operatorTuning = operatorTuningSingleton;
            }

            var deltaTime = timeState.FixedDeltaTime;
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            foreach (var (vessel, aiState, transform, entity) in SystemAPI
                         .Query<RefRW<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithAll<MiningState>() // Only the MiningOrder + MiningState pipeline uses this dropoff.
                         .WithEntityAccess())
            {
                var vesselValue = vessel.ValueRO;
                if (vesselValue.CarrierEntity == Entity.Null || vesselValue.CurrentCargo <= 0.01f)
                {
                    continue;
                }

                // Only drop off when returning (prevents dumping cargo at the mining site).
                if (aiState.ValueRO.CurrentState != VesselAIState.State.Returning)
                {
                    continue;
                }

                var carrierEntity = vesselValue.CarrierEntity;
                if (!_transformLookup.HasComponent(carrierEntity) || !_storageLookup.HasBuffer(carrierEntity))
                {
                    continue;
                }

                var carrierPos = _transformLookup[carrierEntity].Position;
                var distSq = math.lengthsq(transform.ValueRO.Position - carrierPos);
                if (distSq > DropoffDistanceSq)
                {
                    continue;
                }

                var logisticsMultiplier = ResolveLogisticsOpsMultiplier(entity, carrierEntity, operatorTuning);
                var maxTransfer = math.max(0f, DropoffRatePerSecond * deltaTime * logisticsMultiplier);
                var transferAmount = math.min(vesselValue.CurrentCargo, maxTransfer > 0f ? maxTransfer : vesselValue.CurrentCargo);
                if (transferAmount <= 0.0001f)
                {
                    continue;
                }

                var storage = _storageLookup[carrierEntity];
                var accepted = TransferToStorage(storage, vesselValue.CargoResourceType, transferAmount);
                if (accepted <= 0.0001f)
                {
                    continue;
                }

                vesselValue.CurrentCargo = math.max(0f, vesselValue.CurrentCargo - accepted);
                vessel.ValueRW = vesselValue;

                if (hasCommandLog)
                {
                    commandLog.Add(new MiningCommandLogEntry
                    {
                        Tick = timeState.Tick,
                        CommandType = MiningCommandType.Pickup,
                        SourceEntity = entity,
                        TargetEntity = carrierEntity,
                        ResourceType = vesselValue.CargoResourceType,
                        Amount = accepted,
                        Position = carrierPos
                    });
                }

                if (SystemAPI.TryGetSingletonRW<PlayerResources>(out var playerResources))
                {
                    playerResources.ValueRW.AddResource(vesselValue.CargoResourceType, accepted);
                }

                if (vesselValue.CurrentCargo <= 0.01f)
                {
                    // Reset for another mining cycle.
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Mining;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    aiState.ValueRW.TargetPosition = float3.zero;
                    aiState.ValueRW.StateTimer = 0f;
                    aiState.ValueRW.StateStartTick = timeState.Tick;

                    if (SystemAPI.HasComponent<MiningOrder>(entity))
                    {
                        var order = SystemAPI.GetComponentRW<MiningOrder>(entity).ValueRO;
                        order.Status = MiningOrderStatus.Pending;
                        order.TargetEntity = Entity.Null;
                        order.PreferredTarget = Entity.Null;
                        order.IssuedTick = timeState.Tick;
                        SystemAPI.GetComponentRW<MiningOrder>(entity).ValueRW = order;
                    }

                    if (SystemAPI.HasComponent<MiningState>(entity))
                    {
                        var miningState = SystemAPI.GetComponentRW<MiningState>(entity).ValueRO;
                        miningState.Phase = MiningPhase.Docking;
                        miningState.ActiveTarget = Entity.Null;
                        miningState.MiningTimer = 0f;
                        var logisticsSpeed = math.clamp(logisticsMultiplier, 0.75f, 1.35f);
                        miningState.PhaseTimer = DockingHoldDuration / logisticsSpeed;
                        SystemAPI.GetComponentRW<MiningState>(entity).ValueRW = miningState;
                    }
                }
            }
        }

        private static float TransferToStorage(DynamicBuffer<ResourceStorage> storage, ResourceType type, float amount)
        {
            var remaining = amount;
            for (var i = 0; i < storage.Length && remaining > 1e-4f; i++)
            {
                var slot = storage[i];
                if (slot.Type != type)
                {
                    continue;
                }

                remaining = slot.AddAmount(remaining);
                storage[i] = slot;
            }

            if (remaining > 1e-4f && storage.Length < 4)
            {
                var slot = ResourceStorage.Create(type);
                remaining = slot.AddAmount(remaining);
                storage.Add(slot);
            }

            return amount - remaining;
        }

        private float ResolveLogisticsOpsMultiplier(Entity miner, Entity carrier, in CraftOperatorTuning tuning)
        {
            var operatorSkill = 0.5f;
            var consoleQuality = 0.5f;
            var cohesion = 0.5f;
            var commandSkill = 0.5f;
            var haulingSkill = 0.5f;

            if (_crewSkillsLookup.HasComponent(miner))
            {
                haulingSkill = math.saturate(_crewSkillsLookup[miner].HaulingSkill);
            }

            Entity controller = Entity.Null;
            if (_operatorConsoleLookup.HasBuffer(miner))
            {
                var consoles = _operatorConsoleLookup[miner];
                for (int i = 0; i < consoles.Length; i++)
                {
                    var console = consoles[i];
                    if ((console.Domain & AgencyDomain.Logistics) == 0)
                    {
                        continue;
                    }

                    consoleQuality = math.saturate(console.ConsoleQuality);
                    controller = console.Controller;
                    break;
                }
            }

            if (controller == Entity.Null && carrier != Entity.Null)
            {
                controller = ResolveSeatOccupant(carrier, _roleLogisticsOfficer);
            }

            if (controller != Entity.Null && _statsLookup.HasComponent(controller))
            {
                operatorSkill = Space4XOperatorInterfaceUtility.ResolveOperatorSkill(AgencyDomain.Logistics, _statsLookup[controller], tuning);
            }

            if (carrier != Entity.Null)
            {
                cohesion = ResolveLogisticsCohesion(carrier);

                var captain = ResolveSeatOccupant(carrier, _roleCaptain);
                if (captain != Entity.Null && _statsLookup.HasComponent(captain))
                {
                    var stats = _statsLookup[captain];
                    commandSkill = math.saturate(stats.Command / 100f);
                }
            }

            var baseCoordination = math.saturate(operatorSkill * 0.55f + cohesion * 0.25f + commandSkill * 0.2f);
            var qualityCoordination = math.saturate(baseCoordination * math.lerp(0.85f, 1.15f, consoleQuality));
            var haulingBoost = math.lerp(0.9f, 1.1f, haulingSkill);
            var opsMultiplier = math.lerp(0.8f, 1.3f, qualityCoordination) * haulingBoost;
            return math.clamp(opsMultiplier, 0.75f, 1.35f);
        }

        private float ResolveLogisticsCohesion(Entity carrier)
        {
            if (_departmentStatsLookup.HasBuffer(carrier))
            {
                var buffer = _departmentStatsLookup[carrier];
                var logistics = -1f;
                var command = -1f;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var stats = buffer[i].Stats;
                    switch (stats.Type)
                    {
                        case DepartmentType.Logistics:
                            logistics = math.saturate((float)stats.Cohesion);
                            break;
                        case DepartmentType.Command:
                            command = math.saturate((float)stats.Cohesion);
                            break;
                    }
                }

                if (logistics >= 0f && command >= 0f)
                {
                    return math.saturate((logistics + command) * 0.5f);
                }

                if (logistics >= 0f)
                {
                    return logistics;
                }

                if (command >= 0f)
                {
                    return command;
                }
            }

            if (_departmentStateLookup.HasComponent(carrier))
            {
                return math.saturate((float)_departmentStateLookup[carrier].AverageCohesion);
            }

            return 0.5f;
        }

        private Entity ResolveSeatOccupant(Entity carrierEntity, FixedString64Bytes roleId)
        {
            if (!_seatRefLookup.HasBuffer(carrierEntity))
            {
                return Entity.Null;
            }

            var seats = _seatRefLookup[carrierEntity];
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var seat = _seatLookup[seatEntity];
                if (!seat.RoleId.Equals(roleId))
                {
                    continue;
                }

                if (_seatOccupantLookup.HasComponent(seatEntity))
                {
                    return _seatOccupantLookup[seatEntity].OccupantEntity;
                }

                return Entity.Null;
            }

            return Entity.Null;
        }
    }
}
