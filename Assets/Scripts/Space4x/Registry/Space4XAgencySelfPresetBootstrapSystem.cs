using PureDOTS.Runtime.Agency;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds agency self presets for tools (craft) and sentient individuals.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XAgencySelfPresetBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<Carrier>>().WithNone<AgencySelfPreset>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new AgencySelfPreset { Kind = AgencySelfPresetKind.Tool });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithNone<AgencySelfPreset>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new AgencySelfPreset { Kind = AgencySelfPresetKind.Tool });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithNone<AgencySelfPreset>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new AgencySelfPreset { Kind = AgencySelfPresetKind.Tool });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<IndividualStats>>().WithNone<AgencySelfPreset>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new AgencySelfPreset { Kind = AgencySelfPresetKind.Sentient });
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
