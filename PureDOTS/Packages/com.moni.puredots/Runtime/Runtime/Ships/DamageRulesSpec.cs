using Unity.Collections;

namespace PureDOTS.Runtime.Ships
{
    /// <summary>
    /// Damage kind enumeration - affects how damage interacts with armor/shields.
    /// </summary>
    public enum DamageKind : byte
    {
        Kinetic = 0,
        Energy = 1,
        Explosive = 2,
        Radiation = 3,
        Fire = 4
    }

    /// <summary>
    /// Damage rules blob - defines how damage flows through ship systems.
    /// </summary>
    public struct DamageRulesBlob
    {
        public float OverpenToModuleFrac; // Damage passing armor to modules
        public float OverkillToHullFrac; // Damage to hull after module 0 HP
        public float DestroyedModuleHullLeak; // Extra hull leak when hitting destroyed module
        public float LifeBoatThreshold; // % hull or Bridge destroyed to trigger lifeboats
    }

    /// <summary>
    /// Refit and repair rules blob - defines repair/refit constraints.
    /// </summary>
    public struct RefitRepairRulesBlob
    {
        public float FieldPenalty; // >1 slower in field
        public float BelowTechPenalty; // Multiplier per tier deficit
        public byte AllowBelowTech; // 0/1 flag
    }
}

