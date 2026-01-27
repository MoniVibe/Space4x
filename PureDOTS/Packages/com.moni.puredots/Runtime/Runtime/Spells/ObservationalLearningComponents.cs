using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Component marking entities that can observe and learn from spell casts.
    /// </summary>
    public struct SpellObserver : IComponentData
    {
        /// <summary>
        /// Maximum range for observing spell casts.
        /// </summary>
        public float ObservationRange;

        /// <summary>
        /// Learning rate multiplier (1.0 = normal, higher = learns faster).
        /// </summary>
        public float LearningRate;

        /// <summary>
        /// Maximum number of spells that can be observed simultaneously.
        /// </summary>
        public byte MaxSimultaneousObserve;

        /// <summary>
        /// Tick when observer was last updated.
        /// </summary>
        public uint LastObserveTick;
    }

    /// <summary>
    /// Record of an observed spell cast.
    /// Used to grant XP toward spell mastery.
    /// </summary>
    public struct ObservedSpellCast : IBufferElementData
    {
        /// <summary>
        /// Spell identifier that was observed.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Entity that cast the spell.
        /// </summary>
        public Entity CasterEntity;

        /// <summary>
        /// Tick when spell was observed.
        /// </summary>
        public uint ObserveTick;

        /// <summary>
        /// Quality factor (0-1) based on caster's mastery.
        /// Higher quality = more XP gained.
        /// </summary>
        public float QualityFactor;

        /// <summary>
        /// Position where spell was cast (for range calculations).
        /// </summary>
        public float3 CastPosition;
    }

    /// <summary>
    /// Component marking entities that are currently casting spells (observable).
    /// </summary>
    public struct ObservableCaster : IComponentData
    {
        /// <summary>
        /// Visibility range for observers.
        /// </summary>
        public float VisibilityRange;

        /// <summary>
        /// Whether this caster's spells can be learned through observation.
        /// </summary>
        public bool CanBeObserved;
    }
}

