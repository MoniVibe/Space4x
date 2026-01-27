using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Config
{
    /// <summary>
    /// Base class for scene bootstrap profiles.
    /// Games can create ScriptableObject assets inheriting from this to configure initial scene state.
    /// </summary>
    public abstract class BootstrapProfile : ScriptableObject
    {
        /// <summary>
        /// World bounds for spatial grid initialization.
        /// </summary>
        public float3 WorldMin = new float3(-100f, -10f, -100f);
        public float3 WorldMax = new float3(100f, 10f, 100f);

        /// <summary>
        /// Cell size for spatial grid.
        /// </summary>
        public float SpatialCellSize = 4f;

        /// <summary>
        /// Flow field configuration.
        /// </summary>
        public float FlowFieldCellSize = 5f;
        public float2 FlowFieldBoundsMin = new float2(-100f, -100f);
        public float2 FlowFieldBoundsMax = new float2(100f, 100f);

        /// <summary>
        /// Called during scene initialization to apply profile settings.
        /// </summary>
        public abstract void ApplyBootstrap(EntityManager entityManager);
    }
}


