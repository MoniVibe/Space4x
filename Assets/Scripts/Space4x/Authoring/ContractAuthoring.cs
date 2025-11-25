using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for service contract (1-5 years with fleets, manufacturers, mercenary guilds).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Contract")]
    public sealed class ContractAuthoring : MonoBehaviour
    {
        [Tooltip("Contract type")]
        public ContractType contractType = ContractType.Fleet;

        [Tooltip("Employer ID (fleet, manufacturer, guild, corporation)")]
        public string employerId = string.Empty;

        [Tooltip("Contract duration in years (1-5)")]
        [Range(1, 5)]
        public int durationYears = 1;

        public sealed class Baker : Unity.Entities.Baker<ContractAuthoring>
        {
            public override void Bake(ContractAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (!string.IsNullOrWhiteSpace(authoring.employerId))
                {
                    // Note: ExpirationTick is set at runtime based on current tick + duration
                    // For authoring, we store the duration and employer ID
                    AddComponent(entity, new Registry.Contract
                    {
                        Type = authoring.contractType,
                        EmployerId = new FixedString64Bytes(authoring.employerId),
                        ExpirationTick = 0 // Set at runtime
                    });
                }
            }
        }
    }
}

