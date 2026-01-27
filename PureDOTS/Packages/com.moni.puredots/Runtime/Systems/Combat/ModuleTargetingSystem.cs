using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Ships;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// System that handles module targeting selection and updates.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = false)]
    public partial struct ModuleTargetingSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<ShipModule> _moduleLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;
        private BufferLookup<CarrierModuleSlot> _slotBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityLookup = state.GetEntityStorageInfoLookup();
            _moduleLookup = state.GetComponentLookup<ShipModule>(true);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(true);
            _slotBufferLookup = state.GetBufferLookup<CarrierModuleSlot>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _slotBufferLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Update module targets for entities that have targeting components
            // This system can be extended to handle automatic target selection
            // For now, it validates existing targets

            foreach (var (moduleTarget, entity) in SystemAPI.Query<RefRW<ModuleTarget>>()
                .WithEntityAccess())
            {
                var target = moduleTarget.ValueRO;

                // Validate target still exists and is targetable
                if (target.TargetModule != Entity.Null)
                {
                    if (!_entityLookup.Exists(target.TargetModule))
                    {
                        // Target destroyed, clear
                        moduleTarget.ValueRW.TargetModule = Entity.Null;
                        moduleTarget.ValueRW.TargetShip = Entity.Null;
                    }
                    else if (!ModuleTargetingService.IsModuleTargetable(_entityLookup, _moduleLookup, _healthLookup, target.TargetModule))
                    {
                        // Target no longer targetable, clear
                        moduleTarget.ValueRW.TargetModule = Entity.Null;
                        moduleTarget.ValueRW.TargetShip = Entity.Null;
                    }
                }
            }
        }
    }
}

