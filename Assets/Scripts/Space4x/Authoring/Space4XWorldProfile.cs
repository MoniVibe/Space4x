using Unity.Entities;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Space4X.Authoring
{
    /// <summary>
    /// ScriptableObject that defines which PureDOTS system groups and systems should be active
    /// for a Space4X game world. This allows games to explicitly control world composition
    /// instead of relying on default inclusion/exclusion filters.
    /// </summary>
    [CreateAssetMenu(menuName = "Space4X/World Profile", fileName = "Space4XWorldProfile")]
    public class Space4XWorldProfile : ScriptableObject
    {
        [Header("System Groups (Core DOTS Infrastructure)")]

        [Tooltip("Time and rewind systems")]
        public bool enableTimeGroup = true;

        [Tooltip("Resource management systems")]
        public bool enableResourceGroup = true;

        [Tooltip("Spatial partitioning and grid systems")]
        public bool enableSpatialGroup = true;

        [Tooltip("Physics simulation systems")]
        public bool enablePhysicsGroup = true;

        [Header("Space4X Game Systems")]

        [Tooltip("Registry systems for colonies, fleets, anomalies")]
        public bool enableRegistrySystems = true;

        [Tooltip("Mining and resource extraction systems")]
        public bool enableMiningSystems = true;

        [Tooltip("Fleet combat and interception systems")]
        public bool enableCombatSystems = true;

        [Tooltip("Tech diffusion and research systems")]
        public bool enableTechSystems = true;

        [Tooltip("Crew alignment and mutiny systems")]
        public bool enableCrewSystems = true;

        [Tooltip("Construction and building systems")]
        public bool enableConstructionSystems = true;

        [Tooltip("AI and decision-making systems")]
        public bool enableAISystems = true;

        [Header("Presentation & Rendering")]

        [Tooltip("Game-specific rendering systems")]
        public bool enableRenderingSystems = true;

        [Tooltip("UI and HUD systems")]
        public bool enableUISystems = true;

        [Header("Debug & Development")]

        [Tooltip("Debug visualization and telemetry systems")]
        public bool enableDebugSystems = true;

        /// <summary>
        /// Gets all system types that should be included in the world based on this profile.
        /// </summary>
        public IEnumerable<Type> GetIncludedSystems()
        {
            var includedSystems = new List<Type>();

            // Core DOTS groups
            if (enableTimeGroup)
            {
                includedSystems.Add(typeof(PureDOTS.Systems.TimeSystemGroup));
                includedSystems.Add(typeof(PureDOTS.Systems.HistorySystemGroup));
            }

            if (enableResourceGroup)
            {
                includedSystems.Add(typeof(PureDOTS.Systems.ResourceSystemGroup));
                includedSystems.Add(typeof(PureDOTS.Systems.PowerSystemGroup));
            }

            if (enableSpatialGroup)
            {
                includedSystems.Add(typeof(PureDOTS.Systems.SpatialSystemGroup));
                includedSystems.Add(typeof(PureDOTS.Systems.EnvironmentSystemGroup));
            }

            if (enablePhysicsGroup)
            {
                // Physics systems are typically auto-included by Unity
            }

            // Space4X specific systems
            // Space4X specific systems (commented until validated)
            // if (enableRegistrySystems)
            // {
            //     includedSystems.Add(typeof(Space4X.Registry.Space4XRegistryBootstrapSystem));
            // }

            // if (enableMiningSystems)
            // {
            //     includedSystems.Add(typeof(Space4X.Mining.Space4XMiningSystem));
            //     includedSystems.Add(typeof(Space4X.Mining.Space4XMiningYieldSystem));
            // }

            // if (enableCombatSystems)
            // {
            //     includedSystems.Add(typeof(Space4X.Combat.SpaceCombatSystem));
            // }

            if (enableTechSystems)
            {
                // Tech systems will be added here
            }

            if (enableCrewSystems)
            {
                // Crew systems will be added here
            }

            // if (enableConstructionSystems)
            // {
            //     includedSystems.Add(typeof(Space4X.Construction.Space4XConstructionBootstrap));
            //     includedSystems.Add(typeof(Space4X.Construction.Space4XBuildNeedSignalSystem));
            // }

            if (enableAISystems)
            {
                // AI systems will be added here
            }

            // if (enableRenderingSystems)
            // {
            //     // Rendering systems will be added here after validation
            //     //// includedSystems.Add(typeof(Space4X.Rendering.Space4XRenderCatalogSystem));
            //     //// includedSystems.Add(typeof(Space4X.Rendering.Space4XApplyRenderCatalogSystem));
            // }

            if (enableUISystems)
            {
                // UI systems will be added here
            }

            if (enableDebugSystems)
            {
                // Telemetry group removed for now (resolve namespace before re-adding)
                // includedSystems.Add(typeof(PureDOTS.Systems.TelemetrySystemGroup));
            }

            return includedSystems;
        }

        /// <summary>
        /// Gets system types that should be explicitly excluded from the world.
        /// </summary>
        public IEnumerable<Type> GetExcludedSystems()
        {
            var excludedSystems = new List<Type>();

            // Always exclude demo systems (none included in game profile)
            // Demo systems removed from game profile to avoid references
            excludedSystems.Add(typeof(PureDOTS.Systems.Hybrid.HybridControlToggleSystem));

            // Exclude village/miracle systems not relevant to Space4X
            if (!enableAISystems) // If AI is disabled, exclude village systems
            {
                excludedSystems.Add(typeof(PureDOTS.Systems.VillagerSystemGroup));
                excludedSystems.Add(typeof(PureDOTS.Systems.MiracleEffectSystemGroup));
            }

            return excludedSystems;
        }
    }
}
