using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XPresentationRegistryIdentitySystem : ISystem
    {
        private RegistryId _carrierId;
        private RegistryId _minerId;
        private RegistryId _asteroidId;
        private RegistryId _projectileId;
        private RegistryId _fleetImpostorId;
        private RegistryId _individualId;
        private RegistryId _strikeCraftId;
        private RegistryId _resourcePickupId;
        private RegistryId _ghostTetherId;

        public void OnCreate(ref SystemState state)
        {
            _carrierId = RegistryId.FromString("space4x.carrier");
            _minerId = RegistryId.FromString("space4x.miner");
            _asteroidId = RegistryId.FromString("space4x.asteroid");
            _projectileId = RegistryId.FromString("space4x.projectile");
            _fleetImpostorId = RegistryId.FromString("space4x.fleet_impostor");
            _individualId = RegistryId.FromString("space4x.individual");
            _strikeCraftId = RegistryId.FromString("space4x.strike_craft");
            _resourcePickupId = RegistryId.FromString("space4x.resource_pickup");
            _ghostTetherId = RegistryId.FromString("space4x.ghost_tether");

            state.RequireForUpdate<PresentationContentRegistryReference>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            AddIdentity(ref ecb, _carrierId, ref state, ComponentType.ReadOnly<CarrierPresentationTag>());
            AddIdentity(ref ecb, _minerId, ref state, ComponentType.ReadOnly<CraftPresentationTag>());
            AddIdentity(ref ecb, _asteroidId, ref state, ComponentType.ReadOnly<AsteroidPresentationTag>());
            AddIdentity(ref ecb, _projectileId, ref state, ComponentType.ReadOnly<ProjectilePresentationTag>());
            AddIdentity(ref ecb, _fleetImpostorId, ref state, ComponentType.ReadOnly<FleetImpostorTag>());
            AddIdentity(ref ecb, _individualId, ref state, ComponentType.ReadOnly<IndividualPresentationTag>());
            AddIdentity(ref ecb, _strikeCraftId, ref state, ComponentType.ReadOnly<StrikeCraftPresentationTag>());
            AddIdentity(ref ecb, _resourcePickupId, ref state, ComponentType.ReadOnly<ResourcePickupPresentationTag>());
            AddIdentity(ref ecb, _ghostTetherId, ref state, ComponentType.ReadOnly<GhostTetherTag>());

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void AddIdentity(ref EntityCommandBuffer ecb, RegistryId id, ref SystemState state, ComponentType marker)
        {
            if (!id.IsValid)
            {
                return;
            }

            using var query = state.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { marker },
                None = new[] { ComponentType.ReadOnly<RegistryIdentity>() }
            });

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                ecb.AddComponent(entities[i], new RegistryIdentity { Id = id });
            }
        }
    }
}
