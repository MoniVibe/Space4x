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
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<PlayerFlagshipTag> _playerFlagshipLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Carrier>();
            _selectableLookup = state.GetComponentLookup<SelectableTag>(true);
            _ownerLookup = state.GetComponentLookup<SelectionOwner>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _playerFlagshipLookup = state.GetComponentLookup<PlayerFlagshipTag>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
                return;

            _selectableLookup.Update(ref state);
            _ownerLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _playerFlagshipLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var playerAffiliationEntity = ResolvePlayerAffiliationEntity(ref state);

            foreach (var (carrier, entity) in SystemAPI.Query<Carrier>().WithEntityAccess())
            {
                EnsureSelectable(entity, carrier.AffiliationEntity, playerAffiliationEntity, ecb);
            }

            foreach (var (vessel, entity) in SystemAPI.Query<MiningVessel>().WithEntityAccess())
            {
                var affiliationEntity = ResolveMiningVesselAffiliation(entity, vessel);
                EnsureSelectable(entity, affiliationEntity, playerAffiliationEntity, ecb);
            }

            foreach (var (asteroid, entity) in SystemAPI.Query<Asteroid>().WithEntityAccess())
            {
                EnsureSelectable(entity, Entity.Null, playerAffiliationEntity, ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void EnsureSelectable(
            Entity entity,
            Entity affiliationEntity,
            Entity playerAffiliationEntity,
            EntityCommandBuffer ecb)
        {
            if (!_selectableLookup.HasComponent(entity))
            {
                ecb.AddComponent<SelectableTag>(entity);
            }

            byte ownerId = ResolveSelectionOwner(entity, affiliationEntity, playerAffiliationEntity);
            if (!_ownerLookup.HasComponent(entity))
            {
                ecb.AddComponent(entity, new SelectionOwner { PlayerId = ownerId });
            }
            else
            {
                var currentOwner = _ownerLookup[entity];
                if (currentOwner.PlayerId != ownerId)
                {
                    currentOwner.PlayerId = ownerId;
                    ecb.SetComponent(entity, currentOwner);
                }
            }
        }

        private Entity ResolvePlayerAffiliationEntity(ref SystemState state)
        {
            foreach (var carrier in SystemAPI.Query<Carrier>().WithAll<PlayerFlagshipTag>())
            {
                if (carrier.AffiliationEntity != Entity.Null)
                {
                    return carrier.AffiliationEntity;
                }
            }

            foreach (var affiliations in SystemAPI.Query<DynamicBuffer<AffiliationTag>>().WithAll<PlayerFlagshipTag>())
            {
                if (affiliations.Length > 0 && affiliations[0].Target != Entity.Null)
                {
                    return affiliations[0].Target;
                }
            }

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (faction.ValueRO.Type == FactionType.Player)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private Entity ResolveMiningVesselAffiliation(Entity vesselEntity, in MiningVessel vessel)
        {
            if (vessel.CarrierEntity != Entity.Null && _carrierLookup.HasComponent(vessel.CarrierEntity))
            {
                return _carrierLookup[vessel.CarrierEntity].AffiliationEntity;
            }

            if (_affiliationLookup.HasBuffer(vesselEntity))
            {
                var affiliations = _affiliationLookup[vesselEntity];
                if (affiliations.Length > 0)
                {
                    return affiliations[0].Target;
                }
            }

            return Entity.Null;
        }

        private byte ResolveSelectionOwner(Entity entity, Entity affiliationEntity, Entity playerAffiliationEntity)
        {
            if (_playerFlagshipLookup.HasComponent(entity))
            {
                return 0;
            }

            if (playerAffiliationEntity == Entity.Null)
            {
                if (_ownerLookup.HasComponent(entity))
                {
                    return _ownerLookup[entity].PlayerId;
                }

                return affiliationEntity == Entity.Null ? (byte)1 : (byte)0;
            }

            return affiliationEntity != Entity.Null && affiliationEntity == playerAffiliationEntity
                ? (byte)0
                : (byte)1;
        }
    }
}
