using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Infiltration;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Rewind;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Infiltration
{
    /// <summary>
    /// Organizations investigate suspicious agents, building evidence over time.
    /// Active investigations boost detection chance.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SuspicionTrackingSystem))]
    public partial struct InvestigationSystem : ISystem
    {
        private ComponentLookup<InfiltrationState> _infiltrationLookup;
        private ComponentLookup<CounterIntelligence> _counterIntelLookup;
        private ComponentLookup<CoverIdentity> _coverLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _infiltrationLookup = state.GetComponentLookup<InfiltrationState>(true);
            _counterIntelLookup = state.GetComponentLookup<CounterIntelligence>(true);
            _coverLookup = state.GetComponentLookup<CoverIdentity>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Update every 30 ticks (throttled)
            if (timeState.Tick % 30 != 0)
            {
                return;
            }

            _infiltrationLookup.Update(ref state);
            _counterIntelLookup.Update(ref state);
            _coverLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build map: TargetOrganization -> SpyEntity (O(N) instead of O(N²))
            var orgToSpies = new NativeParallelMultiHashMap<Entity, Entity>(64, Allocator.Temp);
            foreach (var (infiltration, spyEntity) in SystemAPI.Query<RefRO<InfiltrationState>>().WithEntityAccess())
            {
                if (infiltration.ValueRO.IsExposed != 0 || infiltration.ValueRO.IsExtracting != 0)
                {
                    continue;
                }

                var targetOrg = infiltration.ValueRO.TargetOrganization;
                if (targetOrg != Entity.Null)
                {
                    orgToSpies.Add(targetOrg, spyEntity);
                }
            }

            // Find all organizations with counter-intelligence
            var orgsWithCounterIntel = new NativeList<Entity>(64, Allocator.Temp);
            foreach (var (counterIntel, orgEntity) in SystemAPI.Query<RefRO<CounterIntelligence>>().WithEntityAccess())
            {
                orgsWithCounterIntel.Add(orgEntity);
            }

            // For each organization, look up suspicious infiltrators from map
            foreach (var orgEntity in orgsWithCounterIntel)
            {
                var counterIntel = _counterIntelLookup[orgEntity];

                // Look up infiltrators targeting this organization (O(1) lookup)
                if (orgToSpies.TryGetFirstValue(orgEntity, out var spyEntity, out var iterator))
                {
                    do
                    {
                        if (!_infiltrationLookup.HasComponent(spyEntity))
                        {
                            continue;
                        }

                        var infiltration = _infiltrationLookup[spyEntity];

                        // Start investigation if suspicion is high enough and no active investigation
                        if (infiltration.SuspicionLevel > 0.5f)
                        {
                            bool hasActiveInvestigation = SystemAPI.HasComponent<Investigation>(orgEntity) &&
                                                          SystemAPI.GetComponent<Investigation>(orgEntity).IsActive != 0;

                            if (!hasActiveInvestigation)
                            {
                                // Create or update investigation
                                if (!SystemAPI.HasComponent<Investigation>(orgEntity))
                                {
                                    ecb.AddComponent(orgEntity, new Investigation
                                    {
                                        SuspectEntity = spyEntity,
                                        InvestigationProgress = 0f,
                                        Evidence = 0f,
                                        InvestigationStartTick = timeState.Tick,
                                        StartTick = timeState.Tick,
                                        Status = InvestigationStatus.Suspicious,
                                        IsActive = 1
                                    });
                                }
                                else
                                {
                                    var investigation = SystemAPI.GetComponentRW<Investigation>(orgEntity);
                                    investigation.ValueRW.SuspectEntity = spyEntity;
                                    investigation.ValueRW.InvestigationStartTick = timeState.Tick;
                                    investigation.ValueRW.StartTick = timeState.Tick;
                                    investigation.ValueRW.Status = InvestigationStatus.UnderInvestigation;
                                    investigation.ValueRW.IsActive = 1;
                                }
                            }
                        }
                    } while (orgToSpies.TryGetNextValue(out spyEntity, ref iterator));
                }

                // Update existing investigations
                if (SystemAPI.HasComponent<Investigation>(orgEntity))
                {
                    var investigation = SystemAPI.GetComponentRW<Investigation>(orgEntity);
                    if (investigation.ValueRO.IsActive != 0 && investigation.ValueRO.Status != InvestigationStatus.Confirmed)
                    {
                        var suspectEntity = investigation.ValueRO.SuspectEntity;
                        if (suspectEntity != Entity.Null && _infiltrationLookup.HasComponent(suspectEntity))
                        {
                            var infiltration = _infiltrationLookup[suspectEntity];
                            
                            // Calculate evidence accumulation rate
                            float coverStrength = infiltration.CoverStrength;
                            if (_coverLookup.HasComponent(suspectEntity))
                            {
                                var cover = _coverLookup[suspectEntity];
                                coverStrength = math.max(coverStrength, cover.Credibility);
                            }

                            // Evidence accumulates faster with low cover or high suspicion
                            float evidenceRate = counterIntel.InvestigationPower * 0.01f;
                            evidenceRate *= (1f - coverStrength * 0.5f); // Low cover = faster evidence
                            evidenceRate *= (1f + infiltration.SuspicionLevel * 0.3f); // High suspicion = faster evidence

                            investigation.ValueRW.Evidence = math.min(1f, investigation.ValueRO.Evidence + evidenceRate);
                            investigation.ValueRW.InvestigationProgress = investigation.ValueRO.Evidence;

                            // Confirm investigation when progress reaches 1.0
                            if (investigation.ValueRO.InvestigationProgress >= 1.0f)
                            {
                                investigation.ValueRW.Status = InvestigationStatus.Confirmed;
                                // Confirmed investigations boost detection (handled in InfiltrationDetectionSystem)
                            }
                        }
                        else
                        {
                            // Suspect no longer exists or no longer infiltrating - clear investigation
                            investigation.ValueRW.Status = InvestigationStatus.Cleared;
                            investigation.ValueRW.IsActive = 0;
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            orgToSpies.Dispose();
            orgsWithCounterIntel.Dispose();
        }
    }
}

