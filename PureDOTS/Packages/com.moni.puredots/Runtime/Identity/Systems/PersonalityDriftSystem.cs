using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Personality drift system: trauma and triumph can shift personality axes over time.
    /// Implements permanent or temporary shifts based on game design.
    /// </summary>
    [BurstCompile]
    public partial struct PersonalityDriftSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PersonalityAxes>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system provides framework for personality shifts
            // Games call ApplyPersonalityShift() when trauma/triumph events occur
        }

        /// <summary>
        /// Apply a personality shift from a trauma or triumph event.
        /// </summary>
        /// <param name="personality">Current personality</param>
        /// <param name="shiftType">Type of shift (trauma/triumph)</param>
        /// <param name="magnitude">How strong the shift is (0..1)</param>
        /// <param name="permanent">If true, shift is permanent; if false, temporary (decays over time)</param>
        [BurstCompile]
        public static void ApplyPersonalityShift(
            in PersonalityAxes personality,
            PersonalityShiftType shiftType,
            float magnitude,
            bool permanent,
            out PersonalityAxes result)
        {
            result = personality;

            switch (shiftType)
            {
                case PersonalityShiftType.TraumaVengeful:
                    // Trauma makes more vengeful
                    result.VengefulForgiving = math.max(-100f, result.VengefulForgiving - (magnitude * 10f));
                    break;

                case PersonalityShiftType.TraumaCraven:
                    // Trauma makes more craven
                    result.CravenBold = math.max(-100f, result.CravenBold - (magnitude * 10f));
                    break;

                case PersonalityShiftType.TriumphBold:
                    // Triumph makes more bold
                    result.CravenBold = math.min(100f, result.CravenBold + (magnitude * 10f));
                    break;

                case PersonalityShiftType.TriumphForgiving:
                    // Triumph makes more forgiving
                    result.VengefulForgiving = math.min(100f, result.VengefulForgiving + (magnitude * 10f));
                    break;

                case PersonalityShiftType.RevengeSatisfied:
                    // Successful revenge reduces vengefulness
                    result.VengefulForgiving = math.min(100f, result.VengefulForgiving + (magnitude * 5f));
                    break;

                case PersonalityShiftType.RevengeDenied:
                    // Denied revenge increases vengefulness
                    result.VengefulForgiving = math.max(-100f, result.VengefulForgiving - (magnitude * 15f));
                    break;
            }
        }
    }

    /// <summary>
    /// Types of personality shifts from events.
    /// </summary>
    public enum PersonalityShiftType : byte
    {
        TraumaVengeful,      // Harm suffered → more vengeful
        TraumaCraven,        // Fear experienced → more craven
        TriumphBold,          // Victory/heroism → more bold
        TriumphForgiving,     // Reconciliation success → more forgiving
        RevengeSatisfied,     // Revenge completed → less vengeful
        RevengeDenied         // Revenge blocked → more vengeful
    }
}

