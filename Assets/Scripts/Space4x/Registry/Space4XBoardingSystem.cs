using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures boarding tuning exists.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XBoardingBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XBoardingTuning>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XBoardingTuning));
            state.EntityManager.SetComponentData(entity, Space4XBoardingTuning.Default);
            state.Enabled = false;
        }
    }

    /// <summary>
    /// Resolves boarding actions.
    /// Boarding can start only when shields/hull are sufficiently suppressed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XWeaponSystem))]
    public partial struct Space4XBoardingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XBoardingProfile> _boardingProfileLookup;
        private ComponentLookup<Space4XBoardingDeploymentProfile> _boardingDeploymentLookup;
        private ComponentLookup<Space4XFocusModifiers> _focusLookup;
        private ComponentLookup<DerivedCapacities> _capacitiesLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private ComponentLookup<IndividualStats> _individualStatsLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<Space4XBoardingCaptureState> _captureLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private BufferLookup<Space4XBoardingManifestEntry> _boardingManifestLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XBoardingOrder>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(true);
            _boardingProfileLookup = state.GetComponentLookup<Space4XBoardingProfile>(true);
            _boardingDeploymentLookup = state.GetComponentLookup<Space4XBoardingDeploymentProfile>(false);
            _focusLookup = state.GetComponentLookup<Space4XFocusModifiers>(true);
            _capacitiesLookup = state.GetComponentLookup<DerivedCapacities>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _individualStatsLookup = state.GetComponentLookup<IndividualStats>(true);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _captureLookup = state.GetComponentLookup<Space4XBoardingCaptureState>(false);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(false);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(false);
            _boardingManifestLookup = state.GetBufferLookup<Space4XBoardingManifestEntry>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tuning = Space4XBoardingTuning.Default;
            if (SystemAPI.TryGetSingleton<Space4XBoardingTuning>(out var configured))
            {
                tuning = configured;
            }

            _transformLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _boardingProfileLookup.Update(ref state);
            _boardingDeploymentLookup.Update(ref state);
            _focusLookup.Update(ref state);
            _capacitiesLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _individualStatsLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _captureLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _boardingManifestLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentTick = timeState.Tick;
            var em = state.EntityManager;

            foreach (var (orderRef, attacker) in SystemAPI.Query<RefRW<Space4XBoardingOrder>>().WithEntityAccess())
            {
                var order = orderRef.ValueRO;
                ClampAndSeedDefaults(ref order, in tuning);

                if (IsResolved(order))
                {
                    if (order.AutoClearOnResolve != 0)
                    {
                        ecb.RemoveComponent<Space4XBoardingOrder>(attacker);
                    }
                    else
                    {
                        orderRef.ValueRW = order;
                    }

                    continue;
                }

                if (order.Target == Entity.Null || !em.Exists(order.Target))
                {
                    FinalizeOrder(ref order, Space4XBoardingPhase.Aborted, Space4XBoardingOutcome.Aborted, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                var inRange = ResolveInRange(attacker, order.Target, order.DesiredRangeMeters, out var distanceMeters);
                order.LastKnownDistanceMeters = distanceMeters;
                ResolveTargetRatios(order.Target, out var hullRatio, out var shieldRatio);
                var boardingWindow = inRange &&
                                     shieldRatio <= tuning.MaxShieldRatioToStart &&
                                     hullRatio <= tuning.MaxHullRatioToStart;

                if (order.Phase == Space4XBoardingPhase.None || order.Phase == Space4XBoardingPhase.WaitingForWindow)
                {
                    order.Phase = Space4XBoardingPhase.WaitingForWindow;
                    if (boardingWindow)
                    {
                        var committedBoarders = ResolveCommittedBoarders(attacker, ref order, consume: true, in tuning);
                        if (committedBoarders <= 0)
                        {
                            FinalizeOrder(ref order, Space4XBoardingPhase.Aborted, Space4XBoardingOutcome.Aborted, currentTick);
                            WriteOrClear(orderRef, in order, attacker, ref ecb);
                            continue;
                        }

                        var defenderBoarders = ResolveDefenderBoarders(order.Target, order.TargetKind, in tuning);
                        order.DefenderEstimatedBoarderCount = (ushort)math.clamp(defenderBoarders, 0, ushort.MaxValue);
                        order.StartedTick = currentTick;
                        order.LastResolveTick = currentTick;
                        order.AttackerForce = ResolveAssaultStrength(attacker, order, committedBoarders, in tuning);
                        order.DefenderForce = ResolveDefenseStrength(order.Target, hullRatio, shieldRatio, defenderBoarders, order.TargetKind, in tuning);
                        order.AttackerLosses = 0f;
                        order.DefenderLosses = 0f;
                        order.AssaultProgress01 = 0f;
                        order.Phase = Space4XBoardingPhase.Launching;
                    }

                    orderRef.ValueRW = order;
                    continue;
                }

                if (!IsActivePhase(order.Phase))
                {
                    FinalizeOrder(ref order, Space4XBoardingPhase.Aborted, Space4XBoardingOutcome.Aborted, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                if (!inRange)
                {
                    FinalizeOrder(ref order, Space4XBoardingPhase.Aborted, Space4XBoardingOutcome.Aborted, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                if (order.MaxDurationTicks > 0u && currentTick >= order.StartedTick + order.MaxDurationTicks)
                {
                    FinalizeOrder(ref order, Space4XBoardingPhase.Repelled, Space4XBoardingOutcome.Repelled, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                var resolveInterval = math.max(1u, tuning.MinResolveIntervalTicks);
                var elapsedTicks = currentTick > order.LastResolveTick ? currentTick - order.LastResolveTick : 0u;
                if (elapsedTicks < resolveInterval)
                {
                    orderRef.ValueRW = order;
                    continue;
                }

                var stepTicks = math.max(1u, elapsedTicks);
                var advantage = order.AttackerForce / math.max(0.05f, order.DefenderForce);

                var progressDelta = (tuning.BaseProgressPerTick + (advantage - 1f) * tuning.AdvantageProgressScale) * stepTicks;
                progressDelta += math.saturate(order.PodPenetration) * 0.02f * stepTicks;
                progressDelta += math.saturate(order.ElectronicWarfareSupport) * 0.015f * stepTicks;
                if (!boardingWindow)
                {
                    progressDelta -= 0.03f * stepTicks;
                }

                progressDelta = math.clamp(progressDelta, -0.12f * stepTicks, 0.25f * stepTicks);
                order.AssaultProgress01 = math.clamp(order.AssaultProgress01 + progressDelta, 0f, 1.5f);

                var attackerMitigation = ResolveCasualtyMitigation(attacker);
                var defenderMitigation = ResolveCasualtyMitigation(order.Target);
                var attackerLoss = tuning.BaseCasualtyPerTick * stepTicks *
                                   math.saturate(1.25f / math.max(0.25f, advantage)) * (1f - attackerMitigation);
                var defenderLoss = tuning.BaseCasualtyPerTick * stepTicks *
                                   math.saturate(advantage) * (1f - defenderMitigation);

                attackerLoss = math.clamp(attackerLoss, 0f, order.AttackerForce);
                defenderLoss = math.clamp(defenderLoss, 0f, order.DefenderForce);

                order.AttackerForce = math.max(0f, order.AttackerForce - attackerLoss);
                order.DefenderForce = math.max(0f, order.DefenderForce - defenderLoss);
                order.AttackerLosses += attackerLoss;
                order.DefenderLosses += defenderLoss;
                order.LastResolveTick = currentTick;

                if (order.AssaultProgress01 < 0.25f)
                {
                    order.Phase = Space4XBoardingPhase.Launching;
                }
                else if (order.AssaultProgress01 < 0.65f)
                {
                    order.Phase = Space4XBoardingPhase.Breaching;
                }
                else
                {
                    order.Phase = Space4XBoardingPhase.Fighting;
                }

                if (order.AttackerForce <= 0.01f)
                {
                    FinalizeOrder(ref order, Space4XBoardingPhase.Repelled, Space4XBoardingOutcome.Repelled, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                var captureWindow = inRange &&
                                    shieldRatio <= tuning.MaxShieldRatioToCapture &&
                                    hullRatio <= tuning.MaxHullRatioToCapture;
                if (captureWindow && (order.AssaultProgress01 >= tuning.CaptureThreshold || order.DefenderForce <= 0.01f))
                {
                    CaptureTarget(attacker, order.Target, hullRatio, order.DefenderForce, currentTick, in tuning, ref ecb);
                    FinalizeOrder(ref order, Space4XBoardingPhase.Captured, Space4XBoardingOutcome.Captured, currentTick);
                    WriteOrClear(orderRef, in order, attacker, ref ecb);
                    continue;
                }

                orderRef.ValueRW = order;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void ClampAndSeedDefaults(ref Space4XBoardingOrder order, in Space4XBoardingTuning tuning)
        {
            if (order.MaxDurationTicks == 0u)
            {
                order.MaxDurationTicks = tuning.DefaultMaxDurationTicks;
            }

            if (order.DesiredRangeMeters <= 0f)
            {
                order.DesiredRangeMeters = tuning.MaxBoardingRangeMeters;
            }

            order.TroopCommitment01 = math.saturate(order.TroopCommitment01);
            order.PodPenetration = math.saturate(order.PodPenetration);
            order.ElectronicWarfareSupport = math.saturate(order.ElectronicWarfareSupport);

            if (order.TargetKind > Space4XBoardingTargetKind.Colony)
            {
                order.TargetKind = Space4XBoardingTargetKind.Ship;
            }

            var hardMax = math.min(math.max(1, tuning.HardMaxBoardersPerAction), (int)ushort.MaxValue);
            if (order.RequestedBoarderCount > hardMax)
            {
                order.RequestedBoarderCount = (ushort)hardMax;
            }
        }

        private static bool IsResolved(in Space4XBoardingOrder order)
        {
            return order.Outcome != Space4XBoardingOutcome.None ||
                   order.Phase == Space4XBoardingPhase.Captured ||
                   order.Phase == Space4XBoardingPhase.Repelled ||
                   order.Phase == Space4XBoardingPhase.Aborted;
        }

        private static bool IsActivePhase(Space4XBoardingPhase phase)
        {
            return phase == Space4XBoardingPhase.Launching ||
                   phase == Space4XBoardingPhase.Breaching ||
                   phase == Space4XBoardingPhase.Fighting;
        }

        private static void FinalizeOrder(
            ref Space4XBoardingOrder order,
            Space4XBoardingPhase phase,
            Space4XBoardingOutcome outcome,
            uint tick)
        {
            order.Phase = phase;
            order.Outcome = outcome;
            order.CompletedTick = tick;
            order.LastResolveTick = tick;
        }

        private void WriteOrClear(
            RefRW<Space4XBoardingOrder> orderRef,
            in Space4XBoardingOrder order,
            Entity attacker,
            ref EntityCommandBuffer ecb)
        {
            if (order.AutoClearOnResolve != 0)
            {
                ecb.RemoveComponent<Space4XBoardingOrder>(attacker);
            }
            else
            {
                orderRef.ValueRW = order;
            }
        }

        private bool ResolveInRange(Entity attacker, Entity target, float desiredRangeMeters, out float distanceMeters)
        {
            distanceMeters = 0f;
            if (!_transformLookup.HasComponent(attacker) || !_transformLookup.HasComponent(target))
            {
                return true;
            }

            var attackerPos = _transformLookup[attacker].Position;
            var targetPos = _transformLookup[target].Position;
            distanceMeters = math.distance(attackerPos, targetPos);
            return distanceMeters <= math.max(0.01f, desiredRangeMeters);
        }

        private void ResolveTargetRatios(Entity target, out float hullRatio, out float shieldRatio)
        {
            hullRatio = 1f;
            shieldRatio = 0f;

            if (_hullLookup.HasComponent(target))
            {
                var hull = _hullLookup[target];
                if (hull.Max > 0f)
                {
                    hullRatio = math.saturate(hull.Current / hull.Max);
                }
            }

            if (_shieldLookup.HasComponent(target))
            {
                var shield = _shieldLookup[target];
                if (shield.Maximum > 0f)
                {
                    shieldRatio = math.saturate(shield.Current / shield.Maximum);
                }
            }
        }

        private float ResolveAssaultStrength(Entity attacker, in Space4XBoardingOrder order, int boarderCount, in Space4XBoardingTuning tuning)
        {
            var strength = 1f;

            if (_boardingProfileLookup.HasComponent(attacker))
            {
                var profile = _boardingProfileLookup[attacker];
                strength *= math.max(0.1f, profile.AssaultStrength);
            }

            strength *= math.lerp(0.35f, 1f, order.TroopCommitment01);
            strength *= 1f + order.PodPenetration * 0.35f;
            strength *= 1f + order.ElectronicWarfareSupport * 0.2f;

            if (_focusLookup.HasComponent(attacker))
            {
                var focus = _focusLookup[attacker];
                var bonus = Space4XFocusIntegration.GetBoardingEffectivenessBonus(focus);
                strength *= 1f + bonus * tuning.FocusBonusScale;
            }

            if (_capacitiesLookup.HasComponent(attacker))
            {
                var capacities = _capacitiesLookup[attacker];
                strength *= 1f + math.max(0f, capacities.Boarding - 1f) * 0.5f;
            }

            if (_physiqueLookup.HasComponent(attacker))
            {
                var physique = _physiqueLookup[attacker];
                var composite = ((float)physique.Physique + (float)physique.Will) / 200f;
                strength *= 1f + composite * 0.25f;
            }

            strength *= ResolveBoarderCountScale(boarderCount, in tuning);
            strength *= ResolveBoarderQualityMultiplier(attacker, boarderCount, in tuning);

            return math.max(0.05f, strength);
        }

        private float ResolveDefenseStrength(
            Entity target,
            float hullRatio,
            float shieldRatio,
            int defenderBoarders,
            Space4XBoardingTargetKind targetKind,
            in Space4XBoardingTuning tuning)
        {
            var strength = 1f;

            if (_boardingProfileLookup.HasComponent(target))
            {
                var profile = _boardingProfileLookup[target];
                strength *= math.max(0.1f, profile.DefenseStrength);
                strength *= 1f + math.max(0f, profile.InternalSecurity - 1f) * 0.35f;
            }

            strength *= 1f + math.saturate(shieldRatio) * tuning.ShieldDefenseScale;
            strength *= math.lerp(tuning.CriticalHullDefenseFloor, 1f, math.saturate(hullRatio));

            if (_focusLookup.HasComponent(target))
            {
                var focus = _focusLookup[target];
                var bonus = Space4XFocusIntegration.GetBoardingEffectivenessBonus(focus);
                strength *= 1f + bonus * tuning.FocusBonusScale * 0.75f;
            }

            if (_capacitiesLookup.HasComponent(target))
            {
                var capacities = _capacitiesLookup[target];
                strength *= 1f + math.max(0f, capacities.Boarding - 1f) * 0.4f;
            }

            if (_physiqueLookup.HasComponent(target))
            {
                var physique = _physiqueLookup[target];
                var composite = ((float)physique.Physique + (float)physique.Will) / 200f;
                strength *= 1f + composite * 0.2f;
            }

            strength *= ResolveBoarderCountScale(defenderBoarders, in tuning);
            strength *= ResolveBoarderQualityMultiplier(target, defenderBoarders, in tuning);

            // Non-ship targets typically have spatially distributed defenders and hardened control nodes.
            if (targetKind == Space4XBoardingTargetKind.Station)
            {
                strength *= 1.15f;
            }
            else if (targetKind == Space4XBoardingTargetKind.Colony)
            {
                strength *= 1.3f;
            }

            return math.max(0.05f, strength);
        }

        private int ResolveCommittedBoarders(Entity attacker, ref Space4XBoardingOrder order, bool consume, in Space4XBoardingTuning tuning)
        {
            var hardCap = math.min(math.max(1, tuning.HardMaxBoardersPerAction), (int)ushort.MaxValue);
            var requested = (int)order.RequestedBoarderCount;

            var manifestCount = CountActiveManifestEntries(attacker, hardCap);
            if (manifestCount > 0)
            {
                var committed = requested > 0 ? math.min(requested, manifestCount) : manifestCount;
                committed = math.clamp(committed, 1, hardCap);
                order.CommittedBoarderCount = (ushort)committed;
                return committed;
            }

            if (_boardingDeploymentLookup.HasComponent(attacker))
            {
                var deployment = _boardingDeploymentLookup[attacker];
                var available = math.max(0, deployment.AvailableBoarders);
                var deployCap = deployment.MaxDeployPerAction > 0 ? deployment.MaxDeployPerAction : available;
                var committed = math.min(available, math.max(0, deployCap));
                if (requested > 0)
                {
                    committed = math.min(committed, requested);
                }

                committed = math.clamp(committed, 0, hardCap);
                if (consume && committed > 0)
                {
                    deployment.AvailableBoarders = math.max(0, available - committed);
                    _boardingDeploymentLookup[attacker] = deployment;
                }

                order.CommittedBoarderCount = (ushort)committed;
                return committed;
            }

            var starterMin = math.max(1, tuning.StarterMinBoarders);
            var starterMax = math.max(starterMin, tuning.StarterMaxBoarders);
            var fallback = (int)math.round(math.lerp(starterMin, starterMax, order.TroopCommitment01));
            var fallbackCommitted = requested > 0 ? requested : fallback;
            fallbackCommitted = math.clamp(fallbackCommitted, 1, hardCap);
            order.CommittedBoarderCount = (ushort)fallbackCommitted;
            return fallbackCommitted;
        }

        private int ResolveDefenderBoarders(Entity target, Space4XBoardingTargetKind targetKind, in Space4XBoardingTuning tuning)
        {
            var hardCap = math.min(math.max(1, tuning.HardMaxBoardersPerAction), (int)ushort.MaxValue);
            var manifestCount = CountActiveManifestEntries(target, hardCap);
            if (manifestCount > 0)
            {
                return math.clamp(manifestCount, 1, hardCap);
            }

            if (_boardingDeploymentLookup.HasComponent(target))
            {
                var deployment = _boardingDeploymentLookup[target];
                var available = math.max(0, deployment.AvailableBoarders);
                var reserve = math.max(0, deployment.ReserveBoarders);
                var defenders = available + reserve;
                if (defenders <= 0)
                {
                    defenders = deployment.MaxDeployPerAction > 0 ? deployment.MaxDeployPerAction : 0;
                }

                return math.clamp(defenders, 1, hardCap);
            }

            var starterMin = math.max(1, tuning.StarterMinBoarders);
            var starterMax = math.max(starterMin, tuning.StarterMaxBoarders);
            var fallback = starterMax;
            if (targetKind == Space4XBoardingTargetKind.Station)
            {
                fallback *= 3;
            }
            else if (targetKind == Space4XBoardingTargetKind.Colony)
            {
                fallback *= 6;
            }

            return math.clamp(fallback, 1, hardCap);
        }

        private int CountActiveManifestEntries(Entity owner, int hardCap)
        {
            if (!_boardingManifestLookup.HasBuffer(owner))
            {
                return 0;
            }

            var count = 0;
            var manifest = _boardingManifestLookup[owner];
            for (var i = 0; i < manifest.Length; i++)
            {
                if (manifest[i].Active == 0)
                {
                    continue;
                }

                if (manifest[i].Individual == Entity.Null)
                {
                    continue;
                }

                count++;
                if (count >= hardCap)
                {
                    break;
                }
            }

            return count;
        }

        private float ResolveBoarderCountScale(int boarderCount, in Space4XBoardingTuning tuning)
        {
            var clampedCount = math.max(1, boarderCount);
            var exponent = math.clamp(tuning.BoarderCountExponent, 0.55f, 1.1f);
            var scale = math.pow(clampedCount, exponent) * math.max(0.001f, tuning.BoarderForceScale);
            return math.max(0.05f, scale);
        }

        private float ResolveBoarderQualityMultiplier(Entity owner, int boarderCount, in Space4XBoardingTuning tuning)
        {
            var quality01 = ResolveBoarderQuality01(owner, boarderCount, in tuning);
            var centered = (quality01 - 0.5f) * 2f;
            var bonus = centered * math.max(0f, tuning.BoarderQualityScale);
            return math.clamp(1f + bonus, 0.35f, 2.5f);
        }

        private float ResolveBoarderQuality01(Entity owner, int boarderCount, in Space4XBoardingTuning tuning)
        {
            var hardCap = math.min(math.max(1, tuning.HardMaxBoardersPerAction), (int)ushort.MaxValue);
            if (_boardingManifestLookup.HasBuffer(owner))
            {
                var manifest = _boardingManifestLookup[owner];
                var qualitySum = 0f;
                var counted = 0;
                var limit = math.max(1, math.min(hardCap, boarderCount > 0 ? boarderCount : manifest.Length));
                for (var i = 0; i < manifest.Length; i++)
                {
                    if (counted >= limit)
                    {
                        break;
                    }

                    var entry = manifest[i];
                    if (entry.Active == 0 || entry.Individual == Entity.Null)
                    {
                        continue;
                    }

                    qualitySum += ResolveIndividualQuality01(entry);
                    counted++;
                }

                if (counted > 0)
                {
                    return math.saturate(qualitySum / counted);
                }
            }

            if (_boardingDeploymentLookup.HasComponent(owner))
            {
                var profile = _boardingDeploymentLookup[owner];
                var quality = 0.35f +
                              math.saturate(profile.AverageTraining01) * 0.35f +
                              math.saturate(profile.AverageArmor01) * 0.15f +
                              math.saturate(profile.AverageWeapon01) * 0.15f;
                return math.saturate(quality);
            }

            return 0.55f;
        }

        private float ResolveIndividualQuality01(in Space4XBoardingManifestEntry entry)
        {
            var physique01 = 0.5f;
            var finesse01 = 0.5f;
            var will01 = 0.5f;
            var boarding01 = 0.5f;
            var tactics01 = 0.5f;

            var individual = entry.Individual;
            if (_physiqueLookup.HasComponent(individual))
            {
                var physique = _physiqueLookup[individual];
                physique01 = math.saturate((float)physique.Physique / 100f);
                finesse01 = math.saturate((float)physique.Finesse / 100f);
                will01 = math.saturate((float)physique.Will / 100f);
            }

            if (_capacitiesLookup.HasComponent(individual))
            {
                var capacities = _capacitiesLookup[individual];
                boarding01 = math.saturate((capacities.Boarding - 0.5f) / 1.5f);
            }

            if (_individualStatsLookup.HasComponent(individual))
            {
                var stats = _individualStatsLookup[individual];
                tactics01 = math.saturate((float)stats.Tactics / 100f);
            }

            var readiness = math.saturate((float)entry.Readiness01);
            var armor = math.saturate((float)entry.ArmorTier01);
            var weapon = math.saturate((float)entry.WeaponTier01);

            var quality = 0.18f +
                          physique01 * 0.24f +
                          finesse01 * 0.2f +
                          will01 * 0.13f +
                          boarding01 * 0.15f +
                          tactics01 * 0.08f +
                          readiness * 0.1f +
                          armor * 0.06f +
                          weapon * 0.06f;

            return math.saturate(quality);
        }

        private float ResolveCasualtyMitigation(Entity entity)
        {
            var mitigation = 0f;

            if (_boardingProfileLookup.HasComponent(entity))
            {
                mitigation += math.saturate(_boardingProfileLookup[entity].CasualtyMitigation01);
            }

            if (_focusLookup.HasComponent(entity))
            {
                var focus = _focusLookup[entity];
                mitigation += math.saturate(Space4XFocusIntegration.GetCrewStressReduction(focus)) * 0.5f;
            }

            return math.saturate(mitigation);
        }

        private void CaptureTarget(
            Entity attacker,
            Entity target,
            float hullRatio,
            float remainingDefenderForce,
            uint tick,
            in Space4XBoardingTuning tuning,
            ref EntityCommandBuffer ecb)
        {
            var capture = new Space4XBoardingCaptureState
            {
                Captor = attacker,
                CapturedTick = tick,
                HullRatioAtCapture = hullRatio,
                RemainingDefenderForce = remainingDefenderForce
            };

            if (_captureLookup.HasComponent(target))
            {
                _captureLookup[target] = capture;
            }
            else
            {
                ecb.AddComponent(target, capture);
            }

            if (_affiliationLookup.HasBuffer(attacker))
            {
                var attackerAffiliations = _affiliationLookup[attacker];
                if (_affiliationLookup.HasBuffer(target))
                {
                    var targetAffiliations = _affiliationLookup[target];
                    targetAffiliations.Clear();
                    for (var i = 0; i < attackerAffiliations.Length; i++)
                    {
                        targetAffiliations.Add(attackerAffiliations[i]);
                    }
                }
                else
                {
                    var targetAffiliations = ecb.AddBuffer<AffiliationTag>(target);
                    for (var i = 0; i < attackerAffiliations.Length; i++)
                    {
                        targetAffiliations.Add(attackerAffiliations[i]);
                    }
                }
            }

            if (_engagementLookup.HasComponent(target))
            {
                var engagement = _engagementLookup[target];
                engagement.Phase = EngagementPhase.Disabled;
                engagement.PrimaryTarget = attacker;
                _engagementLookup[target] = engagement;
            }

            if (tuning.PostCaptureSubsystemDisableTicks > 0u && _subsystemDisabledLookup.HasBuffer(target))
            {
                var disableUntil = tick + tuning.PostCaptureSubsystemDisableTicks;
                var disabled = _subsystemDisabledLookup[target];
                UpsertSubsystemDisable(ref disabled, SubsystemType.Weapons, disableUntil);
                UpsertSubsystemDisable(ref disabled, SubsystemType.Engines, disableUntil);
            }
        }

        private static void UpsertSubsystemDisable(ref DynamicBuffer<SubsystemDisabled> disabled, SubsystemType type, uint untilTick)
        {
            for (var i = 0; i < disabled.Length; i++)
            {
                if (disabled[i].Type != type)
                {
                    continue;
                }

                var entry = disabled[i];
                if (entry.UntilTick < untilTick)
                {
                    entry.UntilTick = untilTick;
                    disabled[i] = entry;
                }

                return;
            }

            disabled.Add(new SubsystemDisabled
            {
                Type = type,
                UntilTick = untilTick
            });
        }
    }
}
