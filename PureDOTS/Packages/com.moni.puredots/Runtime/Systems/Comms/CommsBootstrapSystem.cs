using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Comms
{
    /// <summary>
    /// Ensures comms stream/settings exist (authoring can override settings later).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct CommsBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<CommsSettings>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, CommsSettings.Default);
            }

            if (!SystemAPI.HasSingleton<CommsMessageStreamTag>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<CommsMessageStreamTag>(entity);
                state.EntityManager.AddBuffer<CommsMessage>(entity);
            }

            if (!SystemAPI.HasSingleton<CommsDeliveryDiagnostics>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new CommsDeliveryDiagnostics());
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }
    }
}

