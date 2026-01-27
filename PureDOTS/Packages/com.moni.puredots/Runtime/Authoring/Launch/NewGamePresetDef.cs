using PureDOTS.Runtime.Launch;
using UnityEngine;

namespace PureDOTS.Authoring.Launch
{
    [CreateAssetMenu(fileName = "NewGamePreset", menuName = "PureDOTS/Launch/New Game Preset", order = 20)]
    public sealed class NewGamePresetDef : ScriptableObject
    {
        [Header("Metadata")]
        public string displayName = "New Game";
        [TextArea(2, 6)] public string description;

        [Header("Seed")]
        [Tooltip("0 = choose at runtime (menu-level random).")]
        public uint seed;

        [Header("Difficulty")]
        public DifficultyPreset difficultyPreset = DifficultyPreset.Normal;
        [Range(0f, 1f)] public float customDifficulty01 = 0.5f;

        [Header("Density")]
        public DensityPreset densityPreset = DensityPreset.Normal;
        [Range(0f, 1f)] public float customDensity01 = 0.5f;

        [Header("World Size")]
        public WorldGenSizePreset worldSizePreset = WorldGenSizePreset.Medium;
        [Range(0f, 1f)] public float customWorldSize01 = 0.5f;

        public NewGameDraft ToDraft()
        {
            return new NewGameDraft
            {
                Seed = seed,
                DifficultyPreset = difficultyPreset,
                CustomDifficulty01 = customDifficulty01,
                DensityPreset = densityPreset,
                CustomDensity01 = customDensity01,
                WorldSizePreset = worldSizePreset,
                CustomWorldSize01 = customWorldSize01
            };
        }
    }
}

