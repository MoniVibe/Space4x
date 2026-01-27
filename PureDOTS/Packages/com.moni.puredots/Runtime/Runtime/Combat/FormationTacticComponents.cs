using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Formation tactic - current tactical state.
    /// </summary>
    public struct FormationTactic : IComponentData
    {
        public FormationTacticType TacticType;
        public TacticState State;
        public uint TacticStartTick;
        public float3 TargetPosition;       // Target for tactic (e.g., flank position)
        public Entity TargetEntity;         // Target entity (e.g., enemy formation)
    }

    /// <summary>
    /// Formation tactic types.
    /// </summary>
    public enum FormationTacticType : byte
    {
        None = 0,
        Charge = 1,     // Aggressive forward assault
        Hold = 2,       // Defensive hold position
        Flank = 3,      // Flank enemy position
        Encircle = 4,   // Surround enemy
        Feint = 5,      // Fake attack/retreat
        Retreat = 6     // Fall back
    }

    /// <summary>
    /// Tactic state - execution state of tactic.
    /// </summary>
    public enum TacticState : byte
    {
        Idle = 0,
        Preparing = 1,  // Getting into position
        Executing = 2, // Actively executing
        Completing = 3, // Finishing execution
        Failed = 4     // Tactic failed
    }

    /// <summary>
    /// Tactic execution request.
    /// </summary>
    public struct TacticExecutionRequest : IComponentData
    {
        public FormationTacticType RequestedTactic;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public bool Immediate;              // Skip preparation phase
    }
}



