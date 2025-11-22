using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// Authoring helper to seed crew skills, starting XP, and hazard resistances on vessels/crews.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCrewSkillsAuthoring : MonoBehaviour
    {
        [Range(0f, 1f)] public float MiningSkill = 0f;
        [Range(0f, 1f)] public float HaulingSkill = 0f;
        [Range(0f, 1f)] public float CombatSkill = 0f;
        [Range(0f, 1f)] public float RepairSkill = 0f;
        [Range(0f, 1f)] public float ExplorationSkill = 0f;

        [Min(0f)] public float MiningXp = 0f;
        [Min(0f)] public float HaulingXp = 0f;
        [Min(0f)] public float CombatXp = 0f;
        [Min(0f)] public float RepairXp = 0f;
        [Min(0f)] public float ExplorationXp = 0f;

        [SerializeField] private HazardResistanceEntry[] hazardResistances = Array.Empty<HazardResistanceEntry>();

        [Serializable]
        public struct HazardResistanceEntry
        {
            public HazardTypeId HazardType;
            [Range(0f, 1f)] public float ResistanceMultiplier;
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XCrewSkillsAuthoring>
        {
            public override void Bake(Space4XCrewSkillsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                var skills = new CrewSkills
                {
                    MiningSkill = math.clamp(authoring.MiningSkill, 0f, 1f),
                    HaulingSkill = math.clamp(authoring.HaulingSkill, 0f, 1f),
                    CombatSkill = math.clamp(authoring.CombatSkill, 0f, 1f),
                    RepairSkill = math.clamp(authoring.RepairSkill, 0f, 1f),
                    ExplorationSkill = math.clamp(authoring.ExplorationSkill, 0f, 1f)
                };

                var xp = new SkillExperienceGain
                {
                    MiningXp = math.max(0f, ResolveXp(authoring.MiningXp, skills.MiningSkill)),
                    HaulingXp = math.max(0f, ResolveXp(authoring.HaulingXp, skills.HaulingSkill)),
                    CombatXp = math.max(0f, ResolveXp(authoring.CombatXp, skills.CombatSkill)),
                    RepairXp = math.max(0f, ResolveXp(authoring.RepairXp, skills.RepairSkill)),
                    ExplorationXp = math.max(0f, ResolveXp(authoring.ExplorationXp, skills.ExplorationSkill)),
                    LastProcessedTick = 0
                };

                AddComponent(entity, xp);
                AddComponent(entity, skills);

                if (authoring.hazardResistances != null && authoring.hazardResistances.Length > 0)
                {
                    var buffer = AddBuffer<HazardResistance>(entity);
                    foreach (var entry in authoring.hazardResistances)
                    {
                        var multiplier = math.clamp(entry.ResistanceMultiplier, 0f, 1f);
                        buffer.Add(new HazardResistance
                        {
                            HazardType = entry.HazardType,
                            ResistanceMultiplier = multiplier
                        });
                    }
                }
            }

            private static float ResolveXp(float authoredXp, float authoredSkill)
            {
                if (authoredXp > 0f)
                {
                    return authoredXp;
                }

                if (authoredSkill <= 0f)
                {
                    return 0f;
                }

                return Space4XSkillUtility.SkillToXp(authoredSkill);
            }
        }
    }
}
