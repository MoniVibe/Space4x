using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Helper utilities for applying sense-organ modifiers to perception channels.
    /// </summary>
    public static class PerceptionOrganUtilities
    {
        public static float GetMaxRangeMultiplier(PerceptionChannel enabledChannels, DynamicBuffer<SenseOrganState> organs)
        {
            if (!organs.IsCreated || organs.Length == 0 || enabledChannels == PerceptionChannel.None)
            {
                return 1f;
            }

            float max = 1f;
            for (int i = 0; i < organs.Length; i++)
            {
                var organ = organs[i];
                if ((organ.Channels & enabledChannels) == 0)
                {
                    continue;
                }

                var multiplier = math.max(0f, organ.RangeMultiplier);
                max = math.max(max, multiplier);
            }

            return max;
        }

        public static void GetChannelModifiers(
            PerceptionChannel channel,
            DynamicBuffer<SenseOrganState> organs,
            out float rangeMultiplier,
            out float acuityMultiplier,
            out float noiseFloor)
        {
            rangeMultiplier = 1f;
            acuityMultiplier = 1f;
            noiseFloor = 0f;

            if (!organs.IsCreated || organs.Length == 0 || channel == PerceptionChannel.None)
            {
                return;
            }

            for (int i = 0; i < organs.Length; i++)
            {
                var organ = organs[i];
                if ((organ.Channels & channel) == 0)
                {
                    continue;
                }

                rangeMultiplier = math.max(rangeMultiplier, organ.RangeMultiplier);
                acuityMultiplier = math.max(acuityMultiplier, math.max(0f, organ.Gain) * math.max(0f, organ.Condition));
                noiseFloor = math.max(noiseFloor, organ.NoiseFloor);
            }
        }
    }
}
