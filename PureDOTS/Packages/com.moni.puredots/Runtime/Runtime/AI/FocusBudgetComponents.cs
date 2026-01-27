using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Simple focus budget tracker used by Mind/Body intent bridges.
    /// </summary>
    public struct FocusBudget : IComponentData
    {
        /// <summary>Current focus pool available to this agent.</summary>
        public float Current;

        /// <summary>Maximum focus pool.</summary>
        public float Max;

        /// <summary>Reserved focus that cannot be spent by new intents.</summary>
        public float Reserved;

        /// <summary>Regeneration applied each tick before new allocations.</summary>
        public float RegenPerTick;

        /// <summary>Non-zero when an external system locks the focus pool.</summary>
        public byte IsLocked;

        /// <summary>Amount of focus that can still be spent this tick.</summary>
        public readonly float Available => math.max(0f, Current - Reserved);

        /// <summary>Returns true when the requested focus can be reserved immediately.</summary>
        public readonly bool CanReserve(float amount)
        {
            return IsLocked == 0 && Available >= math.max(0f, amount);
        }
    }

    /// <summary>
    /// Tracks focus reservations per intent/system so they can be released deterministically.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct FocusBudgetReservation : IBufferElementData
    {
        /// <summary>Amount of focus held.</summary>
        public float Amount;

        /// <summary>Tick where the reservation expires (0 = never).</summary>
        public uint ExpirationTick;
    }
}
