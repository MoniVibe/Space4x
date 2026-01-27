using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Presentation
{
    /// <summary>
    /// Provides built-in sample binding sets for graybox placeholder visuals.
    /// </summary>
    public static class PresentationBindingSamples
    {
        private static readonly PresentationEffectBinding[] GrayboxMinimalEffects =
        {
            new PresentationEffectBinding
            {
                EffectId = 1,
                Kind = PresentationKind.Vfx,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"fx.ring.minimal",
                    PaletteIndex = 0,
                    Size = 1f,
                    Speed = 1f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 0.85f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 1001,
                Kind = PresentationKind.Particle,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"fx.miracle.ping",
                    PaletteIndex = 2,
                    Size = 1.1f,
                    Speed = 1.1f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 1.4f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 2001,
                Kind = PresentationKind.Ui,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"ui.banner.pulse",
                    PaletteIndex = 4,
                    Size = 1f,
                    Speed = 0.7f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 2.25f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 3001,
                Kind = PresentationKind.Sfx,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"sfx.chime.minimal",
                    PaletteIndex = 1,
                    Size = 1f,
                    Speed = 1f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 0.8f,
                AttachRule = PresentationAttachRule.World
            }
        };

        private static readonly PresentationCompanionBinding[] GrayboxMinimalCompanions =
        {
            new PresentationCompanionBinding
            {
                CompanionId = 7,
                Kind = PresentationKind.Mesh,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"comp.capsule.basic",
                    PaletteIndex = 1,
                    Size = 1f,
                    Speed = 1f
                },
                AttachRule = PresentationAttachRule.FollowTarget
            },
            new PresentationCompanionBinding
            {
                CompanionId = 101,
                Kind = PresentationKind.Mesh,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"comp.banner.basic",
                    PaletteIndex = 3,
                    Size = 1.2f,
                    Speed = 1f
                },
                AttachRule = PresentationAttachRule.FollowTarget
            }
        };

        private static readonly PresentationEffectBinding[] GrayboxFancyEffects =
        {
            new PresentationEffectBinding
            {
                EffectId = 1,
                Kind = PresentationKind.Vfx,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"fx.ring.fancy",
                    PaletteIndex = 5,
                    Size = 1.2f,
                    Speed = 1.3f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 1.1f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 1001,
                Kind = PresentationKind.Particle,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"fx.miracle.ping.fancy",
                    PaletteIndex = 6,
                    Size = 1.4f,
                    Speed = 1.25f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 1.6f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 2001,
                Kind = PresentationKind.Ui,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"ui.banner.glow",
                    PaletteIndex = 5,
                    Size = 1.05f,
                    Speed = 0.85f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 2.5f,
                AttachRule = PresentationAttachRule.World
            },
            new PresentationEffectBinding
            {
                EffectId = 3001,
                Kind = PresentationKind.Sfx,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"sfx.chime.fancy",
                    PaletteIndex = 4,
                    Size = 1f,
                    Speed = 1f
                },
                Lifetime = PresentationLifetimePolicy.Timed,
                DurationSeconds = 0.9f,
                AttachRule = PresentationAttachRule.World
            }
        };

        private static readonly PresentationCompanionBinding[] GrayboxFancyCompanions =
        {
            new PresentationCompanionBinding
            {
                CompanionId = 7,
                Kind = PresentationKind.Mesh,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"comp.capsule.halo",
                    PaletteIndex = 6,
                    Size = 1.1f,
                    Speed = 1.1f
                },
                AttachRule = PresentationAttachRule.FollowTarget
            },
            new PresentationCompanionBinding
            {
                CompanionId = 101,
                Kind = PresentationKind.Mesh,
                Style = new PresentationStyleBlock
                {
                    Style = (FixedString64Bytes)"comp.banner.glow",
                    PaletteIndex = 5,
                    Size = 1.35f,
                    Speed = 1f
                },
                AttachRule = PresentationAttachRule.FollowTarget
            }
        };

        public static bool TryBuild(string key, Allocator allocator, out BlobAssetReference<PresentationBindingBlob> blob, out FixedString64Bytes appliedKey)
        {
            appliedKey = default;
            blob = default;

            var normalized = string.IsNullOrWhiteSpace(key) ? "graybox-minimal" : key.Trim().ToLowerInvariant();
            PresentationEffectBinding[] effects;
            PresentationCompanionBinding[] companions;

            switch (normalized)
            {
                case "graybox-fancy":
                    effects = GrayboxFancyEffects;
                    companions = GrayboxFancyCompanions;
                    appliedKey = (FixedString64Bytes)"graybox-fancy";
                    break;
                default:
                    effects = GrayboxMinimalEffects;
                    companions = GrayboxMinimalCompanions;
                    appliedKey = (FixedString64Bytes)"graybox-minimal";
                    break;
            }

            blob = Build(effects, companions, allocator);
            return blob.IsCreated;
        }

        private static BlobAssetReference<PresentationBindingBlob> Build(
            PresentationEffectBinding[] effects,
            PresentationCompanionBinding[] companions,
            Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PresentationBindingBlob>();

            var effectsArray = builder.Allocate(ref root.Effects, effects.Length);
            for (int i = 0; i < effects.Length; i++)
            {
                effectsArray[i] = effects[i];
            }

            var companionsArray = builder.Allocate(ref root.Companions, companions.Length);
            for (int i = 0; i < companions.Length; i++)
            {
                companionsArray[i] = companions[i];
            }

            var blob = builder.CreateBlobAssetReference<PresentationBindingBlob>(allocator);
            builder.Dispose();

            return blob;
        }
    }
}
