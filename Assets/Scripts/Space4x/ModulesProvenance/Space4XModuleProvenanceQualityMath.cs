using Unity.Mathematics;
using Unity.Entities;

namespace Space4X.ModulesProvenance
{
    public struct ModuleQualityVector
    {
        public float Cooling;
        public float Power;
        public float Optics;
        public float Mount;
        public float Firmware;
    }

    public static class ModuleProvenanceQualityMath
    {
        public static float ResolveDivisionSkill(in WorkforceQuality workforce, DivisionId divisionId)
        {
            return divisionId switch
            {
                DivisionId.Cooling => workforce.SkillCooling,
                DivisionId.Power => workforce.SkillPower,
                DivisionId.Optics => workforce.SkillOptics,
                DivisionId.Mounting => workforce.SkillMount,
                DivisionId.Firmware => workforce.SkillFirmware,
                _ => 0f
            };
        }

        public static float ResolveFacilityMaturity(
            in BlueprintId blueprintId,
            float fallbackMaturity,
            ref DynamicBuffer<FacilityBlueprintMaturity> maturityByBlueprint)
        {
            for (var i = 0; i < maturityByBlueprint.Length; i++)
            {
                var item = maturityByBlueprint[i];
                if (item.BlueprintId.Equals(blueprintId))
                {
                    return math.saturate(item.ProcessMaturity);
                }
            }

            return math.saturate(fallbackMaturity);
        }

        public static float EvaluateLimbQuality(
            float materialQuality,
            float workforceSkill,
            float facilityCapability,
            float facilityMaturity,
            float targetQuality)
        {
            var baseQuality =
                math.saturate(materialQuality) * 0.42f +
                math.saturate(workforceSkill) * 0.23f +
                math.saturate(facilityCapability) * 0.20f +
                math.saturate(facilityMaturity) * 0.15f;

            // Pushing beyond process capability induces deterministic over-target penalties.
            var targetPenalty = math.max(0f, math.saturate(targetQuality) - baseQuality) * 0.25f;
            return math.saturate(baseQuality - targetPenalty);
        }

        public static float EvaluateIntegrationQuality(
            in ModuleQualityVector limbs,
            float assemblerSkill,
            float assemblerCapability,
            float assemblerProcessMaturity,
            float provenanceProcessMaturity,
            BlueprintProvenanceKind provenanceKind)
        {
            var minLimb = math.min(limbs.Cooling,
                math.min(limbs.Power, math.min(limbs.Optics, math.min(limbs.Mount, limbs.Firmware))));

            var weightedMean =
                limbs.Cooling * 0.18f +
                limbs.Power * 0.24f +
                limbs.Optics * 0.23f +
                limbs.Mount * 0.17f +
                limbs.Firmware * 0.18f;

            var limbAggregate = minLimb * 0.55f + weightedMean * 0.45f;
            var assemblyFactor =
                math.saturate(assemblerSkill) * 0.45f +
                math.saturate(assemblerCapability) * 0.35f +
                math.saturate(assemblerProcessMaturity) * 0.12f +
                math.saturate(provenanceProcessMaturity) * 0.08f;

            var provenanceFactor = ResolveProvenanceFactor(provenanceKind);
            return math.saturate(limbAggregate * 0.72f + assemblyFactor * 0.28f) * provenanceFactor;
        }

        public static bool ShouldScrapLimb(DivisionId divisionId, float limbQuality, in ModuleCommissionSpec spec)
        {
            var threshold = ResolveDivisionThreshold(divisionId, in spec);
            return limbQuality + 1e-5f < threshold;
        }

        public static bool TryFinalizeModule(
            in ModuleQualityVector limbs,
            float assemblerSkill,
            float assemblerCapability,
            float assemblerProcessMaturity,
            in BlueprintProvenance provenance,
            in ModuleCommissionSpec spec,
            out float integrationQuality,
            out uint rejectMask)
        {
            rejectMask = 0u;

            if (ShouldScrapLimb(DivisionId.Cooling, limbs.Cooling, in spec)) rejectMask |= 1u << (int)DivisionId.Cooling;
            if (ShouldScrapLimb(DivisionId.Power, limbs.Power, in spec)) rejectMask |= 1u << (int)DivisionId.Power;
            if (ShouldScrapLimb(DivisionId.Optics, limbs.Optics, in spec)) rejectMask |= 1u << (int)DivisionId.Optics;
            if (ShouldScrapLimb(DivisionId.Mounting, limbs.Mount, in spec)) rejectMask |= 1u << (int)DivisionId.Mounting;
            if (ShouldScrapLimb(DivisionId.Firmware, limbs.Firmware, in spec)) rejectMask |= 1u << (int)DivisionId.Firmware;

            if (rejectMask != 0u && spec.RejectBelowSpec != 0)
            {
                integrationQuality = 0f;
                return false;
            }

            integrationQuality = EvaluateIntegrationQuality(
                in limbs,
                assemblerSkill,
                assemblerCapability,
                assemblerProcessMaturity,
                provenance.ProcessMaturity,
                provenance.ProvenanceKind);

            if (spec.RejectBelowSpec != 0 && integrationQuality + 1e-5f < spec.MinIntegrationQuality)
            {
                rejectMask |= 1u << 7;
                return false;
            }

            return true;
        }

        public static uint ComputeProvenanceDigest(
            in BlueprintId blueprintId,
            in BlueprintProvenance provenance,
            in ModuleQualityVector limbs,
            float integrationQuality)
        {
            var qCooling = Quantize01(limbs.Cooling);
            var qPower = Quantize01(limbs.Power);
            var qOptics = Quantize01(limbs.Optics);
            var qMount = Quantize01(limbs.Mount);
            var qFirmware = Quantize01(limbs.Firmware);
            var qIntegration = Quantize01(integrationQuality);
            var qKnowledge = Quantize01(provenance.KnowledgeLevel);
            var qMaturity = Quantize01(provenance.ProcessMaturity);

            var h0 = math.hash(new uint4(blueprintId.StableHash(), provenance.OriginOrgId.Value, (uint)provenance.ProvenanceKind, qKnowledge));
            var h1 = math.hash(new uint4(h0, qMaturity, qCooling, qPower));
            var h2 = math.hash(new uint4(h1, qOptics, qMount, qFirmware));
            return math.hash(new uint4(h2, qIntegration, 0xA5A5A5A5u, 0x9E3779B9u));
        }

        public static float ResolveProvenanceFactor(BlueprintProvenanceKind provenanceKind)
        {
            return provenanceKind switch
            {
                BlueprintProvenanceKind.Original => 1.00f,
                BlueprintProvenanceKind.Licensed => 0.97f,
                BlueprintProvenanceKind.ReverseEngineered => 0.90f,
                BlueprintProvenanceKind.Iterated => 0.95f,
                _ => 0.92f
            };
        }

        private static float ResolveDivisionThreshold(DivisionId divisionId, in ModuleCommissionSpec spec)
        {
            return divisionId switch
            {
                DivisionId.Cooling => spec.MinCoolingQuality,
                DivisionId.Power => spec.MinPowerQuality,
                DivisionId.Optics => spec.MinOpticsQuality,
                DivisionId.Mounting => spec.MinMountQuality,
                DivisionId.Firmware => spec.MinFirmwareQuality,
                _ => 0f
            };
        }

        private static uint Quantize01(float value)
        {
            return (uint)math.clamp((int)math.round(math.saturate(value) * 10000f), 0, 10000);
        }
    }
}
