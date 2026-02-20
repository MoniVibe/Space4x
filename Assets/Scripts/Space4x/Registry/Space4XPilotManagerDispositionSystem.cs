using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Applies managerial directive/trust offsets onto pilot behavior while preserving profile baseline.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XVesselMovementAISystem))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XStrikeCraftBehaviorSystem))]
    public partial struct Space4XPilotManagerDispositionSystem : ISystem
    {
        private ComponentLookup<BehaviorDisposition> _behaviorLookup;
        private ComponentLookup<MoraleState> _moraleLookup;
        private ComponentLookup<Space4XPilotDirective> _directiveLookup;
        private ComponentLookup<Space4XPilotTrust> _trustLookup;
        private ComponentLookup<Space4XPilotBehaviorBaseline> _baselineLookup;
        private ComponentLookup<Space4XPilotBehaviorRuntime> _runtimeLookup;
        private ComponentLookup<Space4XPilotManagedTag> _managedLookup;
        private ComponentLookup<VillagerIntentDrive> _intentDriveLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _behaviorLookup = state.GetComponentLookup<BehaviorDisposition>(false);
            _moraleLookup = state.GetComponentLookup<MoraleState>(true);
            _directiveLookup = state.GetComponentLookup<Space4XPilotDirective>(true);
            _trustLookup = state.GetComponentLookup<Space4XPilotTrust>(true);
            _baselineLookup = state.GetComponentLookup<Space4XPilotBehaviorBaseline>(true);
            _runtimeLookup = state.GetComponentLookup<Space4XPilotBehaviorRuntime>(false);
            _managedLookup = state.GetComponentLookup<Space4XPilotManagedTag>(true);
            _intentDriveLookup = state.GetComponentLookup<VillagerIntentDrive>(true);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _behaviorLookup.Update(ref state);
            _moraleLookup.Update(ref state);
            _directiveLookup.Update(ref state);
            _trustLookup.Update(ref state);
            _baselineLookup.Update(ref state);
            _runtimeLookup.Update(ref state);
            _managedLookup.Update(ref state);
            _intentDriveLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var config = Space4XPilotManagerConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XPilotManagerConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var uniquePilots = new NativeParallelHashSet<Entity>(128, Allocator.Temp);
            var pilotCandidates = new NativeList<Entity>(128, Allocator.Temp);
            foreach (var pilotLink in SystemAPI.Query<RefRO<StrikeCraftPilotLink>>().WithNone<Prefab>())
            {
                TryAddPilotCandidate(pilotLink.ValueRO.Pilot, ref _entityInfoLookup, ref uniquePilots, ref pilotCandidates);
            }

            foreach (var (pilotLink, _) in SystemAPI.Query<RefRO<VesselPilotLink>, RefRO<MiningVessel>>().WithNone<Prefab>())
            {
                TryAddPilotCandidate(pilotLink.ValueRO.Pilot, ref _entityInfoLookup, ref uniquePilots, ref pilotCandidates);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var hasStructuralChanges = false;
            for (int i = 0; i < pilotCandidates.Length; i++)
            {
                var pilot = pilotCandidates[i];
                var hasBehavior = _behaviorLookup.HasComponent(pilot);
                var baselineDisposition = hasBehavior ? _behaviorLookup[pilot] : BehaviorDisposition.Default;

                if (!hasBehavior)
                {
                    ecb.AddComponent(pilot, baselineDisposition);
                    hasStructuralChanges = true;
                }

                if (!_directiveLookup.HasComponent(pilot))
                {
                    ecb.AddComponent(pilot, Space4XPilotDirective.Neutral);
                    hasStructuralChanges = true;
                }

                if (!_trustLookup.HasComponent(pilot))
                {
                    ecb.AddComponent(pilot, Space4XPilotTrust.Neutral);
                    hasStructuralChanges = true;
                }

                if (!_baselineLookup.HasComponent(pilot))
                {
                    ecb.AddComponent(pilot, new Space4XPilotBehaviorBaseline
                    {
                        Value = baselineDisposition,
                        CapturedTick = time.Tick
                    });
                    hasStructuralChanges = true;
                }

                if (!_runtimeLookup.HasComponent(pilot))
                {
                    ecb.AddComponent(pilot, new Space4XPilotBehaviorRuntime
                    {
                        Effective = baselineDisposition,
                        DirectivePressure = 0f,
                        TrustPressure = 0f,
                        LastUpdatedTick = time.Tick
                    });
                    hasStructuralChanges = true;
                }

                if (!_managedLookup.HasComponent(pilot))
                {
                    ecb.AddComponent<Space4XPilotManagedTag>(pilot);
                    hasStructuralChanges = true;
                }
            }

            if (hasStructuralChanges)
            {
                ecb.Playback(state.EntityManager);
                _behaviorLookup.Update(ref state);
                _moraleLookup.Update(ref state);
                _directiveLookup.Update(ref state);
                _trustLookup.Update(ref state);
                _baselineLookup.Update(ref state);
                _runtimeLookup.Update(ref state);
                _managedLookup.Update(ref state);
            }
            ecb.Dispose();

            for (int i = 0; i < pilotCandidates.Length; i++)
            {
                var pilot = pilotCandidates[i];
                if (!_behaviorLookup.HasComponent(pilot) ||
                    !_baselineLookup.HasComponent(pilot) ||
                    !_directiveLookup.HasComponent(pilot) ||
                    !_trustLookup.HasComponent(pilot) ||
                    !_runtimeLookup.HasComponent(pilot))
                {
                    continue;
                }

                var baseline = _baselineLookup[pilot];
                var directive = _directiveLookup[pilot];
                var trust = _trustLookup[pilot];
                var morale = _moraleLookup.HasComponent(pilot) ? (float)_moraleLookup[pilot].Current : 0f;
                var hasIntentDrive = _intentDriveLookup.HasComponent(pilot);
                var intentDrive = hasIntentDrive ? _intentDriveLookup[pilot] : default;

                var effective = ResolveEffectiveDisposition(
                    in baseline.Value,
                    in directive,
                    in trust,
                    morale,
                    hasIntentDrive,
                    in intentDrive,
                    in config,
                    out var directivePressure,
                    out var trustPressure);

                _behaviorLookup[pilot] = effective;
                _runtimeLookup[pilot] = new Space4XPilotBehaviorRuntime
                {
                    Effective = effective,
                    DirectivePressure = directivePressure,
                    TrustPressure = trustPressure,
                    LastUpdatedTick = time.Tick
                };
            }

            uniquePilots.Dispose();
            pilotCandidates.Dispose();
        }

        private static void TryAddPilotCandidate(
            Entity pilot,
            ref EntityStorageInfoLookup entityInfoLookup,
            ref NativeParallelHashSet<Entity> uniquePilots,
            ref NativeList<Entity> pilotCandidates)
        {
            if (pilot == Entity.Null || !entityInfoLookup.Exists(pilot))
            {
                return;
            }

            if (uniquePilots.Add(pilot))
            {
                pilotCandidates.Add(pilot);
            }
        }

        private static BehaviorDisposition ResolveEffectiveDisposition(
            in BehaviorDisposition baseline,
            in Space4XPilotDirective directive,
            in Space4XPilotTrust trust,
            float morale,
            bool hasIntentDrive,
            in VillagerIntentDrive intentDrive,
            in Space4XPilotManagerConfig config,
            out float directivePressure,
            out float trustPressure)
        {
            var aggressionBias = math.clamp((float)directive.AggressionBias, -1f, 1f);
            var cautionBias = math.clamp((float)directive.CautionBias, -1f, 1f);
            var complianceBias = math.clamp((float)directive.ComplianceBias, -1f, 1f);
            var formationBias = math.clamp((float)directive.FormationBias, -1f, 1f);
            var riskBias = math.clamp((float)directive.RiskBias, -1f, 1f);
            var patienceBias = math.clamp((float)directive.PatienceBias, -1f, 1f);

            var commandTrust = math.clamp((float)trust.CommandTrust, -1f, 1f);
            var crewTrust = math.clamp((float)trust.CrewTrust, -1f, 1f);
            var trustDeficit = math.saturate(-commandTrust);
            var trustSurplus = math.saturate(commandTrust);
            var moraleSigned = math.clamp(morale, -1f, 1f);

            directivePressure = math.saturate(
                (math.abs(aggressionBias) +
                 math.abs(cautionBias) +
                 math.abs(complianceBias) +
                 math.abs(formationBias) +
                 math.abs(riskBias) +
                 math.abs(patienceBias)) / 6f);
            trustPressure = math.saturate((math.abs(commandTrust) + math.abs(crewTrust)) * 0.5f);

            var effective = baseline;
            effective.Aggression = math.saturate(
                baseline.Aggression +
                aggressionBias * config.DirectiveWeight +
                trustDeficit * config.LowTrustAggressionWeight +
                moraleSigned * config.MoraleWeight);
            effective.Caution = math.saturate(
                baseline.Caution +
                cautionBias * config.DirectiveWeight +
                trustDeficit * config.LowTrustCautionWeight -
                moraleSigned * config.MoraleWeight * 0.8f);
            effective.Compliance = math.saturate(
                baseline.Compliance +
                complianceBias * config.DirectiveWeight +
                commandTrust * config.TrustComplianceWeight);
            effective.FormationAdherence = math.saturate(
                baseline.FormationAdherence +
                formationBias * config.DirectiveWeight +
                commandTrust * config.TrustFormationWeight +
                crewTrust * config.TrustFormationWeight * 0.5f);
            effective.RiskTolerance = math.saturate(
                baseline.RiskTolerance +
                riskBias * config.DirectiveWeight +
                trustDeficit * config.LowTrustRiskWeight -
                cautionBias * config.DirectiveWeight * 0.2f);
            effective.Patience = math.saturate(
                baseline.Patience +
                patienceBias * config.DirectiveWeight +
                trustSurplus * config.TrustPatienceWeight +
                moraleSigned * config.MoraleWeight * 0.6f);

            if (hasIntentDrive)
            {
                var selfPressure = math.saturate(intentDrive.SelfPreservationPressure);
                var kinPressure = math.saturate(intentDrive.KinshipPressure);
                var loyaltyPressure = math.saturate(intentDrive.LoyaltyPressure);
                var sharedPressure = math.max(selfPressure, math.max(kinPressure, loyaltyPressure));

                // Shared intent-drive offsets layer onto Space4X pilot directives/trust.
                effective.Aggression = math.saturate(effective.Aggression - selfPressure * 0.22f - loyaltyPressure * 0.08f);
                effective.Caution = math.saturate(effective.Caution + selfPressure * 0.30f + kinPressure * 0.12f);
                effective.Compliance = math.saturate(effective.Compliance + loyaltyPressure * 0.22f + selfPressure * 0.05f);
                effective.FormationAdherence = math.saturate(effective.FormationAdherence + kinPressure * 0.16f + loyaltyPressure * 0.20f);
                effective.RiskTolerance = math.saturate(effective.RiskTolerance - selfPressure * 0.28f - kinPressure * 0.08f + loyaltyPressure * 0.04f);
                effective.Patience = math.saturate(effective.Patience + loyaltyPressure * 0.12f + kinPressure * 0.06f);

                directivePressure = math.saturate(math.max(directivePressure, sharedPressure));
                trustPressure = math.saturate(math.max(trustPressure, loyaltyPressure));
            }

            return effective;
        }
    }
}
