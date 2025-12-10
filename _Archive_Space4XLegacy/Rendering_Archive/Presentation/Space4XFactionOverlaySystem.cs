using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    /// <summary>
    /// System that updates faction overlay data for entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCarrierPresentationSystem))]
    public partial struct Space4XFactionOverlaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierPresentationTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new UpdateFactionOverlayJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CarrierPresentationTag))]
        private partial struct UpdateFactionOverlayJob : IJobEntity
        {
            public void Execute(
                ref FactionOverlayData overlayData,
                in Space4XFleet fleet,
                in FactionColor factionColor,
                in PresentationLOD lod)
            {
                if (lod.Level == PresentationLODLevel.Hidden)
                {
                    return;
                }

                // Extract faction ID from fleet (would need AffiliationTag buffer lookup)
                // For now, use fleet TaskForce as proxy
                overlayData.FactionId = fleet.TaskForce;
                overlayData.ControlStrength = 1f; // Full control for now
                overlayData.IsPlayerControlled = fleet.TaskForce == 0; // Assume TaskForce 0 is player

                // Faction color is already set in FactionColor component
                // This system just tracks overlay data for UI/visualization purposes
            }
        }
    }
}

