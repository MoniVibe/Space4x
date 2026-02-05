using Space4X.Registry;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Strike Craft Pilot Profile Config")]
    public sealed class Space4XStrikeCraftPilotProfileAuthoring : MonoBehaviour
    {
        [Header("Stance Defaults")]
        [FormerlySerializedAs("friendlyOutlook")]
        public StanceId FriendlyStance = StanceId.Loyalist;
        [FormerlySerializedAs("hostileOutlook")]
        public StanceId HostileStance = StanceId.Mutinous;
        [FormerlySerializedAs("neutralOutlook")]
        public StanceId NeutralStance = StanceId.Neutral;

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
                    FriendlyStance = authoring.FriendlyStance,
                    HostileStance = authoring.HostileStance,
                    NeutralStance = authoring.NeutralStance,
                    LoyalistLawThreshold = Mathf.Clamp(authoring.loyalistLawThreshold, -1f, 1f),
                    MutinousLawThreshold = Mathf.Clamp(authoring.mutinousLawThreshold, -1f, 1f)
                });
            }
        }
    }
}


