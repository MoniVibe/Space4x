using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Static service for formation tactic calculations.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public static class FormationTacticService
    {
        /// <summary>
        /// Executes a tactic on a formation.
        /// </summary>
        public static void ExecuteTactic(
            ref FormationTactic tactic,
            FormationTacticType tacticType,
            float3 targetPosition,
            Entity targetEntity,
            uint currentTick)
        {
            tactic.TacticType = tacticType;
            tactic.State = TacticState.Preparing;
            tactic.TacticStartTick = currentTick;
            tactic.TargetPosition = targetPosition;
            tactic.TargetEntity = targetEntity;
        }

        /// <summary>
        /// Gets current tactic for a formation.
        /// </summary>
        public static FormationTacticType GetCurrentTactic(in FormationTactic tactic)
        {
            return tactic.TacticType;
        }

        /// <summary>
        /// Gets tactic state for a formation.
        /// </summary>
        public static TacticState GetTacticState(in FormationTactic tactic)
        {
            return tactic.State;
        }

        /// <summary>
        /// Calculates movement pattern for a tactic.
        /// </summary>
        public static float3 CalculateTacticMovement(
            FormationTacticType tacticType,
            float3 currentPosition,
            float3 targetPosition,
            float deltaTime)
        {
            float3 direction = math.normalize(targetPosition - currentPosition);
            float speed = 5f; // Base movement speed

            return tacticType switch
            {
                FormationTacticType.Charge => direction * speed * 1.5f, // Faster charge
                FormationTacticType.Retreat => -direction * speed * 1.2f, // Faster retreat
                FormationTacticType.Flank => direction * speed * 0.8f, // Slower flanking
                FormationTacticType.Encircle => direction * speed * 0.9f, // Slower encirclement
                _ => direction * speed
            };
        }

        /// <summary>
        /// Checks if tactic is effective against formation type.
        /// </summary>
        public static bool IsTacticEffective(
            FormationTacticType tactic,
            PureDOTS.Runtime.Formation.FormationType targetFormation)
        {
            // Charge effective vs defensive formations
            if (tactic == FormationTacticType.Charge)
            {
                return targetFormation == PureDOTS.Runtime.Formation.FormationType.Defensive ||
                       targetFormation == PureDOTS.Runtime.Formation.FormationType.Phalanx;
            }

            // Flank effective vs phalanx/line
            if (tactic == FormationTacticType.Flank)
            {
                return targetFormation == PureDOTS.Runtime.Formation.FormationType.Phalanx ||
                       targetFormation == PureDOTS.Runtime.Formation.FormationType.Line;
            }

            return true; // Default: tactic is viable
        }
    }
}



