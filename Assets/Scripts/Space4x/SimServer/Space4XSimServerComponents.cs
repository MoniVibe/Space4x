using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.SimServer
{
    public struct Space4XSimServerConfig : IComponentData
    {
        public uint Seed;
        public ushort FactionCount;
        public ushort SystemsPerFaction;
        public ushort ResourcesPerSystem;
        public float StartRadius;
        public float SystemSpacing;
        public float ResourceBaseUnits;
        public float ResourceRichnessGradient;
        public float TechDiffusionDurationSeconds;
        public float TargetTicksPerSecond;
        public ushort HttpPort;
        public float AutosaveSeconds;
        public GalaxySystemTraitMask TraitMask;
        public GalaxyPoiMask PoiMask;
        public byte MaxTraitsPerSystem;
        public byte MaxPoisPerSystem;
        public float TraitChanceBase;
        public float TraitChancePerRing;
        public float TraitChanceMax;
        public float PoiChanceBase;
        public float PoiChancePerRing;
        public float PoiChanceMax;
        public float PoiOffsetMin;
        public float PoiOffsetMax;
    }

    public struct Space4XSimServerTag : IComponentData { }

    public struct Space4XSimServerGalaxyBootstrapped : IComponentData { }

    public struct Space4XStarSystem : IComponentData
    {
        public ushort SystemId;
        public ushort OwnerFactionId;
        public byte RingIndex;
    }

    public struct Space4XFactionDirective : IComponentData
    {
        public float Security;
        public float Economy;
        public float Research;
        public float Expansion;
        public float Diplomacy;
        public float Production;
        public float Food;
        public float Priority;
        public uint LastUpdatedTick;
        public uint ExpiresAtTick;
        public FixedString64Bytes DirectiveId;
    }
}
