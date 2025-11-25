using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for titles (deeds of rulership). Individuals can have multiple titles.
    /// Titles are passed on with inheritance or otherwise depending on outlooks.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Titles")]
    public sealed class TitlesAuthoring : MonoBehaviour
    {
        [Serializable]
        public class TitleData
        {
            [Tooltip("Title tier (Captain, Admiral, Governor, etc.)")]
            public TitleTier tier = TitleTier.None;

            [Tooltip("Title type (Hero, Elite, Ruler)")]
            public TitleType type = TitleType.Hero;

            [Tooltip("Title level/hierarchy - from BandLeader to MultiEmpireRuler. Determines which title is presented (highest level shown).")]
            public TitleLevel level = TitleLevel.None;

            [Tooltip("Title state - Active titles are currently held. Lost/Former titles carry diminished prestige.")]
            public TitleState state = TitleState.Active;

            [Tooltip("Display name (culture-aware variant)")]
            public string displayName = string.Empty;

            [Tooltip("Associated colony ID (if title is colony-specific)")]
            public string colonyId = string.Empty;

            [Tooltip("Associated faction ID (if title is faction-specific)")]
            public string factionId = string.Empty;

            [Tooltip("Associated empire ID (if title is empire-specific)")]
            public string empireId = string.Empty;

            [Tooltip("Acquisition reason (e.g., 'Founded', 'Defended', 'Renown', 'Inherited')")]
            public string acquisitionReason = string.Empty;

            [Tooltip("Loss reason (e.g., 'Broken', 'Fallen', 'Usurped', 'Revoked', 'Disinherited') - only set if state is not Active")]
            public string lossReason = string.Empty;
        }

        [Tooltip("Titles held by this individual (multiple allowed)")]
        public List<TitleData> titles = new List<TitleData>();

        public sealed class Baker : Unity.Entities.Baker<TitlesAuthoring>
        {
            public override void Bake(TitlesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.TitleEntry>(entity);

                if (authoring.titles != null)
                {
                    foreach (var titleData in authoring.titles)
                    {
                        if (titleData.tier != TitleTier.None)
                        {
                            buffer.Add(new Registry.TitleEntry
                            {
                                Tier = titleData.tier,
                                Type = titleData.type,
                                Level = titleData.level != TitleLevel.None ? titleData.level : InferLevelFromTierAndType(titleData.tier, titleData.type),
                                State = titleData.state,
                                DisplayName = new FixedString64Bytes(titleData.displayName ?? string.Empty),
                                ColonyId = new FixedString64Bytes(titleData.colonyId ?? string.Empty),
                                FactionId = new FixedString64Bytes(titleData.factionId ?? string.Empty),
                                EmpireId = new FixedString64Bytes(titleData.empireId ?? string.Empty),
                                AcquiredTick = 0u, // Set at runtime
                                LostTick = titleData.state != TitleState.Active ? 0u : 0u, // Set at runtime when lost
                                AcquisitionReason = new FixedString64Bytes(titleData.acquisitionReason ?? string.Empty),
                                LossReason = new FixedString64Bytes(titleData.lossReason ?? string.Empty)
                            });
                        }
                    }
                }

                // Calculate and add highest ACTIVE title component for presentation
                if (authoring.titles != null && authoring.titles.Count > 0)
                {
                    var highestActiveTitle = FindHighestActiveTitle(authoring.titles);
                    if (highestActiveTitle != null)
                    {
                        AddComponent(entity, new Registry.HighestTitle
                        {
                            Tier = highestActiveTitle.tier,
                            Type = highestActiveTitle.type,
                            Level = highestActiveTitle.level != TitleLevel.None ? highestActiveTitle.level : InferLevelFromTierAndType(highestActiveTitle.tier, highestActiveTitle.type),
                            State = TitleState.Active,
                            DisplayName = new FixedString64Bytes(highestActiveTitle.displayName ?? string.Empty)
                        });
                    }

                    // Also track highest FORMER title (if any) for prestige purposes
                    var highestFormerTitle = FindHighestFormerTitle(authoring.titles);
                    if (highestFormerTitle != null)
                    {
                        AddComponent(entity, new Registry.HighestFormerTitle
                        {
                            Tier = highestFormerTitle.tier,
                            Type = highestFormerTitle.type,
                            Level = highestFormerTitle.level != TitleLevel.None ? highestFormerTitle.level : InferLevelFromTierAndType(highestFormerTitle.tier, highestFormerTitle.type),
                            State = highestFormerTitle.state,
                            DisplayName = new FixedString64Bytes(highestFormerTitle.displayName ?? string.Empty),
                            LossReason = new FixedString64Bytes(highestFormerTitle.lossReason ?? string.Empty)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Infers title level from tier and type if level is not explicitly set.
        /// </summary>
        private static TitleLevel InferLevelFromTierAndType(TitleTier tier, TitleType type)
        {
            // Infer level based on tier and type if not explicitly set
            switch (type)
            {
                case TitleType.Hero:
                    switch (tier)
                    {
                        case TitleTier.Captain: return TitleLevel.BandLeader;
                        case TitleTier.Admiral: return TitleLevel.FactionHero;
                        case TitleTier.Governor: return TitleLevel.ColonyHero;
                        default: return TitleLevel.ColonyHero;
                    }
                case TitleType.Elite:
                    switch (tier)
                    {
                        case TitleTier.Captain: return TitleLevel.SquadLeader;
                        case TitleTier.Admiral: return TitleLevel.FactionElite;
                        case TitleTier.Governor: return TitleLevel.ColonyElite;
                        case TitleTier.StellarLord: return TitleLevel.EmpireElite;
                        default: return TitleLevel.ColonyElite;
                    }
                case TitleType.Ruler:
                    switch (tier)
                    {
                        case TitleTier.Captain: return TitleLevel.BandLeader;
                        case TitleTier.Governor: return TitleLevel.ColonyRuler;
                        case TitleTier.HighMarshal: return TitleLevel.FactionRuler;
                        case TitleTier.StellarLord: return TitleLevel.WorldRuler;
                        case TitleTier.InterstellarLord: return TitleLevel.MultiWorldRuler;
                        case TitleTier.Stellarch: return TitleLevel.EmpireRuler;
                        case TitleTier.GrandStellarch: return TitleLevel.MultiEmpireRuler;
                        default: return TitleLevel.ColonyRuler;
                    }
                default:
                    return TitleLevel.None;
            }
        }

        /// <summary>
        /// Finds the highest level ACTIVE title from a list of titles.
        /// Only active titles are considered for presentation.
        /// </summary>
        private static TitleData FindHighestActiveTitle(List<TitleData> titles)
        {
            TitleData highest = null;
            TitleLevel highestLevel = TitleLevel.None;

            foreach (var title in titles)
            {
                // Only consider active titles
                if (title.state != TitleState.Active) continue;

                var level = title.level != TitleLevel.None ? title.level : InferLevelFromTierAndType(title.tier, title.type);
                if (level > highestLevel)
                {
                    highestLevel = level;
                    highest = title;
                }
            }

            return highest;
        }

        /// <summary>
        /// Finds the highest level FORMER title from a list of titles.
        /// Former titles carry prestige but are shadows of proper titles.
        /// </summary>
        private static TitleData FindHighestFormerTitle(List<TitleData> titles)
        {
            TitleData highest = null;
            TitleLevel highestLevel = TitleLevel.None;

            foreach (var title in titles)
            {
                // Only consider lost/former titles
                if (title.state == TitleState.Active) continue;

                var level = title.level != TitleLevel.None ? title.level : InferLevelFromTierAndType(title.tier, title.type);
                if (level > highestLevel)
                {
                    highestLevel = level;
                    highest = title;
                }
            }

            return highest;
        }
    }
}

