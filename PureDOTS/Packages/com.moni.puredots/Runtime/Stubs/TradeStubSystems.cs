// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Trade
{
    [BurstCompile]
    public partial struct MerchantTradeStubSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) { }
        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (inventory, intent) in SystemAPI.Query<RefRW<MerchantInventory>, RefRO<TradeIntent>>())
            {
                var inv = inventory.ValueRW;
                var action = intent.ValueRO.Action;

                if (action == 1)
                {
                    inv.CurrentMass = math.min(inv.Capacity, inv.CurrentMass + deltaTime);
                }
                else if (action == 2)
                {
                    inv.CurrentMass = math.max(0f, inv.CurrentMass - deltaTime);
                }

                inventory.ValueRW = inv;
            }
        }
    }
}
