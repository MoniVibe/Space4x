#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject for defining a single rewind track.
    /// </summary>
    [Serializable]
    public sealed class RewindTrackDefAsset : ScriptableObject
    {
        [Header("Track Identity")]
        [SerializeField, Tooltip("Track ID (0-255, assigned by modders/content)")]
        [Range(0, 255)]
        private byte trackId = 0;

        [SerializeField, Tooltip("Track name (e.g., 'Combat', 'Village', 'Fire')")]
        private string trackName = "Unnamed";

        [Header("Tier & Sampling")]
        [SerializeField, Tooltip("Rewind tier (Derived/Lite/Full)")]
        private RewindTier tier = RewindTier.SnapshotFull;

        [SerializeField, Tooltip("Record snapshot every N ticks (1 = every tick, 10 = every 10 ticks)")]
        [Range(1, 1000)]
        private uint recordEveryTicks = 1;

        [SerializeField, Tooltip("How far back we can rewind on this track (in ticks)")]
        [Range(1, 100000)]
        private uint windowTicks = 3600;

        [Header("Spatial")]
        [SerializeField, Tooltip("If true, only entities in zones are recorded")]
        private bool spatial = false;

        public byte TrackId => trackId;
        public string TrackName => trackName;
        public RewindTier Tier => tier;
        public uint RecordEveryTicks => recordEveryTicks;
        public uint WindowTicks => windowTicks;
        public bool Spatial => spatial;

#if UNITY_EDITOR
        private void OnValidate()
        {
            recordEveryTicks = (uint)Mathf.Max(1, (int)recordEveryTicks);
            windowTicks = (uint)Mathf.Max(1, (int)windowTicks);
        }
#endif
    }

    /// <summary>
    /// ScriptableObject asset containing multiple rewind track definitions.
    /// Multiple assets can be merged at bootstrap.
    /// </summary>
    [CreateAssetMenu(fileName = "RewindConfig", menuName = "PureDOTS/Time/Rewind Config", order = 103)]
    public sealed class RewindConfigAsset : ScriptableObject
    {
        [SerializeField, Tooltip("List of track definitions")]
        private List<RewindTrackDefAsset> tracks = new List<RewindTrackDefAsset>();

        public IReadOnlyList<RewindTrackDefAsset> Tracks => tracks;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Validate track IDs are unique within this asset
            var seenIds = new HashSet<byte>();
            foreach (var track in tracks)
            {
                if (track == null)
                    continue;
                if (seenIds.Contains(track.TrackId))
                {
                    Debug.LogWarning($"Duplicate track ID {track.TrackId} in {name}. Track IDs must be unique.");
                }
                seenIds.Add(track.TrackId);
            }
        }
#endif
    }

    /// <summary>
    /// Authoring component for rewind configuration.
    /// Add to a GameObject in a subscene to configure rewind tracks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewindConfigAuthoring : MonoBehaviour
    {
        [Header("Rewind Track Configuration")]
        [SerializeField, Tooltip("Optional RewindConfig asset. If null, uses defaults.")]
        private RewindConfigAsset rewindConfigAsset;

        [Header("Inline Tracks (if no asset)")]
        [SerializeField, Tooltip("Inline track definitions")]
        private List<RewindTrackDefAsset> inlineTracks = new List<RewindTrackDefAsset>();

        public RewindConfigAsset RewindConfigAsset => rewindConfigAsset;
        public IReadOnlyList<RewindTrackDefAsset> InlineTracks => inlineTracks;

        /// <summary>
        /// Gets all track definitions from asset and inline tracks.
        /// </summary>
        public List<RewindTrackDefAsset> GetAllTracks()
        {
            var allTracks = new List<RewindTrackDefAsset>();
            if (rewindConfigAsset != null && rewindConfigAsset.Tracks != null)
            {
                allTracks.AddRange(rewindConfigAsset.Tracks);
            }
            if (inlineTracks != null)
            {
                allTracks.AddRange(inlineTracks);
            }
            return allTracks;
        }
    }

    /// <summary>
    /// Baker for RewindConfigAuthoring.
    /// Collects all track definitions and marks entity for bootstrap processing.
    /// </summary>
    public sealed class RewindConfigAuthoringBaker : Baker<RewindConfigAuthoring>
    {
        public override void Bake(RewindConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Store track definitions in a buffer for bootstrap system to process
            var tracks = authoring.GetAllTracks();
            if (tracks.Count > 0)
            {
                var trackBuffer = AddBuffer<RewindConfigTrackEntry>(entity);
                foreach (var trackAsset in tracks)
                {
                    if (trackAsset == null)
                        continue;
                    
                    trackBuffer.Add(new RewindConfigTrackEntry
                    {
                        TrackId = trackAsset.TrackId,
                        TrackName = new FixedString32Bytes(trackAsset.TrackName),
                        Tier = trackAsset.Tier,
                        RecordEveryTicks = trackAsset.RecordEveryTicks,
                        WindowTicks = trackAsset.WindowTicks,
                        Spatial = trackAsset.Spatial
                    });
                }
            }

            // Tag for identification
            AddComponent<RewindConfigTag>(entity);
        }
    }

    /// <summary>
    /// Tag component to identify rewind config entities.
    /// </summary>
    public struct RewindConfigTag : IComponentData { }

    /// <summary>
    /// Buffer element storing track definition data for bootstrap processing.
    /// </summary>
    public struct RewindConfigTrackEntry : IBufferElementData
    {
        public byte TrackId;
        public FixedString32Bytes TrackName;
        public RewindTier Tier;
        public uint RecordEveryTicks;
        public uint WindowTicks;
        public bool Spatial;
    }
}
#endif

