using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Applies ambient group pressure to individuals, biasing their behavior without fully controlling it.
    /// Runs at configurable frequency (e.g., daily/weekly).
    /// Does NOT overwrite traits - only applies slow drifts or modifies other components (morale, stress).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AmbientConditionsUpdateSystem))]
    public partial struct IndividualAmbientResponseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }

            // Skip if paused or rewinding
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process individuals with GroupMembership and MoralProfile
            foreach (var (membership, profile, entity) in SystemAPI.Query<
                RefRO<GroupMembership>,
                RefRW<MoralProfile>>().WithEntityAccess())
            {
                var groupEntity = membership.ValueRO.Group;
                if (groupEntity == Entity.Null)
                    continue;

                // Get ambient conditions from group
                if (!SystemAPI.HasComponent<AmbientGroupConditions>(groupEntity))
                    continue;

                var ambient = SystemAPI.GetComponent<AmbientGroupConditions>(groupEntity);
                var profileValue = profile.ValueRO;

                // Calculate misalignment and apply small adjustments
                // Example: if group AmbientCourage high but individual BoldCraven low → slight push toward boldness
                float courageMisalignment = ambient.AmbientCourage - ((profileValue.CravenBold + 100f) / 200f);
                float angerMisalignment = ambient.AmbientAnger - ((profileValue.VengefulForgiving + 100f) / 200f);
                float driveMisalignment = ambient.AmbientDrive - (profileValue.Initiative / 100f);
                float loyaltyMisalignment = ambient.ExpectationLoyalty - ((profileValue.CorruptPure + 100f) / 200f);

                // Apply small drift (very slow, only if misalignment is significant)
                const float driftRate = 0.01f; // 1% per update
                const float threshold = 0.1f; // Only drift if misalignment > 10%

                if (math.abs(courageMisalignment) > threshold)
                {
                    // Push toward group courage level
                    var currentBold = profileValue.CravenBold;
                    var targetBold = (ambient.AmbientCourage * 200f) - 100f;
                    var drift = (targetBold - currentBold) * driftRate;
                    profileValue.CravenBold = (sbyte)math.clamp(currentBold + drift, -100f, 100f);
                }

                if (math.abs(angerMisalignment) > threshold)
                {
                    var currentVengeful = profileValue.VengefulForgiving;
                    var targetVengeful = (ambient.AmbientAnger * 200f) - 100f;
                    var drift = (targetVengeful - currentVengeful) * driftRate;
                    profileValue.VengefulForgiving = (sbyte)math.clamp(currentVengeful + drift, -100f, 100f);
                }

                if (math.abs(driveMisalignment) > threshold)
                {
                    var currentInitiative = profileValue.Initiative;
                    var targetInitiative = ambient.AmbientDrive * 100f;
                    var drift = (targetInitiative - currentInitiative) * driftRate;
                    profileValue.Initiative = (byte)math.clamp(currentInitiative + drift, 0f, 100f);
                }

                // Apply morale/stress penalties for severe misalignment
                // This would modify VillagerNeeds or a stress component if present
                // For now, we just update the profile
                // Game-specific systems can read misalignment and apply penalties

                profile.ValueRW = profileValue;
            }
        }
    }
}
























