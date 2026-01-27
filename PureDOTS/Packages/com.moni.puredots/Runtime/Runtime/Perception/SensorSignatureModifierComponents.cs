using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Per-channel signature modifier for cloaks, camouflage, or deliberate amplification.
    /// Multipliers default to 1 (no change) and are applied only when the channel is flagged.
    /// </summary>
    public struct SensorSignatureModifier : IComponentData
    {
        public PerceptionChannel Channels;
        public float VisionMultiplier;
        public float HearingMultiplier;
        public float SmellMultiplier;
        public float EMMultiplier;
        public float GraviticMultiplier;
        public float ExoticMultiplier;
        public float ParanormalMultiplier;

        public static SensorSignatureModifier Neutral => new SensorSignatureModifier
        {
            Channels = PerceptionChannel.None,
            VisionMultiplier = 1f,
            HearingMultiplier = 1f,
            SmellMultiplier = 1f,
            EMMultiplier = 1f,
            GraviticMultiplier = 1f,
            ExoticMultiplier = 1f,
            ParanormalMultiplier = 1f
        };
    }

    public static class SensorSignatureModifierUtilities
    {
        public static float ResolveMultiplier(in SensorSignatureModifier modifier, PerceptionChannel channel)
        {
            return channel switch
            {
                PerceptionChannel.Vision => modifier.VisionMultiplier,
                PerceptionChannel.Hearing => modifier.HearingMultiplier,
                PerceptionChannel.Smell => modifier.SmellMultiplier,
                PerceptionChannel.EM => modifier.EMMultiplier,
                PerceptionChannel.Gravitic => modifier.GraviticMultiplier,
                PerceptionChannel.Exotic => modifier.ExoticMultiplier,
                PerceptionChannel.Paranormal => modifier.ParanormalMultiplier,
                _ => 1f
            };
        }

        public static SensorSignature Apply(in SensorSignature signature, in SensorSignatureModifier modifier)
        {
            var result = signature;

            if ((modifier.Channels & PerceptionChannel.Vision) != 0)
            {
                result.VisualSignature = math.max(0f, signature.VisualSignature * modifier.VisionMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.Hearing) != 0)
            {
                result.AuditorySignature = math.max(0f, signature.AuditorySignature * modifier.HearingMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.Smell) != 0)
            {
                result.OlfactorySignature = math.max(0f, signature.OlfactorySignature * modifier.SmellMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.EM) != 0)
            {
                result.EMSignature = math.max(0f, signature.EMSignature * modifier.EMMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.Gravitic) != 0)
            {
                result.GraviticSignature = math.max(0f, signature.GraviticSignature * modifier.GraviticMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.Exotic) != 0)
            {
                result.ExoticSignature = math.max(0f, signature.ExoticSignature * modifier.ExoticMultiplier);
            }

            if ((modifier.Channels & PerceptionChannel.Paranormal) != 0)
            {
                result.ParanormalSignature = math.max(0f, signature.ParanormalSignature * modifier.ParanormalMultiplier);
            }

            return result;
        }
    }
}
