using PureDOTS.Runtime.Combat;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Ensures exactly one HazardGridSingleton exists per world.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct HazardGridBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;

            if (SystemAPI.TryGetSingletonEntity<HazardGridSingleton>(out _))
            {
                state.Enabled = false;
                return;
            }

            var e = em.CreateEntity();
            em.AddComponentData(e, new HazardGridSingleton
            {
                GridEntity = Entity.Null
            });

            state.Enabled = false;
        }

        [BurstCompile] public void OnUpdate(ref SystemState state) { }
        [BurstCompile] public void OnDestroy(ref SystemState state) { }
    }
}


