using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Persistence;
using PureDOTS.Runtime.Time;
using Unity.Entities;
using Space4XSaveSlotRequest = Space4X.Runtime.SaveSlotRequest;

namespace Space4x.Persistence
{
    /// <summary>
    /// Bridges Space4x save slot requests into PureDOTS persistence commands.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSaveSlotBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.EnableSpace4x ||
                !scenario.IsInitialized)
            {
                return;
            }

            uint tick = SystemAPI.GetSingleton<TimeState>().Tick;

            foreach (var (request, entity) in SystemAPI.Query<RefRO<Space4XSaveSlotRequest>>().WithEntityAccess())
            {
                var action = (SaveSlotAction)request.ValueRO.Action;

                var commandEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(commandEntity, new PureDOTS.Runtime.Persistence.SaveSlotRequest
                {
                    SlotIndex = request.ValueRO.SlotIndex,
                    Action = action,
                    Flags = SaveSlotRequestFlags.None,
                    RequestedTick = tick,
                    Label = new Unity.Collections.FixedString64Bytes("space4x")
                });

                state.EntityManager.RemoveComponent<Space4XSaveSlotRequest>(entity);
            }
        }
    }
}
