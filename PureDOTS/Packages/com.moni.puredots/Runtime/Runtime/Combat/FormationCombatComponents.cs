using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Formation;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Formation combat type - maps to FormationType enum.
    /// </summary>
    public struct FormationCombatType : IComponentData
    {
        public FormationType Type;
    }

    /// <summary>
    /// Formation integrity - how well formation maintains structure (0-1).
    /// </summary>
    public struct FormationIntegrity : IComponentData
    {
        public float IntegrityPercent;      // 0.0 to 1.0
        public uint LastCalculatedTick;
        public byte MembersInPosition;      // Count of members in correct position
        public byte TotalMembers;            // Total formation members
    }

    /// <summary>
    /// Formation bonus - combat bonuses from formation (scaled by integrity).
    /// </summary>
    public struct FormationBonus : IComponentData
    {
        public float DefenseMultiplier;      // Applied to defense (e.g., 1.6 = +60%)
        public float AttackMultiplier;       // Applied to attack (e.g., 1.2 = +20%)
        public float MoraleMultiplier;      // Applied to morale (e.g., 1.3 = +30%)
    }

    /// <summary>
    /// Formation combat configuration - base bonuses per formation type.
    /// </summary>
    public struct FormationCombatConfig : IComponentData
    {
        public float BaseDefenseMultiplier;
        public float BaseAttackMultiplier;
        public float BaseMoraleMultiplier;
        public float IntegrityThreshold;    // Minimum integrity to receive bonuses (e.g., 0.3)
        public FormationType AppliedType;   // Formation type this config was created for (used to detect type changes)
    }
}

