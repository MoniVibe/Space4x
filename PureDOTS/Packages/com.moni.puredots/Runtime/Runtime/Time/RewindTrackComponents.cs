using Unity.Collections;
using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Identifier for a rewind track (0-255, assigned by modders/content).
    /// Each track represents a domain of rewindable state (combat, villages, fire, ships, etc.).
    /// </summary>
    public struct RewindTrackId
    {
        public byte Value;
    }

    /// <summary>
    /// Definition for a rewind track, stored in blob asset.
    /// </summary>
    public struct RewindTrackDef
    {
        public RewindTrackId Id;
        public FixedString32Bytes Name;
        public RewindTier Tier;
        public uint RecordEveryTicks;
        public uint WindowTicks;
        public bool Spatial;
    }

    /// <summary>
    /// Blob asset containing all rewind track definitions.
    /// Merged from multiple config assets at bootstrap.
    /// </summary>
    public struct RewindConfigBlob
    {
        public BlobArray<RewindTrackDef> Tracks;
    }

    /// <summary>
    /// Singleton component holding the rewind configuration blob.
    /// Created by RewindConfigBootstrapSystem from authoring configs.
    /// </summary>
    public struct RewindConfigSingleton : IComponentData
    {
        public BlobAssetReference<RewindConfigBlob> Config;
    }
}

