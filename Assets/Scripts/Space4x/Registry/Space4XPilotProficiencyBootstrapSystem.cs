using PureDOTS.Runtime.Movement;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures pilots have proficiency and practice-time components.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XPilotProficiencyBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<VesselPilotLink>>().WithNone<Prefab>().WithEntityAccess())
            {
                EnsurePilot(em, pilotLink.ValueRO.Pilot, ref ecb);
            }

            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithNone<Prefab>().WithEntityAccess())
            {
                EnsurePilot(em, pilotLink.ValueRO.Pilot, ref ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void EnsurePilot(EntityManager em, Entity pilot, ref EntityCommandBuffer ecb)
        {
            if (pilot == Entity.Null || !em.Exists(pilot) || em.HasComponent<Prefab>(pilot))
            {
                return;
            }

            if (!em.HasComponent<PilotProficiency>(pilot))
            {
                ecb.AddComponent(pilot, new PilotProficiency
                {
                    ControlMult = 1f,
                    TurnRateMult = 1f,
                    EnergyMult = 1f,
                    Jitter = 0f,
                    ReactionSec = 0.5f
                });
            }

            if (!em.HasComponent<Space4XPilotPracticeTime>(pilot))
            {
                ecb.AddComponent(pilot, new Space4XPilotPracticeTime());
            }
        }
    }
}
