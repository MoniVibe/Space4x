using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Ships;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// System that updates ship capabilities based on module states.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModuleDamageRouterSystem))]
    public partial struct CapabilityDisableSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<ShipModule> _moduleLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;
        private BufferLookup<CarrierModuleSlot> _slotBufferLookup;
        private ComponentLookup<CapabilityState> _capabilityStateLookup;
        private ComponentLookup<CapabilityEffectiveness> _effectivenessLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityLookup = state.GetEntityStorageInfoLookup();
            _moduleLookup = state.GetComponentLookup<ShipModule>(true);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(true);
            _slotBufferLookup = state.GetBufferLookup<CarrierModuleSlot>(true);
            _capabilityStateLookup = state.GetComponentLookup<CapabilityState>(false);
            _effectivenessLookup = state.GetComponentLookup<CapabilityEffectiveness>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _slotBufferLookup.Update(ref state);
            _capabilityStateLookup.Update(ref state);
            _effectivenessLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Update capabilities for all ships with module slots
            foreach (var (slots, shipEntity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>()
                .WithEntityAccess())
            {
                CapabilityDisableService.UpdateCapabilitiesFromModules(
                    _entityLookup,
                    _slotBufferLookup,
                    _moduleLookup,
                    _healthLookup,
                    _capabilityStateLookup,
                    _effectivenessLookup,
                    ecb,
                    shipEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

