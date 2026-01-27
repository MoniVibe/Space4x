using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Reads hover target components and outputs HandAffordances singleton.
    /// Runs after HandRaycastSystem to detect what actions are available.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandRaycastSystem))]
    public partial struct HandAffordanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandHover>();
            state.RequireForUpdate<HandInputFrame>();
            
            // Ensure HandAffordances singleton exists
            if (!SystemAPI.TryGetSingletonEntity<HandAffordances>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new HandAffordances());
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hover = SystemAPI.GetSingleton<HandHover>();
            var input = SystemAPI.GetSingleton<HandInputFrame>();
            var policy = new HandPickupPolicy
            {
                AutoPickDynamicPhysics = 0,
                EnableWorldGrab = 0,
                DebugWorldGrabAny = 0,
                WorldGrabRequiresTag = 1
            };
            if (SystemAPI.TryGetSingleton(out HandPickupPolicy policyValue))
            {
                policy = policyValue;
            }
            var affordances = new HandAffordances
            {
                Flags = HandAffordanceFlags.None,
                TargetEntity = hover.TargetEntity,
                ResourceTypeIndex = 0
            };

            if (hover.TargetEntity == Entity.Null || !state.EntityManager.Exists(hover.TargetEntity))
            {
                SystemAPI.SetSingleton(affordances);
                return;
            }

            var entityManager = state.EntityManager;
            var targetEntity = hover.TargetEntity;

            bool neverPickable = entityManager.HasComponent<NeverPickableTag>(targetEntity);
            // Check for Pickable
            bool hasPickable = entityManager.HasComponent<PickableTag>(targetEntity) ||
                entityManager.HasComponent<Pickable>(targetEntity);
            bool hasPhysicsVelocity = entityManager.HasComponent<PhysicsVelocity>(targetEntity);
            bool hasWorldTag = entityManager.HasComponent<WorldManipulableTag>(targetEntity);
            bool worldGrabActive = policy.EnableWorldGrab != 0 && input.CtrlHeld && input.ShiftHeld;
            bool autoPickDynamic = policy.AutoPickDynamicPhysics != 0 && hasPhysicsVelocity;
            bool worldGrabAllowed = worldGrabActive && !neverPickable &&
                (policy.DebugWorldGrabAny != 0 || policy.WorldGrabRequiresTag == 0 || hasWorldTag);

            if (!neverPickable && (hasPickable || autoPickDynamic || worldGrabAllowed))
            {
                affordances.Flags |= HandAffordanceFlags.CanPickUp;
            }

            // Check for SiphonSource
            if (entityManager.HasComponent<SiphonSource>(targetEntity))
            {
                var siphonSource = entityManager.GetComponentData<SiphonSource>(targetEntity);
                affordances.Flags |= HandAffordanceFlags.CanSiphon;
                affordances.ResourceTypeIndex = siphonSource.ResourceTypeIndex;
            }

            // Check for dump targets
            if (entityManager.HasComponent<DumpTargetStorehouse>(targetEntity))
            {
                affordances.Flags |= HandAffordanceFlags.CanDumpStorehouse;
            }

            if (entityManager.HasComponent<DumpTargetConstruction>(targetEntity))
            {
                affordances.Flags |= HandAffordanceFlags.CanDumpConstruction;
            }

            if (entityManager.HasComponent<DumpTargetGround>(targetEntity))
            {
                affordances.Flags |= HandAffordanceFlags.CanDumpGround;
            }

            // Check for MiracleSurface
            if (entityManager.HasComponent<MiracleSurface>(targetEntity))
            {
                affordances.Flags |= HandAffordanceFlags.CanCastMiracle;
            }

            SystemAPI.SetSingleton(affordances);
        }
    }
}

