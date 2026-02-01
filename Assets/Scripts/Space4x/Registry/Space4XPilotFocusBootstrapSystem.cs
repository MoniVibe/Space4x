using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Space4X.Runtime;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures pilots have focus components for tactical/weapon focus usage.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XPilotFocusBootstrapSystem : ISystem
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
                EnsurePilotFocus(em, pilotLink.ValueRO.Pilot, ref ecb);
            }

            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithNone<Prefab>().WithEntityAccess())
            {
                EnsurePilotFocus(em, pilotLink.ValueRO.Pilot, ref ecb);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void EnsurePilotFocus(EntityManager em, Entity pilot, ref EntityCommandBuffer ecb)
        {
            if (pilot == Entity.Null || !em.Exists(pilot) || em.HasComponent<Prefab>(pilot))
            {
                return;
            }

            if (!em.HasComponent<Space4XEntityFocus>(pilot))
            {
                ecb.AddComponent(pilot, Space4XEntityFocus.Default());
            }

            if (!em.HasComponent<OfficerFocusProfile>(pilot))
            {
                ecb.AddComponent(pilot, OfficerFocusProfile.Pilot());
            }
            else
            {
                var profile = em.GetComponentData<OfficerFocusProfile>(pilot);
                if (profile.IsOnDuty == 0)
                {
                    profile.IsOnDuty = 1;
                    ecb.SetComponent(pilot, profile);
                }
            }

            if (!em.HasComponent<Space4XFocusModifiers>(pilot))
            {
                ecb.AddComponent(pilot, Space4XFocusModifiers.Default());
            }

            if (!em.HasComponent<FocusGrowth>(pilot))
            {
                ecb.AddComponent(pilot, new FocusGrowth());
            }

            if (!em.HasComponent<FocusUsageTracking>(pilot))
            {
                ecb.AddComponent(pilot, new FocusUsageTracking());
            }

            if (!em.HasComponent<FocusPersonality>(pilot))
            {
                ecb.AddComponent(pilot, FocusPersonality.Disciplined());
            }

            if (!em.HasComponent<FocusBehaviorContext>(pilot))
            {
                ecb.AddComponent(pilot, new FocusBehaviorContext());
            }

            if (!em.HasBuffer<Space4XActiveFocusAbility>(pilot))
            {
                ecb.AddBuffer<Space4XActiveFocusAbility>(pilot);
            }

            if (!em.HasBuffer<FocusAchievement>(pilot))
            {
                ecb.AddBuffer<FocusAchievement>(pilot);
            }

            if (!em.HasBuffer<FocusExhaustionEvent>(pilot))
            {
                ecb.AddBuffer<FocusExhaustionEvent>(pilot);
            }
        }
    }
}
