// [TRI-STUB] Stub components for coordinated combat
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Volley coordinator - coordinates simultaneous attacks.
    /// </summary>
    public struct VolleyCoordinator : IComponentData
    {
        public Entity VolleyCommander;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public float ChargeProgress;
        public float VolleyPowerMultiplier;
        public byte ReadyCount;
        public byte TotalShooters;
        public byte FireOnCommand;
    }

    /// <summary>
    /// Volley member - shooter in coordinated volley.
    /// </summary>
    [InternalBufferCapacity(20)]
    public struct VolleyMember : IBufferElementData
    {
        public Entity ShooterEntity;
        public byte IsReady;
        public float AccuracyBonus;
        public float ReloadTime;
    }
}

