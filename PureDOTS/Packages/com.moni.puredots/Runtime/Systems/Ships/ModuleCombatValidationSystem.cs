using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Validates that module entities have required components for combat systems.
    /// Runs in development builds only to catch configuration errors early.
    /// Note: Not Burst-compiled because it uses UnityEngine.Debug for logging.
    /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CarrierModuleBootstrapSystem))]
    public partial struct ModuleCombatValidationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityLookup = state.GetEntityStorageInfoLookup();
            entityLookup.Update(ref state);

            var moduleLookup = state.GetComponentLookup<ShipModule>(true);
            var healthLookup = state.GetComponentLookup<ModuleHealth>(true);
            var positionLookup = state.GetComponentLookup<ModulePosition>(true);
            var priorityLookup = state.GetComponentLookup<ModuleTargetPriority>(true);
            var slotLookup = state.GetBufferLookup<CarrierModuleSlot>(true);

            moduleLookup.Update(ref state);
            healthLookup.Update(ref state);
            positionLookup.Update(ref state);
            priorityLookup.Update(ref state);
            slotLookup.Update(ref state);

            // Validate all ships with CarrierModuleSlot buffers
            foreach (var (slots, shipEntity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                // Check if using correct namespace (PureDOTS.Runtime.Ships.CarrierModuleSlot)
                // This is a compile-time check, but we validate at runtime that slots have InstalledModule
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    var moduleEntity = slot.InstalledModule;

                    if (moduleEntity == Entity.Null)
                    {
                        continue; // Empty slot is fine
                    }

                    if (!entityLookup.Exists(moduleEntity))
                    {
                        UnityEngine.Debug.LogWarning($"[ModuleCombatValidation] Module entity {moduleEntity} referenced in slot {i} of ship {shipEntity} does not exist.");
                        continue;
                    }

                    // Validate required components
                    if (!moduleLookup.HasComponent(moduleEntity))
                    {
                        UnityEngine.Debug.LogError($"[ModuleCombatValidation] Module {moduleEntity} on ship {shipEntity} missing ShipModule component. Combat systems will fail.");
                    }

                    if (!healthLookup.HasComponent(moduleEntity))
                    {
                        UnityEngine.Debug.LogError($"[ModuleCombatValidation] Module {moduleEntity} on ship {shipEntity} missing ModuleHealth component. Damage routing will fail.");
                    }
                    else
                    {
                        // Validate ModuleHealth structure (must be PureDOTS.Runtime.Ships.ModuleHealth with float fields)
                        var health = healthLookup[moduleEntity];
                        // Check that Health/MaxHealth are reasonable (not default 0 if module should have health)
                        if (health.MaxHealth <= 0f)
                        {
                            UnityEngine.Debug.LogWarning($"[ModuleCombatValidation] Module {moduleEntity} on ship {shipEntity} has invalid MaxHealth ({health.MaxHealth}). Should be > 0.");
                        }
                    }

                    if (!positionLookup.HasComponent(moduleEntity))
                    {
                        UnityEngine.Debug.LogWarning($"[ModuleCombatValidation] Module {moduleEntity} on ship {shipEntity} missing ModulePosition component. Hit detection may be inaccurate.");
                    }

                    if (!priorityLookup.HasComponent(moduleEntity))
                    {
                        UnityEngine.Debug.LogWarning($"[ModuleCombatValidation] Module {moduleEntity} on ship {shipEntity} missing ModuleTargetPriority component. Targeting will use default priorities.");
                    }
                }
            }
        }
    }
#endif
}

