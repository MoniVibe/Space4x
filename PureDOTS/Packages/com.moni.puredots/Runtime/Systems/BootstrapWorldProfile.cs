#nullable enable
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Describes a set of DOTS systems that should be instantiated for a world.
    /// Profiles mirror the legacy DOTS Sample bootstrap behaviour but target Entities 1.x APIs.
    /// </summary>
    public readonly struct BootstrapWorldProfile
    {
        public string Id { get; }
        public string DisplayName { get; }
        public WorldSystemFilterFlags FilterFlags { get; }
        public IReadOnlyList<Type> ForcedInclusions { get; }
        public IReadOnlyList<Type> Exclusions { get; }
        public Predicate<Type>? AdditionalFilter { get; }

        public BootstrapWorldProfile(
            string id,
            string displayName,
            WorldSystemFilterFlags filterFlags,
            IReadOnlyList<Type>? forcedInclusions = null,
            IReadOnlyList<Type>? exclusions = null,
            Predicate<Type>? additionalFilter = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Profile id cannot be null or whitespace", nameof(id));

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            FilterFlags = filterFlags;
            ForcedInclusions = forcedInclusions ?? Array.Empty<Type>();
            Exclusions = exclusions ?? Array.Empty<Type>();
            AdditionalFilter = additionalFilter;
        }

        public bool ShouldInclude(Type systemType)
        {
            if (systemType == null)
                return false;

            foreach (var excluded in Exclusions)
            {
                if (excluded == systemType)
                    return false;
            }

            return AdditionalFilter?.Invoke(systemType) ?? true;
        }
    }
}


