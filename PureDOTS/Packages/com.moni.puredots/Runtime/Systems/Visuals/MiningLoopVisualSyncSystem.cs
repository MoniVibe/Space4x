using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Visuals;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Visuals
{
    /// <summary>
    /// Generates visual spawn requests for villager mining loops so the presentation layer can keep visuals in sync with DOTS data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    public partial struct MiningLoopVisualSyncSystem : ISystem
    {
        private ComponentLookup<VillagerJobProgress> _progressLookup;
        private ComponentLookup<VillagerJobTicket> _ticketLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiningVisualManifest>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _progressLookup = state.GetComponentLookup<VillagerJobProgress>(true);
            _ticketLookup = state.GetComponentLookup<VillagerJobTicket>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _progressLookup.Update(ref state);
            _ticketLookup.Update(ref state);

            var manifestEntity = SystemAPI.GetSingletonEntity<MiningVisualManifest>();
            var manifestRW = SystemAPI.GetComponentRW<MiningVisualManifest>(manifestEntity);
            var manifestSnapshot = manifestRW.ValueRO;

            var requests = state.EntityManager.GetBuffer<MiningVisualRequest>(manifestEntity);
            requests.Clear();

            var stats = new VillagerVisualStats();
            var spawnCount = 0;
            const int maxVisuals = 72;

            foreach (var (job, transform, entity) in SystemAPI.Query<RefRO<VillagerJob>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (job.ValueRO.Type != VillagerJob.JobType.Gatherer)
                {
                    continue;
                }

                if (job.ValueRO.Phase is VillagerJob.JobPhase.Idle or VillagerJob.JobPhase.Completed or VillagerJob.JobPhase.Interrupted)
                {
                    continue;
                }

                stats.ActiveMinerCount++;

                float delivered = 0f;
                float gathered = 0f;
                if (_progressLookup.HasComponent(entity))
                {
                    var progress = _progressLookup[entity];
                    delivered = progress.Delivered;
                    gathered = progress.Gathered;
                }

                stats.DeliveredCumulative += delivered;

                if (spawnCount >= maxVisuals)
                {
                    continue;
                }

                float reserved = 0f;
                if (_ticketLookup.HasComponent(entity))
                {
                    reserved = _ticketLookup[entity].ReservedUnits;
                }

                var gatherProgress = reserved > 0f ? math.saturate(gathered / reserved) : 0.5f;
                var productivityScale = math.clamp(job.ValueRO.Productivity, 0.2f, 1.5f);
                var baseScale = math.max(0.2f, gatherProgress) * productivityScale;

                var position = transform.ValueRO.Position;
                position.z += (spawnCount % 8) * 1.2f;
                position.x += (spawnCount / 8) * 1.5f;

                requests.Add(new MiningVisualRequest
                {
                    VisualType = MiningVisualType.Villager,
                    SourceEntity = entity,
                    Position = position,
                    BaseScale = baseScale
                });

                spawnCount++;
            }

            var deltaSeconds = 0f;
            if (manifestSnapshot.LastSyncTick != 0 && timeState.Tick > manifestSnapshot.LastSyncTick)
            {
                deltaSeconds = (timeState.Tick - manifestSnapshot.LastSyncTick) * timeState.FixedDeltaTime;
            }

            var villagerThroughput = 0f;
            if (deltaSeconds > 0f)
            {
                var deliveredDelta = math.max(0f, stats.DeliveredCumulative - manifestSnapshot.VillagerDeliveredCumulative);
                villagerThroughput = deliveredDelta / deltaSeconds * 60f;
            }

            manifestSnapshot.VillagerNodeCount = stats.ActiveMinerCount;
            manifestSnapshot.VillagerThroughput = villagerThroughput;
            manifestSnapshot.VillagerDeliveredCumulative = stats.DeliveredCumulative;
            manifestSnapshot.LastSyncTick = timeState.Tick;

            manifestRW.ValueRW = manifestSnapshot;
        }

        private struct VillagerVisualStats
        {
            public int ActiveMinerCount;
            public float DeliveredCumulative;
        }
    }
}

