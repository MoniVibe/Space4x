using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for assigning medical staff to a facility owner.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Medical Staff Assignment")]
    public sealed class MedicalStaffAssignmentAuthoring : MonoBehaviour
    {
        [Tooltip("Facility owner (ship/station/colony) to assign this staff member to.")]
        public GameObject facilityOwner;
        [Range(0, 255)]
        [Tooltip("0=Clinic, 1=Surgery, 2=Research, other=Flex/On-Call.")]
        public byte shiftIndex = 0;
        public bool isActive = true;

        private sealed class Baker : Baker<MedicalStaffAssignmentAuthoring>
        {
            public override void Bake(MedicalStaffAssignmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var facilityEntity = authoring.facilityOwner != null
                    ? GetEntity(authoring.facilityOwner, TransformUsageFlags.None)
                    : Entity.Null;

                AddComponent(entity, new MedicalStaffAssignment
                {
                    FacilityEntity = facilityEntity,
                    ShiftIndex = authoring.shiftIndex,
                    IsActive = (byte)(authoring.isActive ? 1 : 0)
                });
            }
        }
    }
}
