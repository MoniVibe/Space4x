using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for medical staff role metadata on crew entities.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Staff Role")]
    public sealed class MedicalStaffRoleAuthoring : MonoBehaviour
    {
        public MedicalStaffRoleType role = MedicalStaffRoleType.Doctor;
        [Range(0f, 1f)]
        public float skill01 = 0.6f;
        public bool isLicensed = true;
        public bool isCertified = true;

        private sealed class Baker : Baker<MedicalStaffRoleAuthoring>
        {
            public override void Bake(MedicalStaffRoleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MedicalStaffRole
                {
                    Role = authoring.role,
                    Skill01 = (half)math.clamp(authoring.skill01, 0f, 1f),
                    IsLicensed = (byte)(authoring.isLicensed ? 1 : 0),
                    IsCertified = (byte)(authoring.isCertified ? 1 : 0)
                });
            }
        }
    }
}
