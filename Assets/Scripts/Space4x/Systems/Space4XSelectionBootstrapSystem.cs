using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems
{
    /// <summary>
    /// Tags core Space4X entities (carriers, mining vessels, asteroids) as selectable and assigns owner id.
    /// Runs once per entity; skips headless.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSelectionBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Carrier>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
                return;

            var selectableLookup = state.GetComponentLookup<SelectableTag>(false);
            var ownerLookup = state.GetComponentLookup<SelectionOwner>(false);

            foreach (var (carrier, entity) in SystemAPI.Query<Carrier>().WithEntityAccess())
            {
                EnsureSelectable(ref state, entity, carrier.AffiliationEntity, selectableLookup, ownerLookup);
            }

            foreach (var (vessel, entity) in SystemAPI.Query<MiningVessel>().WithEntityAccess())
            {
                EnsureSelectable(ref state, entity, vessel.CarrierEntity, selectableLookup, ownerLookup);
            }

            foreach (var (asteroid, entity) in SystemAPI.Query<Asteroid>().WithEntityAccess())
            {
                EnsureSelectable(ref state, entity, Entity.Null, selectableLookup, ownerLookup);
            }
        }

        private void EnsureSelectable(
            ref SystemState state,
            Entity entity,
            Entity affiliationEntity,
            ComponentLookup<SelectableTag> selectableLookup,
            ComponentLookup<SelectionOwner> ownerLookup)
        {
            if (!selectableLookup.HasComponent(entity))
            {
                state.EntityManager.AddComponent<SelectableTag>(entity);
            }

            if (!ownerLookup.HasComponent(entity))
            {
                // For now, use player 0; could map affiliation to player later.
                state.EntityManager.AddComponentData(entity, new SelectionOwner { PlayerId = 0 });
            }
        }
    }
}
