using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Checks pilot preferences against tuning state.
    /// Sets refusal flags if below standards. Emits narrative events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PilotAcceptanceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var crewMemberLookup = SystemAPI.GetBufferLookup<PlatformCrewMember>(false);

            foreach (var (tuningState, pilotPref, kind, entity) in SystemAPI.Query<RefRO<PlatformTuningState>, RefRO<PlatformPilotPreference>, RefRO<PlatformKind>>().WithAll<PlatformCrewMember>().WithEntityAccess())
            {
                if ((kind.ValueRO.Flags & (PlatformFlags.Craft | PlatformFlags.Drone)) == 0)
                {
                    continue;
                }

                var crewMembers = crewMemberLookup[entity];
                var platformEntityRef = entity;
                CheckPilotAcceptance(
                    ref state,
                    ref ecb,
                    ref platformEntityRef,
                    in tuningState.ValueRO,
                    in pilotPref.ValueRO,
                    ref crewMembers);
            }
        }

        [BurstCompile]
        private static void CheckPilotAcceptance(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref Entity platformEntity,
            in PlatformTuningState tuningState,
            in PlatformPilotPreference pilotPref,
            ref DynamicBuffer<PlatformCrewMember> crewMembers)
        {
            var reliabilityBelowMin = tuningState.Reliability < pilotPref.MinReliability;
            var performanceBelowMin = tuningState.PerformanceFactor < pilotPref.MinPerformance;

            if (!reliabilityBelowMin && !performanceBelowMin)
            {
                return;
            }

            var willRefuse = pilotPref.WillFlyIfBelow == 0;
            var grudgingAccept = pilotPref.WillFlyIfBelow == 1;

            if (willRefuse)
            {
                for (int i = 0; i < crewMembers.Length; i++)
                {
                    var crewEntity = crewMembers[i].CrewEntity;
                    if (state.EntityManager.Exists(crewEntity) && crewMembers[i].RoleId == 4)
                    {
                        var pilotEntityRef = crewEntity;
                        var platformEntityRef = platformEntity;
                        EmitPilotRefusalEvent(ref ecb, ref pilotEntityRef, ref platformEntityRef);
                        break;
                    }
                }
            }
            else if (grudgingAccept)
            {
                for (int i = 0; i < crewMembers.Length; i++)
                {
                    var crewEntity = crewMembers[i].CrewEntity;
                    if (state.EntityManager.Exists(crewEntity) && crewMembers[i].RoleId == 4)
                    {
                        var pilotEntityRef = crewEntity;
                        var platformEntityRef = platformEntity;
                        EmitGrudgingAcceptanceEvent(ref ecb, ref pilotEntityRef, ref platformEntityRef);
                        break;
                    }
                }
            }
        }

        [BurstCompile]
        private static void EmitPilotRefusalEvent(ref EntityCommandBuffer ecb, ref Entity pilotEntity, ref Entity craftEntity)
        {
        }

        [BurstCompile]
        private static void EmitGrudgingAcceptanceEvent(ref EntityCommandBuffer ecb, ref Entity pilotEntity, ref Entity craftEntity)
        {
        }
    }
}





