using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Tracks miracle charge accumulation while the cast input is held.
    /// Updates MiracleChargeState so activation/preview systems can scale effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(MiracleTargetingSystem))]
    public partial struct MiracleChargeSystem : ISystem
    {
        private ComponentLookup<MiracleCasterSelection> _slotSelectionLookup;
        private ComponentLookup<MiracleChargeTrackingState> _trackingLookup;
        private ComponentLookup<DivineHandInput> _handInputLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleConfigState>();
            _slotSelectionLookup = state.GetComponentLookup<MiracleCasterSelection>(true);
            _trackingLookup = state.GetComponentLookup<MiracleChargeTrackingState>(false);
            _handInputLookup = state.GetComponentLookup<DivineHandInput>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<MiracleConfigState>(out var configState))
            {
                return;
            }

            ref var catalog = ref configState.Catalog.Value;
            if (catalog.Specs.Length == 0)
            {
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            _slotSelectionLookup.Update(ref state);
            _trackingLookup.Update(ref state);
            _handInputLookup.Update(ref state);

            foreach (var (chargeRef, runtimeState, entity) in SystemAPI
                         .Query<RefRW<MiracleChargeState>, RefRO<MiracleRuntimeStateNew>>()
                         .WithEntityAccess())
            {
                var charge = chargeRef.ValueRW;
                var runtime = runtimeState.ValueRO;

                bool hasSlotSelection = _slotSelectionLookup.HasComponent(entity);
                MiracleCasterSelection slotSelection = hasSlotSelection ? _slotSelectionLookup[entity] : default;
                var selectedId = ResolveSelectedMiracleId(
                    runtime,
                    hasSlotSelection,
                    slotSelection,
                    SystemAPI.HasBuffer<MiracleSlotDefinition>(entity)
                        ? SystemAPI.GetBuffer<MiracleSlotDefinition>(entity)
                        : default);

                // Track previous selected ID to detect miracle switches
                bool hasTracking = _trackingLookup.HasComponent(entity);
                MiracleChargeTrackingState tracking = hasTracking ? _trackingLookup[entity] : default;
                MiracleId previousId = hasTracking ? tracking.PreviousSelectedId : MiracleId.None;

                // Reset charge if miracle changed (but not if switching from None to a miracle)
                if (previousId != MiracleId.None && previousId != selectedId)
                {
                    ResetCharge(ref charge);
                }

                // Update tracking state
                if (!hasTracking)
                {
                    ecb.AddComponent(entity, new MiracleChargeTrackingState { PreviousSelectedId = selectedId });
                }
                else
                {
                    tracking.PreviousSelectedId = selectedId;
                    ecb.SetComponent(entity, tracking);
                }

                if (selectedId == MiracleId.None || !TryLookupSpec(selectedId, ref catalog, out int specIndex))
                {
                    ResetCharge(ref charge);
                    chargeRef.ValueRW = charge;
                    continue;
                }

                ref var spec = ref catalog.Specs[specIndex];
                if (spec.ChargeModel == MiracleChargeModel.None)
                {
                    ResetCharge(ref charge);
                    chargeRef.ValueRW = charge;
                    continue;
                }

                bool hasHandInput = _handInputLookup.HasComponent(entity);
                DivineHandInput handInput = hasHandInput ? _handInputLookup[entity] : default;
                bool inputHeld = hasHandInput && handInput.SecondaryHeld != 0;
                bool chargingSignal = runtime.IsActivating != 0 || runtime.IsSustained != 0 || inputHeld;

                // Track previous charging state to detect transitions
                bool wasCharging = charge.IsCharging != 0;

                if (chargingSignal)
                {
                    charge.IsCharging = 1;
                    charge.HeldTime += deltaTime;

                    switch (spec.ChargeModel)
                    {
                        case MiracleChargeModel.HoldToTier:
                            UpdateTierCharge(ref charge, ref spec);
                            break;
                        case MiracleChargeModel.HoldToContinuous:
                            UpdateContinuousCharge(ref charge, ref spec);
                            break;
                    }
                }
                else
                {
                    // Reset charge when charging signal transitions from true → false
                    if (wasCharging)
                    {
                        ResetCharge(ref charge);
                    }
                    else
                    {
                        charge.IsCharging = 0;
                    }
                }

                chargeRef.ValueRW = charge;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void UpdateTierCharge(ref MiracleChargeState charge, ref MiracleSpec spec)
        {
            ref var thresholds = ref spec.TierTimeThresholds;
            byte tierIndex = 0;
            float heldTime = charge.HeldTime;

            for (int i = 0; i < thresholds.Length; i++)
            {
                if (heldTime + math.FLT_MIN_NORMAL >= thresholds[i])
                {
                    tierIndex = (byte)math.min(i + 1, 255);
                }
                else
                {
                    break;
                }
            }

            charge.TierIndex = (byte)math.min((int)tierIndex, (int)spec.TierCount);

            if (spec.TierCount > 0)
            {
                charge.Charge01 = math.saturate(charge.TierIndex / (float)spec.TierCount);
            }
            else
            {
                charge.Charge01 = 0f;
            }
        }

        private static void UpdateContinuousCharge(ref MiracleChargeState charge, ref MiracleSpec spec)
        {
            float maxTime = math.max(0.0001f, spec.ChargeTimeMax);
            float normalized = math.saturate(charge.HeldTime / maxTime);
            normalized = ApplyCurve(normalized, spec.ChargeCurveType);
            charge.Charge01 = normalized;

            if (spec.TierCount > 0)
            {
                float tierFloat = normalized * spec.TierCount;
                charge.TierIndex = (byte)math.clamp((int)math.ceil(tierFloat), 0, spec.TierCount);
            }
            else
            {
                charge.TierIndex = 0;
            }
        }

        private static float ApplyCurve(float t, MiracleChargeCurveType curveType)
        {
            switch (curveType)
            {
                case MiracleChargeCurveType.EaseIn:
                    return t * t;
                case MiracleChargeCurveType.EaseOut:
                    {
                        float inv = 1f - t;
                        return 1f - inv * inv;
                    }
                case MiracleChargeCurveType.EaseInOut:
                    return math.saturate(t * t * (3f - 2f * t));
                default:
                    return t;
            }
        }

        private static MiracleId ResolveSelectedMiracleId(
            in MiracleRuntimeStateNew runtimeState,
            bool hasSlotSelection,
            MiracleCasterSelection slotSelection,
            DynamicBuffer<MiracleSlotDefinition> slots)
        {
            if (runtimeState.SelectedId != MiracleId.None)
            {
                return runtimeState.SelectedId;
            }

            if (!hasSlotSelection || !slots.IsCreated)
            {
                return MiracleId.None;
            }

            byte selectedIndex = slotSelection.SelectedSlot;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].SlotIndex == selectedIndex)
                {
                    return (MiracleId)slots[i].Type;
                }
            }

            return MiracleId.None;
        }

        private static bool TryLookupSpec(MiracleId id, ref MiracleCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Specs.Length; i++)
            {
                if (catalog.Specs[i].Id == id)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void ResetCharge(ref MiracleChargeState charge)
        {
            charge.Charge01 = 0f;
            charge.HeldTime = 0f;
            charge.TierIndex = 0;
            charge.IsCharging = 0;
        }
    }
}

