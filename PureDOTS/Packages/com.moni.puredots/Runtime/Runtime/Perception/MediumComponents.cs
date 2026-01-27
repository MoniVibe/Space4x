using Unity.Entities;

namespace PureDOTS.Runtime.Perception
{
    public enum MediumType : byte
    {
        Vacuum = 0,
        Gas = 1,
        Liquid = 2,
        Solid = 3,
        Mixed = 4
    }

    /// <summary>
    /// Medium context sampled at an entity position.
    /// Used to gate channel-based perception and comms.
    /// </summary>
    public struct MediumContext : IComponentData
    {
        public MediumType Type;
        public float SoundAttenuation;
        public float SmellAttenuation;
        public float EMTransmission;

        public static MediumContext DefaultGas => new MediumContext
        {
            Type = MediumType.Gas,
            SoundAttenuation = 0f,
            SmellAttenuation = 0f,
            EMTransmission = 1f
        };

        public static MediumContext Vacuum => new MediumContext
        {
            Type = MediumType.Vacuum,
            SoundAttenuation = 1f,
            SmellAttenuation = 1f,
            EMTransmission = 1f
        };
    }

    public static class MediumUtilities
    {
        public static bool SupportsChannel(MediumType medium, PerceptionChannel channel)
        {
            if ((channel & (PerceptionChannel.Hearing | PerceptionChannel.Smell)) == 0)
            {
                return true;
            }

            switch (medium)
            {
                case MediumType.Vacuum:
                    return false;
                case MediumType.Solid:
                    return (channel & PerceptionChannel.Smell) == 0;
                default:
                    return true;
            }
        }

        public static PerceptionChannel FilterChannels(MediumType medium, PerceptionChannel channels)
        {
            switch (medium)
            {
                case MediumType.Vacuum:
                    return channels & ~(PerceptionChannel.Hearing | PerceptionChannel.Smell);
                case MediumType.Solid:
                    return channels & ~PerceptionChannel.Smell;
                default:
                    return channels;
            }
        }
    }
}
