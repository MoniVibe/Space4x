using System.Runtime.CompilerServices;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Shared helper that enforces deterministic cadence gates for Body/Mind/Aggregate pillars.
    /// </summary>
    public static class CadenceGate
    {
        /// <summary>
        /// Returns true when the provided tick falls on the cadence boundary.
        /// A cadence &lt;= 1 means "run every tick". Values &lt;= 0 are clamped to 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldRun(uint currentTick, int cadenceTicks)
        {
            var cadence = math.max(1, cadenceTicks);
            return (currentTick % (uint)cadence) == 0u;
        }

        /// <summary>
        /// Convenience overload that reads the tick from a TimeState singleton.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldRun(in TimeState timeState, int cadenceTicks)
        {
            return ShouldRun(timeState.Tick, cadenceTicks);
        }

        /// <summary>
        /// Convenience overload that tries to fetch TimeState from the supplied system state.
        /// If TimeState is missing, the caller is allowed to run (so bootstraps are not blocked).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldRun(ref SystemState state, int cadenceTicks)
        {
            if (cadenceTicks <= 1)
            {
                return true;
            }

            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return true;
            }

            return ShouldRun(timeState.Tick, cadenceTicks);
        }
    }
}
