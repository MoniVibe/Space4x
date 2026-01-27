#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Motivation;
using UnityEngine;

namespace PureDOTS.Authoring.Motivation
{
    /// <summary>
    /// ScriptableObject for defining individual motivation specs.
    /// Used to build motivation catalogs.
    /// </summary>
    [CreateAssetMenu(fileName = "MotivationSpec", menuName = "PureDOTS/Motivation/Motivation Spec", order = 100)]
    public class MotivationSpecAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this motivation spec.")]
        public short SpecId = -1;

        [Header("Type")]
        [Tooltip("Layer/type of this motivation.")]
        public MotivationLayer Layer = MotivationLayer.Dream;

        [Tooltip("Scope (individual, aggregate, or either).")]
        public MotivationScope Scope = MotivationScope.Individual;

        [Tooltip("High-level category tag.")]
        public MotivationTag Tag = MotivationTag.None;

        [Header("Properties")]
        [Tooltip("Baseline importance (0-255).")]
        [Range(0, 255)]
        public byte BaseImportance = 100;

        [Tooltip("How expensive it is in initiative to pursue (0-255).")]
        [Range(0, 255)]
        public byte BaseInitiativeCost = 50;

        [Tooltip("How many entities can hold this at once (0 = unlimited).")]
        [Range(0, 255)]
        public byte MaxConcurrentHolders = 0;

        [Tooltip("Minimum loyalty required to prioritize this (0-200).")]
        [Range(0, 200)]
        public byte RequiredLoyalty = 0;

        [Header("Alignment Requirements")]
        [Tooltip("Minimum corrupt/pure alignment (-100..100).")]
        [Range(-100, 100)]
        public sbyte MinCorruptPure = -100;

        [Tooltip("Minimum lawful/chaotic alignment (-100..100).")]
        [Range(-100, 100)]
        public sbyte MinLawChaos = -100;

        [Tooltip("Minimum good/evil alignment (-100..100).")]
        [Range(-100, 100)]
        public sbyte MinGoodEvil = -100;

        /// <summary>
        /// Converts to MotivationSpec struct.
        /// </summary>
        public MotivationSpec ToSpec()
        {
            return new MotivationSpec
            {
                SpecId = SpecId,
                Layer = Layer,
                Scope = Scope,
                Tag = Tag,
                BaseImportance = BaseImportance,
                BaseInitiativeCost = BaseInitiativeCost,
                MaxConcurrentHolders = MaxConcurrentHolders,
                RequiredLoyalty = RequiredLoyalty,
                MinCorruptPure = MinCorruptPure,
                MinLawChaos = MinLawChaos,
                MinGoodEvil = MinGoodEvil
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (SpecId < 0)
            {
                Debug.LogWarning($"[MotivationSpecAsset] {name} has invalid SpecId ({SpecId}). Must be >= 0.");
            }
        }
#endif
    }
}
#endif
























