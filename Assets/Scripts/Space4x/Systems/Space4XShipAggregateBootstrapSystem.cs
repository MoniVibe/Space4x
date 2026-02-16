using PureDOTS.Runtime.Ships;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XShipAggregateBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Carrier>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<Carrier>>().WithNone<ShipAggregate>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new ShipAggregate
                {
                    Role = ShipRole.Carrier
                });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithNone<ShipAggregate>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new ShipAggregate
                {
                    Role = ShipRole.Support
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
