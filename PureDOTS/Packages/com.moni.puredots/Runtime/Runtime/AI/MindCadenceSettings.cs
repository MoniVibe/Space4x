using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Shared cadence settings for the Mind/Aggregate pillars. All values use tick counts.
    /// </summary>
    public struct MindCadenceSettings : IComponentData
    {
        /// <summary>Sensor sampling cadence (ticks). 1 = every tick.</summary>
        public int SensorCadenceTicks;

        /// <summary>Intent/planning cadence (ticks).</summary>
        public int EvaluationCadenceTicks;

        /// <summary>Command resolution cadence (ticks).</summary>
        public int ResolutionCadenceTicks;

        /// <summary>High-level aggregate cadence (ticks).</summary>
        public int AggregateCadenceTicks;

        /// <summary>Returns canonical defaults (run every tick).</summary>
        public static MindCadenceSettings CreateDefault()
        {
            return new MindCadenceSettings
            {
                SensorCadenceTicks = 1,
                EvaluationCadenceTicks = 1,
                ResolutionCadenceTicks = 1,
                AggregateCadenceTicks = 1
            };
        }
    }

    /// <summary>
    /// Ensures a MindCadenceSettings singleton exists with safe defaults so games can override when needed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct MindCadenceBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<MindCadenceSettings>())
            {
                state.Enabled = false;
                return;
            }

            var ecb = SystemAPI
                .GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var settings = MindCadenceSettings.CreateDefault();
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, settings);

            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}
