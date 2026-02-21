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
            var carrierSegments = new[] { "carrier-bridge-m1", "carrier-keel-m1", "carrier-stern-m1" };
            var escortSegments = new[] { "escort-bridge-s1", "escort-stern-s1" };
            var chronoSegments = new[] { "segment-command-general-s1", "segment-research-general-s1", "segment-production-general-s1" };
            var carrierModules = new[] { "reactor-mk1", "engine-mk1", "hangar-s-1", "shield-s-1", "armor-s-1", "pd-s-1", "scanner-s-1" };
            var frigateModules = new[] { "reactor-mk1", "engine-mk1", "laser-s-1", "shield-s-1", "repair-s-1", "scanner-s-1" };
            var interceptorModules = new[] { "reactor-mk1", "engine-mk1", "missile-s-1", "laser-s-1", "armor-s-1", "scanner-s-1" };
            var chronoModules = new[] { "reactor-mk1", "engine-mk1", "scanner-s-1", "shield-s-1", "repair-s-1" };
            var skipModules = new[] { "reactor-mk1", "engine-mk1", "laser-s-1", "pd-s-1", "scanner-s-1" };
            presets = new[]
            {
                new Space4XShipPresetEntry(
                    "ship.square.carrier",
                    "Square Carrier",
                    "Heavy frame, stable handling, strong opening survivability.",
                    previewShape: Space4XShipPreviewShape.Cube,
                    archetype: "Bulwark Carrier",
                    hullSegments: carrierSegments,
                    startingModules: carrierModules,
                    metaPerks: new[] { "Reinforced bulkheads", "Emergency hangar launch", "Shield warm start" }),
                new Space4XShipPresetEntry(
                    "ship.sphere.frigate",
                    "Sphere Frigate",
                    "Balanced profile with agile turn response and mid-range control.",
                    previewShape: Space4XShipPreviewShape.Sphere,
                    archetype: "Balanced Frigate",
                    hullSegments: escortSegments,
                    startingModules: frigateModules,
                    metaPerks: new[] { "Targeting uplink", "Adaptive thrusters", "Reserve capacitors" }),
                new Space4XShipPresetEntry(
                    "ship.capsule.interceptor",
                    "Capsule Interceptor",
                    "Fast acceleration with tighter margins and higher execution demand.",
                    previewShape: Space4XShipPreviewShape.Capsule,
                    archetype: "Strike Interceptor",
                    hullSegments: escortSegments,
                    startingModules: interceptorModules,
                    metaPerks: new[] { "Overclocked burst", "Evasive blink", "Kill chain bonus" }),
                new Space4XShipPresetEntry(
                    "ship.timeship.chronos",
                    "Timeship Chronos",
                    "Concept hull: no shields, brief time-stop bursts to reposition.",
                    previewShape: Space4XShipPreviewShape.Cylinder,
                    flightProfile: timeShipProfile,
                    archetype: "Temporal Vanguard",
                    hullSegments: chronoSegments,
                    startingModules: chronoModules,
                    metaPerks: new[] { "Chrono charge", "Phase anchor", "Stasis wake" }),
                new Space4XShipPresetEntry(
                    "ship.skipship.shift",
                    "Skipship Shift",
                    "Concept hull: no boost, instant short-range skip jumps.",
                    previewShape: Space4XShipPreviewShape.Sphere,
                    flightProfile: skipShipProfile,
                    archetype: "Skip Skirmisher",
                    hullSegments: escortSegments,
                    startingModules: skipModules,
                    metaPerks: new[] { "Skip echo", "Drift stabilizers", "Blink reserve" })
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
        [SerializeField] private string archetype;
        [SerializeField] private string[] hullSegments;
        [SerializeField] private string[] startingModules;
        [SerializeField] private string[] metaPerks;
        [SerializeField] private Space4XShipPreviewShape previewShape;
        [SerializeField] private ShipFlightProfile flightProfile;

        public Space4XShipPresetEntry(
            string presetId,
            string displayName,
            string description,
            Space4XShipPreviewShape previewShape = Space4XShipPreviewShape.Cube,
            ShipFlightProfile flightProfile = default,
            string archetype = null,
            string[] hullSegments = null,
            string[] startingModules = null,
            string[] metaPerks = null)
        {
            this.presetId = presetId;
            this.displayName = displayName;
            this.description = description;
            this.archetype = archetype;
            this.hullSegments = hullSegments;
            this.startingModules = startingModules;
            this.metaPerks = metaPerks;
            this.previewShape = previewShape;
            this.flightProfile = flightProfile;
        }

        public string PresetId => string.IsNullOrWhiteSpace(presetId) ? "ship.unknown" : presetId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? PresetId : displayName;
        public string Description => description ?? string.Empty;
        public string Archetype => string.IsNullOrWhiteSpace(archetype) ? "Unknown Archetype" : archetype;
        public string[] HullSegments => hullSegments ?? Array.Empty<string>();
        public string[] StartingModules => startingModules ?? Array.Empty<string>();
        public string[] MetaPerks => metaPerks ?? Array.Empty<string>();
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
                    previewShape: Space4XShipPreviewShape.Cube,
                    archetype: "Bulwark Carrier",
                    hullSegments: new[] { "carrier-bridge-m1", "carrier-keel-m1", "carrier-stern-m1" },
                    startingModules: new[] { "reactor-mk1", "engine-mk1", "hangar-s-1", "shield-s-1", "armor-s-1", "pd-s-1", "scanner-s-1" },
                    metaPerks: new[] { "Reinforced bulkheads", "Emergency hangar launch", "Shield warm start" }),
                1 => new Space4XShipPresetEntry(
                    "ship.sphere.frigate",
                    "Sphere Frigate",
                    "Fallback preset: balanced starter hull.",
                    previewShape: Space4XShipPreviewShape.Sphere,
                    archetype: "Balanced Frigate",
                    hullSegments: new[] { "escort-bridge-s1", "escort-stern-s1" },
                    startingModules: new[] { "reactor-mk1", "engine-mk1", "laser-s-1", "shield-s-1", "repair-s-1", "scanner-s-1" },
                    metaPerks: new[] { "Targeting uplink", "Adaptive thrusters", "Reserve capacitors" }),
                2 => new Space4XShipPresetEntry(
                    "ship.capsule.interceptor",
                    "Capsule Interceptor",
                    "Fallback preset: agile starter hull.",
                    previewShape: Space4XShipPreviewShape.Capsule,
                    archetype: "Strike Interceptor",
                    hullSegments: new[] { "escort-bridge-s1", "escort-stern-s1" },
                    startingModules: new[] { "reactor-mk1", "engine-mk1", "missile-s-1", "laser-s-1", "armor-s-1", "scanner-s-1" },
                    metaPerks: new[] { "Overclocked burst", "Evasive blink", "Kill chain bonus" }),
                3 => new Space4XShipPresetEntry(
                    "ship.timeship.chronos",
                    "Timeship Chronos",
                    "Fallback preset: temporal hull.",
                    previewShape: Space4XShipPreviewShape.Cylinder,
                    flightProfile: Space4XShipPresetCatalog.CreateTimeShipProfile(),
                    archetype: "Temporal Vanguard",
                    hullSegments: new[] { "segment-command-general-s1", "segment-research-general-s1", "segment-production-general-s1" },
                    startingModules: new[] { "reactor-mk1", "engine-mk1", "scanner-s-1", "shield-s-1", "repair-s-1" },
                    metaPerks: new[] { "Chrono charge", "Phase anchor", "Stasis wake" }),
                _ => new Space4XShipPresetEntry(
                    "ship.skipship.shift",
                    "Skipship Shift",
                    "Fallback preset: skip-jump hull.",
                    previewShape: Space4XShipPreviewShape.Sphere,
                    flightProfile: Space4XShipPresetCatalog.CreateSkipShipProfile(),
                    archetype: "Skip Skirmisher",
                    hullSegments: new[] { "escort-bridge-s1", "escort-stern-s1" },
                    startingModules: new[] { "reactor-mk1", "engine-mk1", "laser-s-1", "pd-s-1", "scanner-s-1" },
                    metaPerks: new[] { "Skip echo", "Drift stabilizers", "Blink reserve" })
            };
        }
    }
}
