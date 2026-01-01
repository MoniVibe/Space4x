using PureDOTS.Runtime.Modules;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Modules/Engine Module Normalization")]
    public sealed class Space4XEngineModuleAuthoring : MonoBehaviour
    {
        [Header("Posture")]
        public ModulePosture InitialPosture = ModulePosture.Standby;

        [Header("Power Draw")]
        [Min(0f)] public float PowerDrawOff = 0f;
        [Min(0f)] public float PowerDrawStandby = 2f;
        [Min(0f)] public float PowerDrawOnline = 6f;
        [Min(0f)] public float PowerDrawEmergency = 8f;

        [Header("Normalization")]
        [Min(0.01f)] public float TauColdToOnline = 6f;
        [Min(0.01f)] public float TauWarmToOnline = 2f;
        [Min(0.01f)] public float TauOnlineToStandby = 3f;
        [Min(0.01f)] public float TauStandbyToOff = 4f;
        [Min(0f)] public float RampRateLimit = 0f;

        [Header("Output")]
        [Min(0f)] public float MaxOutput = 1f;

        private sealed class Baker : Unity.Entities.Baker<Space4XEngineModuleAuthoring>
        {
            public override void Bake(Space4XEngineModuleAuthoring authoring)
            {
                Space4XModuleNormalizationBakeUtility.Bake(this, authoring.InitialPosture,
                    authoring.PowerDrawOff, authoring.PowerDrawStandby, authoring.PowerDrawOnline, authoring.PowerDrawEmergency,
                    authoring.TauColdToOnline, authoring.TauWarmToOnline, authoring.TauOnlineToStandby, authoring.TauStandbyToOff,
                    authoring.MaxOutput, authoring.RampRateLimit, ModuleCapabilityKind.ThrustAuthority);
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Modules/Thruster Module Normalization")]
    public sealed class Space4XThrusterModuleAuthoring : MonoBehaviour
    {
        [Header("Posture")]
        public ModulePosture InitialPosture = ModulePosture.Standby;

        [Header("Power Draw")]
        [Min(0f)] public float PowerDrawOff = 0f;
        [Min(0f)] public float PowerDrawStandby = 1.5f;
        [Min(0f)] public float PowerDrawOnline = 4f;
        [Min(0f)] public float PowerDrawEmergency = 6f;

        [Header("Normalization")]
        [Min(0.01f)] public float TauColdToOnline = 4f;
        [Min(0.01f)] public float TauWarmToOnline = 1.5f;
        [Min(0.01f)] public float TauOnlineToStandby = 2f;
        [Min(0.01f)] public float TauStandbyToOff = 3f;
        [Min(0f)] public float RampRateLimit = 0f;

        [Header("Output")]
        [Min(0f)] public float MaxOutput = 1f;

        private sealed class Baker : Unity.Entities.Baker<Space4XThrusterModuleAuthoring>
        {
            public override void Bake(Space4XThrusterModuleAuthoring authoring)
            {
                Space4XModuleNormalizationBakeUtility.Bake(this, authoring.InitialPosture,
                    authoring.PowerDrawOff, authoring.PowerDrawStandby, authoring.PowerDrawOnline, authoring.PowerDrawEmergency,
                    authoring.TauColdToOnline, authoring.TauWarmToOnline, authoring.TauOnlineToStandby, authoring.TauStandbyToOff,
                    authoring.MaxOutput, authoring.RampRateLimit, ModuleCapabilityKind.TurnAuthority);
            }
        }
    }

    internal static class Space4XModuleNormalizationBakeUtility
    {
        public static void Bake<T>(Baker<T> baker,
            ModulePosture initialPosture,
            float powerDrawOff,
            float powerDrawStandby,
            float powerDrawOnline,
            float powerDrawEmergency,
            float tauColdToOnline,
            float tauWarmToOnline,
            float tauOnlineToStandby,
            float tauStandbyToOff,
            float maxOutput,
            float rampRateLimit,
            ModuleCapabilityKind capability)
            where T : Component
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            var spec = BuildSpec(powerDrawOff, powerDrawStandby, powerDrawOnline, powerDrawEmergency,
                tauColdToOnline, tauWarmToOnline, tauOnlineToStandby, tauStandbyToOff, maxOutput, rampRateLimit, capability);

            baker.AddComponent(entity, new PureDOTS.Runtime.Modules.ModuleSpec { Spec = spec });
            baker.AddComponent(entity, new ModuleRuntimeState
            {
                Posture = initialPosture,
                NormalizedOutput = 0f,
                TargetOutput = ResolveInitialTarget(initialPosture),
                TimeInState = 0f
            });
            baker.AddComponent(entity, new ModulePowerRequest
            {
                RequestedPower = ResolvePowerDraw(initialPosture, powerDrawOff, powerDrawStandby, powerDrawOnline, powerDrawEmergency)
            });
            baker.AddComponent(entity, new ModulePowerAllocation
            {
                AllocatedPower = 0f,
                SupplyRatio = 1f
            });
            baker.AddBuffer<ModuleCommand>(entity);
        }

        private static BlobAssetReference<ModuleSpecBlob> BuildSpec(
            float powerDrawOff,
            float powerDrawStandby,
            float powerDrawOnline,
            float powerDrawEmergency,
            float tauColdToOnline,
            float tauWarmToOnline,
            float tauOnlineToStandby,
            float tauStandbyToOff,
            float maxOutput,
            float rampRateLimit,
            ModuleCapabilityKind capability)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModuleSpecBlob>();

            root.PowerDrawOff = math.max(0f, powerDrawOff);
            root.PowerDrawStandby = math.max(0f, powerDrawStandby);
            root.PowerDrawOnline = math.max(0f, powerDrawOnline);
            root.PowerDrawEmergency = math.max(0f, powerDrawEmergency);
            root.TauColdToOnline = math.max(0.01f, tauColdToOnline);
            root.TauWarmToOnline = math.max(0.01f, tauWarmToOnline);
            root.TauOnlineToStandby = math.max(0.01f, tauOnlineToStandby);
            root.TauStandbyToOff = math.max(0.01f, tauStandbyToOff);
            root.MaxOutput = math.max(0f, maxOutput);
            root.RampRateLimit = math.max(0f, rampRateLimit);
            root.Capability = capability;

            var blob = builder.CreateBlobAssetReference<ModuleSpecBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static float ResolveInitialTarget(ModulePosture posture)
        {
            return posture == ModulePosture.Online || posture == ModulePosture.Emergency ? 1f : 0f;
        }

        private static float ResolvePowerDraw(ModulePosture posture, float off, float standby, float online, float emergency)
        {
            return posture switch
            {
                ModulePosture.Off => off,
                ModulePosture.Standby => standby,
                ModulePosture.Online => online,
                ModulePosture.Emergency => emergency,
                _ => off
            };
        }
    }
}
