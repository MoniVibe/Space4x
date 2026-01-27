using PureDOTS.Input;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Miracles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Computes the current targeting solution for the selected miracle.
    /// Produces data for preview systems and validates inputs before activation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(MiracleActivationSystem))]
    public partial struct MiracleTargetingSystem : ISystem
    {
        private BufferLookup<MiracleCooldown> _cooldownLookup;
        private ComponentLookup<MiracleCasterSelection> _slotSelectionLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<MiracleChargeState> _chargeStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleConfigState>();
            _cooldownLookup = state.GetBufferLookup<MiracleCooldown>(true);
            _slotSelectionLookup = state.GetComponentLookup<MiracleCasterSelection>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _chargeStateLookup = state.GetComponentLookup<MiracleChargeState>(true);
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

            _cooldownLookup.Update(ref state);
            _slotSelectionLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _chargeStateLookup.Update(ref state);

            foreach (var (runtimeState, handInput, solutionRef, entity) in SystemAPI
                         .Query<RefRO<MiracleRuntimeStateNew>, RefRO<DivineHandInput>, RefRW<MiracleTargetSolution>>()
                         .WithEntityAccess())
            {
                var solution = solutionRef.ValueRW;
                bool hasSlotSelection = _slotSelectionLookup.HasComponent(entity);
                MiracleCasterSelection slotSelection = hasSlotSelection ? _slotSelectionLookup[entity] : default;
                var selectedId = ResolveSelectedMiracleId(
                    runtimeState.ValueRO,
                    hasSlotSelection,
                    slotSelection,
                    SystemAPI.HasBuffer<MiracleSlotDefinition>(entity)
                        ? SystemAPI.GetBuffer<MiracleSlotDefinition>(entity)
                        : default);

                if (selectedId == MiracleId.None || !TryLookupSpec(selectedId, ref catalog, out int specIndex))
                {
                    MarkInvalid(ref solution, selectedId, MiracleTargetValidityReason.NoTargetFound);
                    solutionRef.ValueRW = solution;
                    continue;
                }

                ref var spec = ref catalog.Specs[specIndex];
                float3 origin = handInput.ValueRO.CursorWorldPosition;
                if (_transformLookup.HasComponent(entity))
                {
                    origin = _transformLookup[entity].Position;
                }

                var targetPoint = ComputeTargetPoint(origin, handInput.ValueRO, runtimeState.ValueRO, ref spec);
                var validity = ValidateSolution(entity, origin, targetPoint, ref spec, ref _cooldownLookup);

                // Compute charge-aware preview radius
                float previewRadius = ComputePreviewRadius(ref spec);
                bool hasChargeState = _chargeStateLookup.HasComponent(entity);
                if (hasChargeState && spec.ChargeModel != MiracleChargeModel.None)
                {
                    var chargeState = _chargeStateLookup[entity];
                    float chargeMultiplier = math.lerp(1f, spec.RadiusChargeMultiplier, chargeState.Charge01);
                    previewRadius *= chargeMultiplier;
                    float maxChargedRadius = spec.MaxRadius > 0f ? spec.MaxRadius * spec.RadiusChargeMultiplier : spec.BaseRadius * spec.RadiusChargeMultiplier;
                    previewRadius = math.clamp(previewRadius, spec.BaseRadius, maxChargedRadius);
                }

                solution.TargetPoint = targetPoint;
                solution.TargetEntity = Entity.Null;
                solution.Radius = previewRadius;
                solution.PreviewArcStart = origin;
                solution.PreviewArcEnd = targetPoint;
                solution.SelectedMiracleId = selectedId;
                solution.IsValid = (byte)(validity == MiracleTargetValidityReason.None ? 1 : 0);
                solution.ValidityReason = validity;

                solutionRef.ValueRW = solution;
            }
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

        private static MiracleTargetValidityReason ValidateSolution(
            Entity casterEntity,
            float3 origin,
            float3 predictedPoint,
            ref MiracleSpec spec,
            ref BufferLookup<MiracleCooldown> cooldownLookup)
        {
            float maxRange = GetMaxRange(ref spec);
            if (maxRange > 0f)
            {
                float distanceSq = math.lengthsq(predictedPoint - origin);
                float rangeSq = maxRange * maxRange;
                if (distanceSq > rangeSq + math.FLT_MIN_NORMAL)
                {
                    return MiracleTargetValidityReason.OutOfRange;
                }
            }

            if (cooldownLookup.HasBuffer(casterEntity))
            {
                var cooldowns = cooldownLookup[casterEntity];
                for (int i = 0; i < cooldowns.Length; i++)
                {
                    if (cooldowns[i].Id == spec.Id && cooldowns[i].RemainingSeconds > 0f)
                    {
                        return MiracleTargetValidityReason.OnCooldown;
                    }
                }
            }

            return MiracleTargetValidityReason.None;
        }

        private static float ComputePreviewRadius(ref MiracleSpec spec)
        {
            float radius = math.max(0f, spec.BaseRadius);
            if (spec.MaxRadius > 0f)
            {
                radius = math.clamp(radius, 0f, spec.MaxRadius);
            }
            return radius;
        }

        private static float GetMaxRange(ref MiracleSpec spec)
        {
            float maxRange = spec.MaxRadius;
            if (maxRange <= 0f)
            {
                maxRange = spec.BaseRadius;
            }

            return math.max(0f, maxRange);
        }

        private static void MarkInvalid(ref MiracleTargetSolution solution, MiracleId id, MiracleTargetValidityReason reason)
        {
            solution.SelectedMiracleId = id;
            solution.IsValid = 0;
            solution.ValidityReason = reason;
            solution.TargetEntity = Entity.Null;
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

        private static float3 ComputeTargetPoint(
            float3 origin,
            in DivineHandInput handInput,
            in MiracleRuntimeStateNew runtimeState,
            ref MiracleSpec spec)
        {
            if (runtimeState.IsSustained != 0)
            {
                return handInput.CursorWorldPosition;
            }

            bool slingshot = handInput.ThrowModeIsSlingshot != 0;
            if (slingshot)
            {
                return ComputeSlingTarget(origin, handInput, ref spec);
            }

            return ComputeReleaseTarget(origin, handInput, ref spec);
        }

        private static float3 ComputeSlingTarget(float3 origin, in DivineHandInput handInput, ref MiracleSpec spec)
        {
            float maxRange = GetMaxRange(ref spec);
            float normalizedCharge = math.saturate(handInput.ThrowCharge);
            float travelDistance = math.max(1f, normalizedCharge * math.max(5f, maxRange));
            travelDistance = math.min(travelDistance, maxRange);

            float3 planarAim = new float3(handInput.AimDirection.x, 0f, handInput.AimDirection.z);
            float3 direction = math.normalizesafe(planarAim, new float3(0f, 0f, 1f));
            var target = origin + direction * travelDistance;
            target.y = math.max(0f, origin.y + handInput.AimDirection.y * travelDistance * 0.25f);
            return target;
        }

        private static float3 ComputeReleaseTarget(float3 origin, in DivineHandInput handInput, ref MiracleSpec spec)
        {
            float maxRange = GetMaxRange(ref spec);
            float3 direction = math.normalizesafe(handInput.AimDirection, new float3(0f, 0f, 1f));
            return origin + direction * maxRange;
        }
    }
}

