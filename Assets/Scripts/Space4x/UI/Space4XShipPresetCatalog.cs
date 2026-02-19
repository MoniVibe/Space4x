using System;
using System.Collections.Generic;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.UI
{
    public enum Space4XShipPreviewShape : byte
    {
        Cube = 0,
        Sphere = 1,
        Capsule = 2,
        Cylinder = 3
    }

    [CreateAssetMenu(fileName = "Space4XShipPresetCatalog", menuName = "Space4X/UI/Ship Preset Catalog")]
    public sealed class Space4XShipPresetCatalog : ScriptableObject
    {
        public const string DefaultGameplayScenePath = "Assets/Scenes/Demos/MiningCombatDemo.unity";

        [SerializeField] private string gameplayScenePath = DefaultGameplayScenePath;
        [SerializeField] private int minDifficulty = 1;
        [SerializeField] private int maxDifficulty = 5;
        [SerializeField] private int defaultDifficulty = 2;
        [SerializeField] private Space4XShipPresetEntry[] presets = Array.Empty<Space4XShipPresetEntry>();

        public string GameplayScenePath => gameplayScenePath;
        public int MinDifficulty => Math.Min(minDifficulty, maxDifficulty);
        public int MaxDifficulty => Math.Max(minDifficulty, maxDifficulty);
        public int DefaultDifficulty => ClampDifficulty(defaultDifficulty);
        public int PresetCount => presets?.Length ?? 0;
        public bool HasPresets => PresetCount > 0;

        public IReadOnlyList<Space4XShipPresetEntry> Presets => presets;

        public int ClampDifficulty(int value)
        {
            return Mathf.Clamp(value, MinDifficulty, MaxDifficulty);
        }

        public Space4XShipPresetEntry GetPresetOrFallback(int index)
        {
            if (!HasPresets)
                return Space4XShipPresetEntry.CreateFallback(index);

            var safeIndex = Mathf.Clamp(index, 0, presets.Length - 1);
            var preset = presets[safeIndex];
            return preset.IsValid ? preset : Space4XShipPresetEntry.CreateFallback(safeIndex);
        }

        public static Space4XShipPresetCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XShipPresetCatalog>();
            catalog.ApplyDefaultFleetCrawlSlice();
            return catalog;
        }

        public void ApplyDefaultFleetCrawlSlice()
        {
            gameplayScenePath = DefaultGameplayScenePath;
            minDifficulty = 1;
            maxDifficulty = 5;
            defaultDifficulty = 2;
            presets = new[]
            {
                new Space4XShipPresetEntry(
                    "ship.square.carrier",
                    "Square Carrier",
                    "Heavy frame, stable handling, strong opening survivability.",
                    Space4XShipPreviewShape.Cube),
                new Space4XShipPresetEntry(
                    "ship.sphere.frigate",
                    "Sphere Frigate",
                    "Balanced profile with agile turn response and mid-range control.",
                    Space4XShipPreviewShape.Sphere),
                new Space4XShipPresetEntry(
                    "ship.capsule.interceptor",
                    "Capsule Interceptor",
                    "Fast acceleration with tighter margins and higher execution demand.",
                    Space4XShipPreviewShape.Capsule)
            };
        }
    }

    [Serializable]
    public struct Space4XShipPresetEntry
    {
        [SerializeField] private string presetId;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea(2, 4)] private string description;
        [SerializeField] private Space4XShipPreviewShape previewShape;
        [SerializeField] private ShipFlightProfile flightProfile;

        public Space4XShipPresetEntry(
            string presetId,
            string displayName,
            string description,
            Space4XShipPreviewShape previewShape = Space4XShipPreviewShape.Cube,
            ShipFlightProfile flightProfile = default)
        {
            this.presetId = presetId;
            this.displayName = displayName;
            this.description = description;
            this.previewShape = previewShape;
            this.flightProfile = flightProfile;
        }

        public string PresetId => string.IsNullOrWhiteSpace(presetId) ? "ship.unknown" : presetId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? PresetId : displayName;
        public string Description => description ?? string.Empty;
        public Space4XShipPreviewShape PreviewShape => previewShape;
        public ShipFlightProfile FlightProfile => flightProfile.IsConfigured
            ? flightProfile.Sanitized()
            : ShipFlightProfile.CreateDefault(PresetId);
        public bool IsValid => !string.IsNullOrWhiteSpace(presetId) || !string.IsNullOrWhiteSpace(displayName);

        public static Space4XShipPresetEntry CreateFallback(int index)
        {
            return index switch
            {
                0 => new Space4XShipPresetEntry(
                    "ship.square.carrier",
                    "Square Carrier",
                    "Fallback preset: heavy starter hull.",
                    Space4XShipPreviewShape.Cube),
                1 => new Space4XShipPresetEntry(
                    "ship.sphere.frigate",
                    "Sphere Frigate",
                    "Fallback preset: balanced starter hull.",
                    Space4XShipPreviewShape.Sphere),
                _ => new Space4XShipPresetEntry(
                    "ship.capsule.interceptor",
                    "Capsule Interceptor",
                    "Fallback preset: agile starter hull.",
                    Space4XShipPreviewShape.Capsule)
            };
        }
    }
}
