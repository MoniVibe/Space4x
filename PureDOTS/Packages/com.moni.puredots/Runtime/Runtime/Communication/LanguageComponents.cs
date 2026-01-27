using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Communication
{
    public enum ProficiencyLevel : byte
    {
        None = 0,
        Rudimentary = 1,
        Basic = 2,
        Conversational = 3,
        Fluent = 4,
        Native = 5,
        Scholarly = 6
    }

    [System.Flags]
    public enum LanguageSkillset : uint
    {
        None = 0,
        Understand = 1 << 0,
        Speak = 1 << 1,
        Read = 1 << 2,
        Write = 1 << 3,
        Teach = 1 << 4,
        Translate = 1 << 5,
        CastSpells = 1 << 6,
        DetectLies = 1 << 7,
        Persuade = 1 << 8,
        Intimidate = 1 << 9,
        Negotiate = 1 << 10
    }

    [InternalBufferCapacity(4)]
    public struct LanguageProficiency : IBufferElementData
    {
        public FixedString64Bytes LanguageId;
        public ProficiencyLevel Level;
        public float Experience;
        public byte IsNative;
        public LanguageSkillset Skills;
    }
}
