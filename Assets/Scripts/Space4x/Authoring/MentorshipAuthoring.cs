using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for mentorship relationships (mentor/mentee).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Mentorship")]
    public sealed class MentorshipAuthoring : MonoBehaviour
    {
        [Tooltip("Mentor entity ID (high-expertise individual who trains juniors)")]
        public string mentorId = string.Empty;

        [Tooltip("Mentee entity IDs (juniors being trained)")]
        public string[] menteeIds = new string[0];

        public sealed class Baker : Unity.Entities.Baker<MentorshipAuthoring>
        {
            public override void Bake(MentorshipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Add mentor reference
                if (!string.IsNullOrWhiteSpace(authoring.mentorId))
                {
                    AddComponent(entity, new Registry.Mentor
                    {
                        MentorId = new FixedString64Bytes(authoring.mentorId)
                    });
                }

                // Add mentee buffer
                if (authoring.menteeIds != null && authoring.menteeIds.Length > 0)
                {
                    var buffer = AddBuffer<Registry.Mentee>(entity);
                    foreach (var menteeId in authoring.menteeIds)
                    {
                        if (!string.IsNullOrWhiteSpace(menteeId))
                        {
                            buffer.Add(new Registry.Mentee
                            {
                                MenteeId = new FixedString64Bytes(menteeId)
                            });
                        }
                    }
                }
            }
        }
    }
}

