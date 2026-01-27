using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Configuration singleton for resource and storehouse interaction parameters.
    /// Values are baked from ResourceInteractionProfile ScriptableObject at authoring time.
    /// </summary>
    public struct ResourceInteractionConfig : IComponentData
    {
        /// <summary>
        /// Maximum distance for depositing resources at storehouse.
        /// </summary>
        public float DepositDistance;
        
        /// <summary>
        /// Maximum distance for withdrawing resources from storehouse.
        /// </summary>
        public float WithdrawDistance;
        
        /// <summary>
        /// Default maximum carry capacity when not specified per item.
        /// </summary>
        public float DefaultMaxCarryCapacity;
        
        /// <summary>
        /// Default resource units remaining when resource pile respawns.
        /// </summary>
        public float DefaultRespawnUnits;
        
        /// <summary>
        /// Minimum seconds since depletion before resource respawns (prevents immediate respawn).
        /// </summary>
        public float MinRespawnDelaySeconds;
        
        /// <summary>
        /// Creates default configuration matching current hard-coded values.
        /// </summary>
        public static ResourceInteractionConfig CreateDefaults()
        {
            return new ResourceInteractionConfig
            {
                DepositDistance = 5f,
                WithdrawDistance = 5f,
                DefaultMaxCarryCapacity = 50f,
                DefaultRespawnUnits = 100f,
                MinRespawnDelaySeconds = 0.01f
            };
        }
    }
}

