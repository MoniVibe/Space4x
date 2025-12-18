using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems
{
    /// <summary>
    /// Tags core Space4X entities (carriers, mining vessels, asteroids) as selectable and assigns owner id.
    /// Runs once per entity; skips headless.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSelectionBootstrapSystem : ISystem
    {
        private ComponentLookup<SelectableTag> _selectableLookup;
        private ComponentLookup<SelectionOwner> _ownerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Carrier>();
            _selectableLookup = state.GetComponentLookup<SelectableTag>(true);
            _ownerLookup = state.GetComponentLookup<SelectionOwner>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
                return;

            _selectableLookup.Update(ref state);
            _ownerLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (carrier, entity) in SystemAPI.Query<Carrier>().WithEntityAccess())
            {
                EnsureSelectable(entity, carrier.AffiliationEntity, ecb);
            }

            foreach (var (vessel, entity) in SystemAPI.Query<MiningVessel>().WithEntityAccess())
            {
                EnsureSelectable(entity, vessel.CarrierEntity, ecb);
            }

            foreach (var (asteroid, entity) in SystemAPI.Query<Asteroid>().WithEntityAccess())
            {
                EnsureSelectable(entity, Entity.Null, ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void EnsureSelectable(
            Entity entity,
            Entity affiliationEntity,
            EntityCommandBuffer ecb)
        {
            if (!_selectableLookup.HasComponent(entity))
            {
                ecb.AddComponent<SelectableTag>(entity);
            }

            if (!_ownerLookup.HasComponent(entity))
            {
                // For now, use player 0; could map affiliation to player later.
                ecb.AddComponent(entity, new SelectionOwner { PlayerId = 0 });
            }
        }
    }
}
