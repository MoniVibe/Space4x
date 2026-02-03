using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using Space4X.Migration;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Profile.ProfileMutationSystem))]
    [BurstCompile]
    public partial struct Space4XProfileMutationPreSyncSystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<AlignmentTriplet>>()
                         .WithNone<PureDOTS.Runtime.Individual.AlignmentTriplet>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, AlignmentMigrationHelper.ToUnified(alignment.ValueRO));
            }

            foreach (var (outlookBuffer, entity) in SystemAPI.Query<DynamicBuffer<StanceEntry>>()
                         .WithNone<PureDOTS.Runtime.Alignment.OutlookEntry>()
                         .WithEntityAccess())
            {
                var target = ecb.AddBuffer<PureDOTS.Runtime.Alignment.OutlookEntry>(entity);
                for (int i = 0; i < outlookBuffer.Length; i++)
                {
                    var entry = outlookBuffer[i];
                    target.Add(new PureDOTS.Runtime.Alignment.OutlookEntry
                    {
                        StanceId = (PureDOTS.Runtime.Alignment.Outlook)entry.StanceId,
                        Weight = entry.Weight
                    });
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Profile.ProfileMutationSystem))]
    [BurstCompile]
    public partial struct Space4XProfileMutationPostSyncSystem : ISystem
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
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Individual.AlignmentTriplet>>()
                         .WithAll<ProfileActionAccumulator, AlignmentTriplet>()
                         .WithEntityAccess())
            {
                state.EntityManager.SetComponentData(entity, AlignmentMigrationHelper.FromUnified(alignment.ValueRO));
            }

            foreach (var (outlookBuffer, entity) in SystemAPI.Query<DynamicBuffer<PureDOTS.Runtime.Alignment.OutlookEntry>>()
                         .WithAll<ProfileActionAccumulator, StanceEntry>()
                         .WithEntityAccess())
            {
                var target = state.EntityManager.GetBuffer<StanceEntry>(entity);
                target.Clear();
                for (int i = 0; i < outlookBuffer.Length; i++)
                {
                    var entry = outlookBuffer[i];
                    target.Add(new StanceEntry
                    {
                        StanceId = (StanceId)entry.StanceId,
                        Weight = entry.Weight
                    });
                }
            }
        }
    }
}

