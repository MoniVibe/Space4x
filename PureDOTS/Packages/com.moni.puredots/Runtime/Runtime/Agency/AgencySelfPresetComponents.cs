using Unity.Entities;

namespace PureDOTS.Runtime.Agency
{
    public enum AgencySelfPresetKind : byte
    {
        Default = 0,
        Tool = 1,
        Sentient = 2,
        Custom = 3
    }

    public struct AgencySelfPreset : IComponentData
    {
        public AgencySelfPresetKind Kind;
        public AgencySelf Self;
    }

    public static class AgencySelfPresetUtility
    {
        public static AgencySelf Resolve(in AgencySelfPreset preset)
        {
            return preset.Kind switch
            {
                AgencySelfPresetKind.Tool => AgencyDefaults.ToolSelf(),
                AgencySelfPresetKind.Sentient => AgencyDefaults.SentientSelf(),
                AgencySelfPresetKind.Custom => preset.Self,
                _ => AgencyDefaults.DefaultSelf()
            };
        }
    }
}
