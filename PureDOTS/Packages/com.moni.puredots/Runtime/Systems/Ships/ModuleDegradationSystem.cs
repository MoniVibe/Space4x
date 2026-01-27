using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Applies degradation to active modules and enqueues repair tickets when they fall below thresholds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModuleStatAggregationSystem))]
    public partial struct ModuleDegradationSystem : ISystem
    {
        private BufferLookup<ModuleRepairTicket> _repairLookup;
        private ComponentLookup<CarrierOwner> _ownerLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleHealth>();
            state.RequireForUpdate<TimeState>();
            _repairLookup = state.GetBufferLookup<ModuleRepairTicket>(false);
            _ownerLookup = state.GetComponentLookup<CarrierOwner>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            _repairLookup.Update(ref state);
            _ownerLookup.Update(ref state);

            foreach (var (healthRef, entity) in SystemAPI.Query<RefRW<ModuleHealth>>().WithEntityAccess())
            {
                var opState = SystemAPI.HasComponent<ModuleOperationalState>(entity)
                    ? SystemAPI.GetComponent<ModuleOperationalState>(entity)
                    : default;

                var health = healthRef.ValueRO;
                ModuleMaintenanceUtility.ApplyDegradation(ref health, opState, timeState.Tick);
                healthRef.ValueRW = health;

                if (health.State == ModuleHealthState.Nominal || (health.Flags & ModuleHealthFlags.PendingRepairQueue) != 0)
                {
                    continue;
                }

                if (!_ownerLookup.HasComponent(entity))
                {
                    continue;
                }

                var owner = _ownerLookup[entity];
                if (!_repairLookup.HasBuffer(owner.Carrier))
                {
                    continue;
                }

                var queue = _repairLookup[owner.Carrier];
                queue.Add(new ModuleRepairTicket
                {
                    Module = entity,
                    Kind = DetermineRepairKind(health),
                    Priority = health.State == ModuleHealthState.Failed ? (byte)2 : (byte)1,
                    Severity = ModuleMaintenanceUtility.CalculateSeverity(health),
                    RequestedTick = timeState.Tick
                });

                health.Flags |= ModuleHealthFlags.PendingRepairQueue;
                healthRef.ValueRW = health;
            }
        }

        private static ModuleRepairKind DetermineRepairKind(in ModuleHealth health)
        {
            if (health.State == ModuleHealthState.Failed || (health.Flags & ModuleHealthFlags.RequiresStation) != 0)
            {
                return ModuleRepairKind.Station;
            }

            return ModuleRepairKind.Field;
        }
    }
}
