using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Space4X.Scenario;
using Hash128 = Unity.Entities.Hash128;

namespace Space4X.Presentation
{
    // Legacy stubs trimmed; real presentation tags live in Rendering/Space4XPresentationTags.cs

    public struct Space4XMiningVisualConfig : IComponentData
    {
        public Space4XMiningPrimitive CarrierPrimitive;
        public Space4XMiningPrimitive MiningVesselPrimitive;
        public Space4XMiningPrimitive AsteroidPrimitive;
        public float CarrierScale;
        public float MiningVesselScale;
        public float AsteroidScale;
        public float4 CarrierColor;
        public float4 MiningVesselColor;
        public float4 AsteroidColor;
    }
}

namespace Space4X.Scenario
{
    // Legacy scenario placeholder for mining primitives.
    public enum Space4XMiningPrimitive : byte
    {
        None = 0,
        Capsule = 1,
        Cylinder = 2,
        Sphere = 3,
        Asteroid = 4,
        Rock = 5,
        Ice = 6
    }
}

namespace Space4X.Registry
{
    // Minimal placeholders so effect request buffers compile within the gameplay asmdef.
    public struct Space4XEffectRequestStream : IComponentData { }

    public struct PlayEffectRequest : IBufferElementData
    {
        public FixedString64Bytes EffectId;
        public Entity AttachTo;
        public float3 Position;
        public float3 Direction;
        public float Lifetime;
        public float Intensity;
    }
}

namespace Space4X.Presentation.Camera
{
    // Stub camera system to satisfy interaction system dependencies.
    public partial struct Space4XCameraSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
    }
}
