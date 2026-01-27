using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Skills
{
    public enum PrimaryAttribute : byte
    {
        Physique = 0,
        Finesse = 1,
        Will = 2
    }

    public enum XpPool : byte
    {
        Physique = 0,
        Finesse = 1,
        Will = 2,
        General = 3
    }

    public enum SkillId : byte
    {
        None = 0,
        HarvestBotany = 1,
        AnimalHandling = 2,
        Mining = 3,
        Processing = 4,
        ShipEngineering = 5,
        ShipRepair = 6,
        ShipRefit = 7,
        Gunnery = 8,
        Hauling = 9,
        HazardOps = 10
    }

    public struct SkillEntry
    {
        public SkillId Id;
        public byte Level;
        public float Progress;
        public byte MasteryTier;
    }

    public struct SkillSet : IComponentData
    {
        public FixedList32Bytes<SkillEntry> Entries;
        public float PhysiqueXp;
        public float FinesseXp;
        public float WillXp;
        public float GeneralXp;

        public readonly byte GetLevel(SkillId id)
        {
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Id == id)
                {
                    return Entries[i].Level;
                }
            }

            return 0;
        }

        public readonly byte GetMaxLevel()
        {
            byte max = 0;
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Level > max)
                {
                    max = Entries[i].Level;
                }
            }

            return max;
        }

        public void AddSkillXp(SkillId id, float xp)
        {
            if (xp <= 0f)
            {
                return;
            }

            const float xpPerLevel = 100f;
            var entryIndex = EnsureEntry(id);
            if (entryIndex < 0)
            {
                return;
            }

            var entry = Entries[entryIndex];
            entry.Progress += xp / xpPerLevel;

            while (entry.Progress >= 1f)
            {
                entry.Progress -= 1f;
                entry.Level = (byte)math.clamp(entry.Level + 1, 0, 255);
            }

            Entries[entryIndex] = entry;
        }

        private int EnsureEntry(SkillId id)
        {
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Id == id)
                {
                    return i;
                }
            }

            if (Entries.Length >= Entries.Capacity)
            {
                return -1;
            }

            Entries.Add(new SkillEntry
            {
                Id = id,
                Level = 0,
                Progress = 0f,
                MasteryTier = 0
            });
            return Entries.Length - 1;
        }

        public void SetLevel(SkillId id, byte level)
        {
            var index = EnsureEntry(id);
            if (index < 0)
            {
                return;
            }

            var entry = Entries[index];
            entry.Level = level;
            entry.Progress = 0f;
            Entries[index] = entry;
        }
    }
}
