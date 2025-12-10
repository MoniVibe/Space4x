#if SPACE4X_WORLD_PROFILE
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that applies a Space4XWorldProfile to set the active PureDOTS world profile.
    /// Place this on a GameObject in your scene to configure which systems run in the DOTS world.
    /// </summary>
    public class Space4XWorldProfileAuthoring : MonoBehaviour
    {
        [Header("Profile ID (string, authoring only)")]
        public string ProfileId;

        [Tooltip("Whether to override the default profile resolution (environment variables, etc.)")]
        public bool forceOverrideActiveProfile = true;

        public class Space4XWorldProfileBaker : Baker<Space4XWorldProfileAuthoring>
        {
            public override void Bake(Space4XWorldProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                var profile = new Space4XWorldProfile();

                if (!string.IsNullOrEmpty(authoring.ProfileId))
                {
                    profile.ProfileId = default;
                    profile.ProfileId.CopyFrom(authoring.ProfileId);
                }

                AddComponent(entity, profile);
            }
        }
    }
}

#endif
