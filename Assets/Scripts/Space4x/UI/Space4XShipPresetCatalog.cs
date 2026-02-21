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
            var timeShipProfile = CreateTimeShipProfile();
            var skipShipProfile = CreateSkipShipProfile();
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
                    Space4XShipPreviewShape.Capsule),
                new Space4XShipPresetEntry(
                    "ship.timeship.chronos",
                    "Timeship Chronos",
                    "Concept hull: no shields, brief time-stop bursts to reposition.",
                    Space4XShipPreviewShape.Cylinder,
                    timeShipProfile),
                new Space4XShipPresetEntry(
                    "ship.skipship.shift",
                    "Skipship Shift",
                    "Concept hull: no boost, instant short-range skip jumps.",
                    Space4XShipPreviewShape.Sphere,
                    skipShipProfile)
            };
        }

        internal static ShipFlightProfile CreateTimeShipProfile()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = 150f,
                MaxReverseSpeed = 92f,
                MaxStrafeSpeed = 74f,
                MaxVerticalSpeed = 64f,
                ForwardAcceleration = 124f,
                ReverseAcceleration = 110f,
                StrafeAcceleration = 94f,
                VerticalAcceleration = 82f,
                BoostMultiplier = 1.35f,
                PassiveDriftDrag = 0.03f,
                DampenerDeceleration = 92f,
                RetroBrakeAcceleration = 132f,
                RollSpeedDegrees = 72f,
                CursorTurnSharpness = 11.5f,
                MaxAngularSpeedDegrees = 30f,
                AngularAccelerationDegrees = 92f,
                AngularDampingDegrees = 105f,
                AngularDeadbandDegrees = 0.55f,
                MaxCursorLeadDegrees = 148f,
                TurnAuthorityAtMaxSpeed = 0.52f,
                AngularOvershootRatio = 0.16f,
                MaxCursorPitchDegrees = 68f,
                DefaultInertialDampenersEnabled = 1
            };
        }

        internal static ShipFlightProfile CreateSkipShipProfile()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = 240f,
                MaxReverseSpeed = 140f,
                MaxStrafeSpeed = 150f,
                MaxVerticalSpeed = 130f,
                ForwardAcceleration = 200f,
                ReverseAcceleration = 176f,
                StrafeAcceleration = 182f,
                VerticalAcceleration = 168f,
                BoostMultiplier = 1f,
                PassiveDriftDrag = 0.01f,
                DampenerDeceleration = 40f,
                RetroBrakeAcceleration = 78f,
                RollSpeedDegrees = 130f,
                CursorTurnSharpness = 15f,
                MaxAngularSpeedDegrees = 44f,
                AngularAccelerationDegrees = 142f,
                AngularDampingDegrees = 156f,
                AngularDeadbandDegrees = 0.4f,
                MaxCursorLeadDegrees = 172f,
                TurnAuthorityAtMaxSpeed = 0.64f,
                AngularOvershootRatio = 0.08f,
                MaxCursorPitchDegrees = 74f,
                DefaultInertialDampenersEnabled = 0
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
                2 => new Space4XShipPresetEntry(
                    "ship.capsule.interceptor",
                    "Capsule Interceptor",
                    "Fallback preset: agile starter hull.",
                    Space4XShipPreviewShape.Capsule),
                3 => new Space4XShipPresetEntry(
                    "ship.timeship.chronos",
                    "Timeship Chronos",
                    "Fallback preset: temporal hull.",
                    Space4XShipPreviewShape.Cylinder,
                    Space4XShipPresetCatalog.CreateTimeShipProfile()),
                _ => new Space4XShipPresetEntry(
                    "ship.skipship.shift",
                    "Skipship Shift",
                    "Fallback preset: skip-jump hull.",
                    Space4XShipPreviewShape.Sphere,
                    Space4XShipPresetCatalog.CreateSkipShipProfile())
            };
        }
    }
}
