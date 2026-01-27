using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Movement
{
    /// <summary>
    /// Mapping blob that maps MovementKind to ExpertiseType in ExpertiseEntry system.
    /// Used by MovementStatCalcSystem to resolve pilot proficiency.
    /// </summary>
    public struct MovementSkillMapBlob
    {
        /// <summary>
        /// Expertise types indexed by MovementKind (cast to byte).
        /// </summary>
        public BlobArray<byte> ExpertiseTypesByKind; // Maps to ExpertiseType enum
    }

    /// <summary>
    /// Hot-cached pilot proficiency multipliers and modifiers.
    /// Computed from ExpertiseEntry + MovementSkillMapBlob by MovementStatCalcSystem.
    /// Updated when skills change (dirty tag).
    /// </summary>
    public struct PilotProficiency : IComponentData
    {
        public float ControlMult; // Scales acceleration (1.0 = baseline, >1.0 = veteran)
        public float TurnRateMult; // Scales turn rates (1.0 = baseline)
        public float EnergyMult; // Energy efficiency multiplier (<1.0 = more efficient)
        public float Jitter; // Erratic variance for novices (0 = perfect, >0 = random deviation)
        public float ReactionSec; // Delay before responding to commands (lower = faster)
    }
}

