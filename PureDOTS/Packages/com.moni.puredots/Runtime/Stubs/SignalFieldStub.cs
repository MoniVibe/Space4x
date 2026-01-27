// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    public static class SignalFieldStub
    {
        public static void EmitSignal(in Entity emitter, PerceptionChannel channel, float strength, float3 position) { }

        public static float SampleSignalField(float3 position, PerceptionChannel channel) => 0f;

        public static void UpdateSignalFieldCells() { }

        public static void DecaySignalField(float deltaTime) { }
    }
}

