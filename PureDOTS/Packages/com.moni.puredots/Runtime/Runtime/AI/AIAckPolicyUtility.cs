using PureDOTS.Runtime.Individual;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    public static class AIAckPolicyUtility
    {
        public static float ComputeChaos01(in AlignmentTriplet alignment)
        {
            // Order axis: +1 = Order, -1 = Chaos.
            return math.saturate((1f - alignment.Order) * 0.5f);
        }

        public static float ComputeFocusRatio01(
            bool hasFocusBudget,
            in FocusBudget focusBudget,
            bool hasPools,
            in ResourcePools pools)
        {
            if (hasFocusBudget)
            {
                var max = math.max(1e-4f, focusBudget.Max);
                return math.saturate(focusBudget.Available / max);
            }

            if (hasPools)
            {
                return pools.FocusRatio;
            }

            return 1f;
        }

        public static float ComputeSleepPressure01(
            bool hasVillagerNeeds,
            in VillagerNeedState needs,
            bool hasPools,
            in ResourcePools pools,
            bool hasStats,
            in IndividualStats stats)
        {
            var restUrgency = hasVillagerNeeds ? math.saturate(needs.RestUrgency) : 0f;
            var staminaPressure = hasPools ? math.saturate(1f - pools.StaminaRatio) : 0f;
            var raw = math.max(restUrgency, staminaPressure);

            // Will offsets sleep suppression a bit (higher will => less suppression).
            if (hasStats)
            {
                var will01 = math.saturate(stats.Will / 10f);
                raw *= math.lerp(1f, 0.5f, will01);
            }

            return math.saturate(raw);
        }

        public static bool ShouldSkipForChaos(uint token, float chaos01, float chaosSkipChanceMax)
        {
            var skipChance = math.saturate(chaos01) * math.saturate(chaosSkipChanceMax);
            if (skipChance <= 0f)
            {
                return false;
            }

            // Deterministic pseudo-random in [0,1).
            var h = math.hash(new uint2(token, 0xA17ACCAu));
            var r01 = (h & 0x00FFFFFFu) / 16777216f;
            return r01 < skipChance;
        }

        public static bool ShouldRequestReceiptAcks(
            in AIAckConfig config,
            float focusRatio01,
            float sleepPressure01,
            float chaos01,
            uint token)
        {
            if (config.Enabled == 0 || config.WantsReceiptAcks == 0)
            {
                return false;
            }

            if (focusRatio01 < math.max(0f, config.MinFocusRatio))
            {
                return false;
            }

            if (sleepPressure01 > math.max(0f, config.MaxSleepPressure))
            {
                return false;
            }

            return !ShouldSkipForChaos(token, chaos01, config.ChaosSkipChanceMax);
        }

        public static bool ShouldEmitReceiptAcks(
            in AIAckConfig config,
            float focusRatio01,
            float sleepPressure01,
            float chaos01,
            uint token)
        {
            if (config.Enabled == 0 || config.EmitsReceiptAcks == 0)
            {
                return false;
            }

            if (focusRatio01 < math.max(0f, config.MinFocusRatio))
            {
                return false;
            }

            if (sleepPressure01 > math.max(0f, config.MaxSleepPressure))
            {
                return false;
            }

            return !ShouldSkipForChaos(token ^ 0x51C0FFEEu, chaos01, config.ChaosSkipChanceMax);
        }
    }
}


