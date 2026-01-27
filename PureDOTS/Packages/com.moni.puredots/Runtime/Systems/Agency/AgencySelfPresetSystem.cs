using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Agency
{
    /// <summary>
    /// Applies agency self presets before control claims resolve.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(AgencyControlClaimBridgeSystem))]
    public partial struct AgencySelfPresetSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindState>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<AgencySelfPreset>()
                .WithNone<AgencySelf>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (preset, entity) in SystemAPI.Query<RefRO<AgencySelfPreset>>().WithNone<AgencySelf>().WithEntityAccess())
            {
                ecb.AddComponent(entity, AgencySelfPresetUtility.Resolve(preset.ValueRO));
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
