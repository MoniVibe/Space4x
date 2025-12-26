using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Strike Craft Pilot Profile Config")]
    public sealed class Space4XStrikeCraftPilotProfileAuthoring : MonoBehaviour
    {
        [Header("Outlook Defaults")]
        public OutlookId friendlyOutlook = OutlookId.Loyalist;
        public OutlookId hostileOutlook = OutlookId.Mutinous;
        public OutlookId neutralOutlook = OutlookId.Neutral;

        [Header("Lawfulness Thresholds")]
        [Range(-1f, 1f)] public float loyalistLawThreshold = 0.55f;
        [Range(-1f, 1f)] public float mutinousLawThreshold = -0.55f;

        public sealed class Baker : Baker<Space4XStrikeCraftPilotProfileAuthoring>
        {
            public override void Bake(Space4XStrikeCraftPilotProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StrikeCraftPilotProfileConfig
                {
                    FriendlyOutlook = authoring.friendlyOutlook,
                    HostileOutlook = authoring.hostileOutlook,
                    NeutralOutlook = authoring.neutralOutlook,
                    LoyalistLawThreshold = Mathf.Clamp(authoring.loyalistLawThreshold, -1f, 1f),
                    MutinousLawThreshold = Mathf.Clamp(authoring.mutinousLawThreshold, -1f, 1f)
                });
            }
        }
    }
}
