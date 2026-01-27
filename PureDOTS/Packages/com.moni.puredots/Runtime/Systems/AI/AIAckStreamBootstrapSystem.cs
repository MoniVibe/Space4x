using PureDOTS.Runtime.AI;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// Ensures the AI acknowledgement stream exists.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct AIAckStreamBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<AIAckStreamTag>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<AIAckStreamTag>(entity);
                state.EntityManager.AddBuffer<AIAckEvent>(entity);
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }
    }
}


