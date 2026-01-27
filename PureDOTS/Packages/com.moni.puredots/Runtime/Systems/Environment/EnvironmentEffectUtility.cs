using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    public static class EnvironmentEffectUtility
    {
        public static uint TickDelta(uint current, uint last)
        {
            return current >= last
                ? current - last
                : (uint.MaxValue - last) + current + 1u;
        }

        public static bool ShouldUpdate(uint current, uint last, uint stride)
        {
            if (current == last)
            {
                return false;
            }

            if (last == uint.MaxValue)
            {
                return true;
            }

            if (stride <= 1u)
            {
                return true;
            }

            var delta = TickDelta(current, last);
            return delta >= stride;
        }

        public static float WrapHours(float hours)
        {
            hours %= 24f;
            return hours < 0f ? hours + 24f : hours;
        }
    }
}
