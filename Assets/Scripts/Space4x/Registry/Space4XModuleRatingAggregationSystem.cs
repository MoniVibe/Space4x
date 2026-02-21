using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates module ratings (offense/defense/utility) and power balance from installed modules.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XModuleStatAggregationSystem))]
    public partial struct Space4XModuleRatingAggregationSystem : ISystem
    {
        private ComponentLookup<ModuleHealth> _healthLookup;
        private ComponentLookup<ModuleTypeId> _moduleTypeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _healthLookup = state.GetComponentLookup<ModuleHealth>(true);
            _moduleTypeLookup = state.GetComponentLookup<ModuleTypeId>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _moduleTypeLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (slots, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithEntityAccess())
            {
                int offenseRating = 0;
                int defenseRating = 0;
                int utilityRating = 0;
                float powerBalanceMW = 0f;
                int degradedCount = 0;
                int repairingCount = 0;
                int refittingCount = 0;

                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.CurrentModule == Entity.Null || slot.State != ModuleSlotState.Active)
                    {
                        if (slot.State == ModuleSlotState.Installing || slot.State == ModuleSlotState.Removing)
                        {
                            refittingCount++;
                        }
                        continue;
                    }

                    if (!_moduleTypeLookup.HasComponent(slot.CurrentModule))
                    {
                        continue;
                    }

                    var moduleId = _moduleTypeLookup[slot.CurrentModule].Value;
                    if (!ModuleCatalogUtility.TryGetModuleSpec(ref state, moduleId, out var spec))
                    {
                        continue;
                    }

                    var efficiency = 1f;
                    if (_healthLookup.HasComponent(slot.CurrentModule))
                    {
                        var health = _healthLookup[slot.CurrentModule];
                        efficiency = math.clamp(health.CurrentHealth / math.max(0.01f, health.MaxHealth), 0f, 1f);
                        if (efficiency < 0.95f)
                        {
                            degradedCount++;
                        }
                        if (health.CurrentHealth < health.MaxHealth)
                        {
                            repairingCount++;
                        }
                    }

                    offenseRating += (int)(spec.OffenseRating * efficiency);
                    defenseRating += (int)(spec.DefenseRating * efficiency);
                    utilityRating += (int)(spec.UtilityRating * efficiency);
                    powerBalanceMW += spec.PowerDrawMW * efficiency;
                }

                var aggregate = state.EntityManager.HasComponent<ModuleRatingAggregate>(entity)
                    ? state.EntityManager.GetComponentData<ModuleRatingAggregate>(entity)
                    : default;

                aggregate.OffenseRating = (byte)math.clamp(offenseRating, 0, 255);
                aggregate.DefenseRating = (byte)math.clamp(defenseRating, 0, 255);
                aggregate.UtilityRating = (byte)math.clamp(utilityRating, 0, 255);
                aggregate.PowerBalanceMW = powerBalanceMW;
                aggregate.DegradedModuleCount = (byte)math.clamp(degradedCount, 0, 255);
                aggregate.RepairingModuleCount = (byte)math.clamp(repairingCount, 0, 255);
                aggregate.RefittingModuleCount = (byte)math.clamp(refittingCount, 0, 255);

                if (!state.EntityManager.HasComponent<ModuleRatingAggregate>(entity))
                {
                    ecb.AddComponent(entity, aggregate);
                }
                else
                {
                    state.EntityManager.SetComponentData(entity, aggregate);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    public struct ModuleRatingAggregate : IComponentData
    {
        public byte OffenseRating;
        public byte DefenseRating;
        public byte UtilityRating;
        public float PowerBalanceMW;
        public byte DegradedModuleCount;
        public byte RepairingModuleCount;
        public byte RefittingModuleCount;
    }
}
