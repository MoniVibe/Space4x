using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    /// <summary>
    /// Assigns authority capability flags to an entity (leaders, sentient buildings, artifacts).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthorityCapabilityAuthoring : MonoBehaviour
    {
        [SerializeField] private AuthorityCapabilityFlags flags = AuthorityCapabilityFlags.IssueOrders;

        private sealed class Baker : Baker<AuthorityCapabilityAuthoring>
        {
            public override void Bake(AuthorityCapabilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AuthorityCapabilities
                {
                    Flags = authoring.flags
                });
            }
        }
    }
}



